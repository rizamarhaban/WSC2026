namespace SimulationChallenge2026;

/// <summary>
/// Represents the activity in which vessels are served at berth.
///
/// During this activity, a vessel may load new shipments, discharge arrived
/// shipments, and then advance to its next route segment.
///
/// Start side:
/// - Controlled by shipment signals accumulated in P_StartSignals.
/// - For each vessel requesting start, the handler searches the pending shipment pool
///   for shipments whose current booking:
///   1. belongs to the same service route as the vessel; and
///   2. departs on the vessel's next segment.
/// - A subset of such shipments is selected and added to vessel.CarriedShipments,
///   subject to the vessel TEU capacity constraint.
/// - The vessel is allowed to start even if no shipment is selected for loading.
///
/// Finish side:
/// - Controlled by berth signals.
/// - A ready vessel is matched by vessel.CurrentBerth == berth.
/// - Shipments whose current booking arrives at the vessel's current segment are
///   removed from vessel.CarriedShipments.
/// - The vessel is then advanced to the next segment in its cyclic service route
///   and released from the berth.
/// </summary>
public class Vessel_BeingServed : ActivityHandler<Vessel>
{
    /// <summary>
    /// Pending shipment signals representing shipments available for loading consideration.
    ///
    /// This is a shared pool across all vessels handled by this activity.
    /// A shipment is matched to a vessel later in AttemptStart(), based on the
    /// vessel's assigned service route and next segment.
    /// </summary>
    private readonly HashSet<Shipment> P_StartSignals = new();

    /// <summary>
    /// Finish signal tokens keyed by berth.
    ///
    /// Each token may allow one ready vessel currently occupying the signalled
    /// berth to finish being served.
    /// </summary>
    private readonly HashSet<Berth> Q_FinishSignals = new();

    public Vessel_BeingServed(int seed = 0)
        : base(
              id: nameof(Vessel_BeingServed),
              seed: seed)
    {
    }

    /// <summary>
    /// Registers a shipment as available for loading consideration.
    ///
    /// This method does not directly assign the shipment to a vessel.
    /// It only adds the shipment to the pending shipment pool and schedules
    /// AttemptStart(). Actual vessel-shipment matching and capacity checks are
    /// handled in AttemptStart().
    /// </summary>
    public void SignalStart(Shipment shipment)
    {
        if (shipment == null)
            throw new ArgumentNullException(nameof(shipment));

        Log(nameof(SignalStart), shipment);

        Require(
            shipment.CarryingVessel == null,
            nameof(SignalStart),
            $"Shipment {shipment.Index} already has carrying vessel {shipment.CarryingVessel?.Index}.");

        if (P_StartSignals.Add(shipment))
        {
            Schedule(AttemptStart);
        }
    }

    /// <summary>
    /// Signals that a berth service may finish.
    ///
    /// The berth is used to identify which ready vessel can leave this activity.
    /// </summary>
    public void SignalFinish(Berth berth)
    {
        if (berth == null)
            throw new ArgumentNullException(nameof(berth));

        Log(nameof(SignalFinish), berth);

        if (Q_FinishSignals.Add(berth))
        {
            Schedule(AttemptFinish);
        }
    }

    /// <summary>
    /// Attempts to start vessel service by loading feasible shipments.
    ///
    /// For each vessel that has requested start:
    /// 1. Determine the vessel's assigned service route and next segment.
    /// 2. Select pending shipments whose current booking departs on that next segment.
    /// 3. Calculate occupied TEU before loading. Shipments arriving at the current
    ///    segment are excluded because they are treated as dischargeable at this berth.
    /// 4. Select loadable shipments within the remaining TEU capacity.
    /// 5. Add selected shipments to vessel.CarriedShipments.
    /// 6. Remove selected shipments from the pending shipment pool.
    /// 7. Start the vessel service activity, even if no shipment is loaded.
    /// </summary>
    protected override void AttemptStart()
    {
        Log(nameof(AttemptStart));

        foreach (var vessel in R_LoadsRequestedStart.ToList())
        {
            var assignedServiceRoute = RequireNotNull(
                vessel.AssignedServiceRoute,
                nameof(AttemptStart),
                $"Vessel {vessel.Index} assigned service route");

            var vesselClass = RequireNotNull(
                vessel.VesselClass,
                nameof(AttemptStart),
                $"Vessel {vessel.Index} vessel class");

            var currentSegment = vessel.CurrentSegment;
            var nextSegment = vessel.GetNextSegment();

            int? currentSequenceIndex = currentSegment?.SequenceIndex;
            int nextSequenceIndex = nextSegment.SequenceIndex;

            var candidateShipments = GetCandidateShipmentsForLoading(
                assignedServiceRoute,
                nextSequenceIndex);

            int occupiedTeu = CalculateOccupiedTeuBeforeLoading(
                vessel,
                assignedServiceRoute,
                currentSequenceIndex);

            int remainingCapacity = vesselClass.TeuCapacity - occupiedTeu;

            Require(
                remainingCapacity >= 0,
                nameof(AttemptStart),
                $"Vessel {vessel.Index} exceeds TEU capacity before loading. " +
                $"Occupied TEU: {occupiedTeu}, capacity: {vesselClass.TeuCapacity}.");

            var selectedShipments = SelectShipmentsWithinCapacity(
                candidateShipments,
                remainingCapacity);

            foreach (var shipment in selectedShipments)
            {
                if (!vessel.CarriedShipments.Contains(shipment))
                {
                    vessel.CarriedShipments.Add(shipment);
                }

                P_StartSignals.Remove(shipment);
            }

            Start(vessel);
        }
    }

    /// <summary>
    /// Finds pending shipments that can be loaded onto a vessel for its next segment.
    ///
    /// A shipment is a loading candidate when:
    /// - it is waiting in the pending shipment pool;
    /// - it is not currently carried by another vessel;
    /// - its current booking uses the vessel's assigned service route; and
    /// - its departure segment index matches the vessel's next segment index.
    /// </summary>
    private List<Shipment> GetCandidateShipmentsForLoading(
        ServiceRoute assignedServiceRoute,
        int nextSequenceIndex)
    {
        return P_StartSignals
            .Where(shipment =>
            {
                Require(
                    shipment.CarryingVessel == null,
                    nameof(AttemptStart),
                    $"Shipment {shipment.Index} already assigned to vessel {shipment.CarryingVessel?.Index}.");

                var booking = shipment.GetCurrentBooking();

                return booking.ServiceRoute == assignedServiceRoute
                    && booking.DepartureSegmentIndex == nextSequenceIndex;
            })
            .ToList();
    }

    /// <summary>
    /// Calculates the vessel's occupied TEU before loading new shipments.
    ///
    /// Shipments whose current booking belongs to another service route indicate
    /// an inconsistent model state and therefore raise an exception.
    ///
    /// If currentSequenceIndex is null, the vessel has not yet entered any segment,
    /// so no onboard shipment is excluded as dischargeable. Otherwise, shipments
    /// whose arrival segment index matches the current segment are excluded from
    /// occupied TEU because they are expected to be discharged at this berth.
    /// </summary>
    private int CalculateOccupiedTeuBeforeLoading(
        Vessel vessel,
        ServiceRoute assignedServiceRoute,
        int? currentSequenceIndex)
    {
        return vessel.CarriedShipments
            .Where(shipment =>
            {
                var booking = shipment.GetCurrentBooking();

                Require(
                    booking.ServiceRoute == assignedServiceRoute,
                    nameof(AttemptStart),
                    $"Shipment {shipment.Index} current booking service route " +
                    $"{booking.ServiceRoute?.Id} does not match vessel {vessel.Index} " +
                    $"assigned service route {assignedServiceRoute.Id}.");

                if (currentSequenceIndex == null)
                    return true;

                return booking.ArrivalSegmentIndex != currentSequenceIndex.Value;
            })
            .Sum(shipment => shipment.TeuSize);
    }

    /// <summary>
    /// Selects candidate shipments using a simple greedy loading rule.
    ///
    /// Candidates are processed in their current order. A shipment is selected
    /// if adding its TEU size does not exceed the remaining vessel capacity.
    /// </summary>
    private List<Shipment> SelectShipmentsWithinCapacity(
        List<Shipment> candidateShipments,
        int remainingCapacity)
    {
        var selectedShipments = new List<Shipment>();
        int selectedTeu = 0;

        foreach (var shipment in candidateShipments)
        {
            Require(
                shipment.TeuSize > 0,
                nameof(AttemptStart),
                $"Shipment {shipment.Index} has non-positive TEU size: {shipment.TeuSize}.");

            if (selectedTeu + shipment.TeuSize > remainingCapacity)
                continue;

            selectedShipments.Add(shipment);
            selectedTeu += shipment.TeuSize;
        }

        return selectedShipments;
    }

    /// <summary>
    /// Attempts to finish vessels based on berth-specific finish signals.
    ///
    /// For each berth signal:
    /// 1. Find a ready-to-finish vessel whose CurrentBerth matches the signalled berth.
    /// 2. Consume the berth finish signal.
    /// 3. Remove shipments that should be discharged at the vessel's current segment.
    /// 4. Remove the vessel from the current segment's vessel list, if applicable.
    /// 5. Advance the vessel to the next segment in its cyclic service route.
    /// 6. Clear the vessel's berth association.
    /// 7. Finish the vessel service activity.
    /// </summary>
    protected override void AttemptFinish()
    {
        Log(nameof(AttemptFinish));

        foreach (var berth in Q_FinishSignals.ToList())
        {
            var vessel = D_LoadsReadyFinish
                .FirstOrDefault(v => v.CurrentBerth == berth);

            if (vessel == null)
                continue;

            // Consume the finish token.
            Q_FinishSignals.Remove(berth);

            // Remove shipments that complete their current booking at this segment.
            var dischargingShipments = vessel.GetDischargingShipmentsAtCurrentSegment();

            foreach (var shipment in dischargingShipments)
            {
                vessel.CarriedShipments.Remove(shipment);
            }

            // Remove the vessel from its current segment before advancing.
            if (vessel.CurrentSegment != null)
            {
                vessel.CurrentSegment.CurrentVessels.Remove(vessel);
            }

            // Advance to the next segment. If CurrentSegment is null, this initializes
            // the vessel to the first segment of its assigned service route.
            var nextSegment = vessel.GetNextSegment();
            vessel.CurrentSegment = nextSegment;
            nextSegment.CurrentVessels.Add(vessel);

            // Release berth association.
            vessel.CurrentBerth = null;

            Finish(vessel);
        }
    }
}