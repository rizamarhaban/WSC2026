namespace SimulationChallenge2026;

/// <summary>
/// Represents the idle activity of berths.
///
/// A berth enters the idle state once its start is requested by the activity framework.
/// It leaves the idle state only when an arriving vessel can be matched to the berth's port.
///
/// Matching rule:
/// - If the vessel has no current segment, it is treated as being at the departure
///   port of the first segment in its assigned service route.
/// - Otherwise, it is treated as arriving at the arrival port of its current segment.
/// </summary>
public class Berth_Idle : ActivityHandler<Berth>
{
    /// <summary>
    /// Vessel finish signals waiting to seize an idle berth.
    ///
    /// Each vessel signal represents a vessel that is ready to occupy a berth
    /// at its current arrival port.
    /// </summary>
    public HashSet<Vessel> Q_FinishSignals { get; } = new();

    public Berth_Idle(int seed = 0)
        : base(id: nameof(Berth_Idle), seed: seed)
    {
    }

    /// <summary>
    /// Signals that a vessel is ready to occupy a berth.
    ///
    /// The vessel is not assigned immediately. Instead, the signal is stored
    /// and AttemptFinish is scheduled, where the vessel is matched with an
    /// available idle berth at the correct port.
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
    /// Attempts to match signalled vessels with idle berths.
    ///
    /// For each vessel signal:
    /// 1. Determine the port where the vessel should occupy a berth.
    /// 2. Find an idle berth at that port.
    /// 3. Assign the vessel and berth to each other.
    /// 4. Finish the berth's idle activity.
    ///
    /// If no matching berth is currently idle, the vessel signal remains pending.
    /// </summary>
    protected override void AttemptFinish()
    {
        Log("AttemptFinish");

        if (D_LoadsReadyFinish.Count == 0 || Q_FinishSignals.Count == 0)
            return;

        foreach (var vessel in Q_FinishSignals.ToList())
        {
            var arrivalPort = GetArrivalPortOrThrow(vessel);

            var berth = D_LoadsReadyFinish
                .FirstOrDefault(b => b.Port == arrivalPort);

            if (berth == null)
                continue;

            Q_FinishSignals.Remove(vessel);

            berth.OccupyingVessel = vessel;
            vessel.CurrentBerth = berth;

            Finish(berth);
        }
    }

    /// <summary>
    /// Determines the port where the vessel should occupy a berth.
    ///
    /// Interpretation:
    /// - If CurrentSegment is null, the vessel has not yet entered the route cycle.
    ///   It is therefore placed at the departure port of the first segment of its
    ///   assigned service route.
    /// - If CurrentSegment is not null, the vessel has completed or is associated
    ///   with that segment, and should berth at that segment's arrival port.
    /// </summary>
    private Port GetArrivalPortOrThrow(Vessel vessel)
    {
        if (vessel.CurrentSegment == null)
        {
            var assignedServiceRoute = RequireNotNull(
                vessel.AssignedServiceRoute,
                nameof(AttemptFinish),
                $"Vessel {vessel.Index} assigned service route");

            var firstSegment = assignedServiceRoute.Segments
                .OrderBy(segment => segment.SequenceIndex)
                .FirstOrDefault();

            if (firstSegment == null)
            {
                ThrowActivityException(
                    nameof(AttemptFinish),
                    $"Vessel {vessel.Index} has no segments in assigned service route {assignedServiceRoute.Id}.");
            }

            var firstLeg = RequireNotNull(
                firstSegment!.AssociatedLeg,
                nameof(AttemptFinish),
                $"First segment {firstSegment.SequenceIndex} of vessel {vessel.Index} associated leg");

            var departurePort = RequireNotNull(
                firstLeg.DeparturePort,
                nameof(AttemptFinish),
                $"First segment {firstSegment.SequenceIndex} of vessel {vessel.Index} departure port");

            return departurePort;
        }

        var currentSegment = vessel.CurrentSegment;

        var currentLeg = RequireNotNull(
            currentSegment.AssociatedLeg,
            nameof(AttemptFinish),
            $"Current segment {currentSegment.SequenceIndex} of vessel {vessel.Index} associated leg");

        var arrivalPort = RequireNotNull(
            currentLeg.ArrivalPort,
            nameof(AttemptFinish),
            $"Current segment {currentSegment.SequenceIndex} of vessel {vessel.Index} arrival port");

        return arrivalPort;
    }
}