using O2DESNet;

namespace SimulationChallenge2026;

/// <summary>
/// Represents vessels waiting in queue for berth assignment.
///
/// Start side:
/// - No gating. Vessels enter the queue immediately.
///
/// Finish side:
/// - Controlled by berth signals.
/// - Each berth signal releases exactly one vessel, identified by berth.OccupyingVessel.
/// </summary>
public class Vessel_QueuingForBerth : ActivityHandler<Vessel>
{
    /// <summary>
    /// Finish signal tokens keyed by berth.
    /// Each token allows one vessel assigned to that berth to leave the queue.
    /// </summary>
    private readonly HashSet<Berth> Q_FinishSignals = new();

    public Vessel_QueuingForBerth(int seed = 0)
        : base(
              id: nameof(Vessel_QueuingForBerth),
              seed: seed)
    {
    }

    /// <summary>
    /// External signal indicating that a berth is ready to admit its occupying vessel.
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
    /// Attempts to release queued vessels based on berth-specific finish signals.
    ///
    /// For each signalled berth:
    /// 1. Identify the occupying vessel from the berth.
    /// 2. Check whether that vessel is ready to finish this activity.
    /// 3. Assign the berth to vessel.CurrentBerth.
    /// 4. Finish the vessel.
    /// </summary>
    protected override void AttemptFinish()
    {
        Log(nameof(AttemptFinish));

        foreach (var berth in Q_FinishSignals.ToList())
        {
            var vessel = RequireNotNull(
                berth.OccupyingVessel,
                nameof(AttemptFinish),
                $"Berth {berth.Index} occupying vessel");

            if (!D_LoadsReadyFinish.Contains(vessel))
                continue;

            Q_FinishSignals.Remove(berth);

            // Assign the berth to the vessel before it leaves the queue.
            vessel.CurrentBerth = berth;

            Finish(vessel);
        }
    }

    #region Statistics - Number of Vessels by Port

    /// <summary>
    /// Time-weighted vessel counters grouped by port.
    ///
    /// Each counter tracks the number of vessels currently staying in this activity
    /// and waiting for berth assignment at the corresponding arrival port.
    ///
    /// The port is identified from:
    /// vessel.CurrentSegment.AssociatedLeg.ArrivalPort
    ///
    /// The counter increases when a vessel starts this activity and decreases
    /// when the vessel departs from this activity.
    /// </summary>
    public Dictionary<Port, HourCounter> HC_NumberOfVesselsByPort { get; } = new();

    protected override void Start(Vessel vessel)
    {
        base.Start(vessel);

        var port = GetArrivalPortForStatistics(vessel, nameof(Start));

        if (!HC_NumberOfVesselsByPort.TryGetValue(port, out var portCounter))
        {
            portCounter = AddHourCounter();
            HC_NumberOfVesselsByPort[port] = portCounter;
        }

        portCounter.ObserveChange(1);
    }

    public override void Depart(Vessel vessel)
    {
        if (F_LoadsFinished.Contains(vessel))
        {
            base.Depart(vessel);

            var port = GetArrivalPortForStatistics(vessel, nameof(Depart));

            if (!HC_NumberOfVesselsByPort.TryGetValue(port, out var portCounter))
            {
                ThrowActivityException(
                    nameof(Depart),
                    $"Cannot update vessel queue statistics for Vessel {vessel.Index} because no HourCounter exists for port {port}. " +
                    "This indicates that the vessel may not have started this activity before departure.");
            }

            portCounter.ObserveChange(-1);
        }
    }

    /// <summary>
    /// Gets the arrival port used for vessel queue statistics.
    ///
    /// The port is derived from the vessel's current segment:
    /// vessel.CurrentSegment.AssociatedLeg.ArrivalPort.
    /// </summary>
    private Port GetArrivalPortForStatistics(Vessel vessel, string eventName)
    {
        if (vessel.CurrentSegment == null)
        {
            var assignedServiceRoute = RequireNotNull(
                vessel.AssignedServiceRoute,
                eventName,
                $"Assigned service route of Vessel {vessel.Index}");

            var firstSegment = assignedServiceRoute.Segments
                .OrderBy(segment => segment.SequenceIndex)
                .FirstOrDefault();

            firstSegment = RequireNotNull(
                firstSegment,
                eventName,
                $"First segment of assigned service route {assignedServiceRoute.Id} for Vessel {vessel.Index}");

            var firstLeg = RequireNotNull(
                firstSegment.AssociatedLeg,
                eventName,
                $"Associated leg of first segment {firstSegment.SequenceIndex} for Vessel {vessel.Index}");

            var departurePort = RequireNotNull(
                firstLeg.DeparturePort,
                eventName,
                $"Departure port of first segment {firstSegment.SequenceIndex} for Vessel {vessel.Index}");

            return departurePort;
        }

        var currentSegment = vessel.CurrentSegment;

        var associatedLeg = RequireNotNull(
            currentSegment.AssociatedLeg,
            eventName,
            $"Associated leg of Vessel {vessel.Index} current segment {currentSegment.SequenceIndex}");

        var arrivalPort = RequireNotNull(
            associatedLeg.ArrivalPort,
            eventName,
            $"Arrival port of Vessel {vessel.Index} current segment {currentSegment.SequenceIndex}");

        return arrivalPort;
    }

    #endregion
}