using O2DESNet;

namespace SimulationChallenge2026;

/// <summary>
/// Represents shipments being transported by vessel.
///
/// Design:
/// - Start is controlled by vessel signals.
/// - Finish is controlled by vessel signals.
///
/// Start:
/// - For a signalled vessel, start all shipments loaded for the vessel's next segment.
/// - Before starting, assign shipment.CarryingVessel to that vessel.
///
/// Finish:
/// - For a signalled vessel, finish all ready shipments whose carrying vessel is that vessel
///   and which are no longer in vessel.CarriedShipments.
/// - The discharge segment validation is handled when shipments are removed from
///   vessel.CarriedShipments, not here.
/// - Before finishing, clear shipment.CarryingVessel.
/// </summary>
public class Shipment_BeingTransported : ActivityHandler<Shipment>
{
    /// <summary>
    /// Vessel signals for starting transported shipments.
    /// </summary>
    private readonly HashSet<Vessel> P_StartSignals = new();

    /// <summary>
    /// Vessel signals for finishing transported shipments.
    /// </summary>
    private readonly HashSet<Vessel> Q_FinishSignals = new();

    public Shipment_BeingTransported(int seed = 0)
        : base(
              id: nameof(Shipment_BeingTransported),
              seed: seed)
    {
    }

    /// <summary>
    /// External start signal indicating that shipments loaded onto a vessel
    /// should begin the transported activity.
    /// </summary>
    public void SignalStart(Vessel vessel)
    {
        if (vessel == null)
            throw new ArgumentNullException(nameof(vessel));

        Log("SignalStart", vessel);

        if (P_StartSignals.Add(vessel))
        {
            Schedule(AttemptStart);
        }
    }

    /// <summary>
    /// External finish signal indicating that shipments associated with a vessel
    /// may finish the transported activity.
    /// </summary>
    public void SignalFinish(Vessel vessel)
    {
        if (vessel == null)
            throw new ArgumentNullException(nameof(vessel));

        Log("SignalFinish", vessel);

        if (Q_FinishSignals.Add(vessel))
        {
            Schedule(AttemptFinish);
        }
    }

    /// <summary>
    /// Starts transported shipments for each signalled vessel.
    ///
    /// The vessel identifies the shipments that should be loaded for its next segment.
    /// Each shipment must have already requested start in this activity before it can
    /// be started here.
    /// </summary>
    protected override void AttemptStart()
    {
        Log("AttemptStart");

        foreach (var vessel in P_StartSignals.ToList())
        {
            var loadingShipments = vessel.GetLoadingShipmentsAtNextSegment();

            foreach (var shipment in loadingShipments)
            {
                Require(
                    shipment.CarryingVessel == null,
                    nameof(AttemptStart),
                    $"Shipment {shipment.Index} already has carrying vessel " +
                    $"{shipment.CarryingVessel?.Index}.");

                Require(
                    R_LoadsRequestedStart.Contains(shipment),
                    nameof(AttemptStart),
                    $"Shipment {shipment.Index} is loaded onto vessel {vessel.Index} " +
                    $"without requesting start.");

                shipment.CarryingVessel = vessel;
                Start(shipment);
            }

            P_StartSignals.Remove(vessel);
        }
    }

    /// <summary>
    /// Finishes transported shipments for each signalled vessel.
    ///
    /// A shipment can finish this activity when:
    /// - it is ready to finish;
    /// - it is currently carried by the signalled vessel; and
    /// - it has already been removed from vessel.CarriedShipments.
    ///
    /// The vessel's current segment is not checked here because the vessel may
    /// already have advanced to its next segment by the time this activity finishes.
    /// </summary>
    protected override void AttemptFinish()
    {
        Log("AttemptFinish");

        foreach (var vessel in Q_FinishSignals.ToList())
        {
            var shipmentsToFinish = D_LoadsReadyFinish
                .Where(shipment =>
                    shipment.CarryingVessel == vessel &&
                    !vessel.CarriedShipments.Contains(shipment))
                .ToList();

            foreach (var shipment in shipmentsToFinish)
            {
                shipment.CarryingVessel = null;
                Finish(shipment);
            }

            Q_FinishSignals.Remove(vessel);
        }
    }

    #region Statistics - TEU in Transition by Demand

    /// <summary>
    /// Time-weighted TEU counters grouped by demand.
    ///
    /// Each counter tracks the total TEU volume of shipments currently in transition
    /// for the corresponding origin-destination demand.
    ///
    /// A shipment is considered in transition from the moment it starts transportation
    /// on its first booking until it departs transportation after its last booking.
    /// </summary>
    public Dictionary<Demand, HourCounter> HC_TeusInTransitionByDemand { get; } = new();

    protected override void Start(Shipment shipment)
    {
        base.Start(shipment);

        if (shipment.CurrentBookingIndex != 1)
            return;

        var demand = RequireNotNull(
            shipment.Demand,
            nameof(Start),
            $"Shipment {shipment.Index} demand");

        if (!HC_TeusInTransitionByDemand.TryGetValue(demand, out var demandCounter))
        {
            demandCounter = AddHourCounter();
            HC_TeusInTransitionByDemand[demand] = demandCounter;
        }

        demandCounter.ObserveChange(shipment.TeuSize);
    }

    public override void Depart(Shipment shipment)
    {
        if (!F_LoadsFinished.Contains(shipment))
            return;

        bool isLastBooking = shipment.IsAtLastBooking();

        base.Depart(shipment);

        if (!isLastBooking)
            return;

        var demand = RequireNotNull(
            shipment.Demand,
            nameof(Depart),
            $"Shipment {shipment.Index} demand");

        if (!HC_TeusInTransitionByDemand.TryGetValue(demand, out var demandCounter))
        {
            ThrowActivityException(
                nameof(Depart),
                $"Cannot update in-transition TEU statistics for Shipment {shipment.Index} " +
                $"because no HourCounter exists for its demand.");
        }

        demandCounter.ObserveChange(-shipment.TeuSize);
    }        

    #endregion
}