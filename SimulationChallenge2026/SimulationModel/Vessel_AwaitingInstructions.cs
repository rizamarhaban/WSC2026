namespace SimulationChallenge2026;

/// <summary>
/// Represents vessels awaiting departure instructions on their assigned service routes.
///
/// This activity controls when vessels are released based on a predefined service schedule.
///
/// Key design:
/// - This activity does not modify vessel location or route state.
/// - It only controls the timing of vessel release.
///
/// Mechanism:
/// - Each service route maintains a release schedule.
/// - The first vessel is released based on StartDayOfWeek.
/// - Subsequent vessels are released periodically using a fixed headway.
///
/// Synchronization:
/// - Finish is controlled via route-based finish signals.
/// - Each signal allows exactly one vessel to be released.
/// </summary>
public class Vessel_AwaitingInstructions : ActivityHandler<Vessel>
{
    /// <summary>
    /// Tracks whether the initial release has been scheduled for each route.
    /// Ensures the first vessel follows the configured start day.
    /// </summary>
    private readonly HashSet<ServiceRoute> _initializedRoutes = new();

    /// <summary>
    /// Finish signal tokens keyed by service route.
    /// Each token allows one vessel on that route to be released.
    /// </summary>
    private readonly HashSet<ServiceRoute> Q_FinishSignals = new();

    /// <summary>
    /// Fixed interval between consecutive vessel releases on the same route.
    /// </summary>
    private readonly TimeSpan _headway = TimeSpan.FromDays(7);

    public Vessel_AwaitingInstructions(int seed = 0)
        : base(
              id: nameof(Vessel_AwaitingInstructions),
              seed: seed)
    {
    }

    /// <summary>
    /// Attempts to release vessels based on route-specific finish signals.
    ///
    /// Logic:
    /// 1. For each vessel ready to finish:
    ///    - If this is the first vessel of its route, schedule the first release
    ///      using StartDayOfWeek.
    ///    - Otherwise, release only if a finish signal token exists for that route.
    ///
    /// 2. Upon release:
    ///    - Do not modify CurrentSegment.
    ///    - Do not assign the vessel to route segments.
    ///    - Simply call Finish(vessel).
    ///
    /// 3. After releasing a vessel:
    ///    - Schedule the next release using the fixed headway.
    ///
    /// This activity only controls timing, not spatial or route state.
    /// </summary>
    protected override void AttemptFinish()
    {
        Log(nameof(AttemptFinish));

        foreach (var vessel in D_LoadsReadyFinish.ToList())
        {
            var route = RequireNotNull(
                vessel.AssignedServiceRoute,
                nameof(AttemptFinish),
                $"Vessel {vessel.Index} assigned service route");

            // First vessel of this route: schedule initial release.
            if (!_initializedRoutes.Contains(route))
            {
                _initializedRoutes.Add(route);

                var delay = ComputeInitialDelay(route);

                Schedule(() => SignalFinish(route), delay);

                continue;
            }

            // Subsequent vessels: require one finish signal token.
            if (Q_FinishSignals.Contains(route))
            {
                Q_FinishSignals.Remove(route);

                // IMPORTANT:
                // Do NOT modify vessel route or location state here.
                // Vessel is assumed to already be positioned correctly
                // (e.g., at the first port during initialization).

                Finish(vessel);

                // Schedule next release for this route.
                Schedule(() => SignalFinish(route), _headway);
            }
        }
    }

    /// <summary>
    /// Computes delay until the next occurrence of route.StartDayOfWeek.
    ///
    /// Convention:
    /// - StartDayOfWeek is expressed in [0, 7), where 0 = Monday 00:00.
    /// </summary>
    private TimeSpan ComputeInitialDelay(ServiceRoute route)
    {
        double startDay = route.StartDayOfWeek;

        double nowDay =
            ((int)ClockTime.DayOfWeek + 6) % 7 +
            ClockTime.TimeOfDay.TotalDays;

        double delayDays = startDay - nowDay;

        if (delayDays < 0)
        {
            delayDays += 7.0;
        }

        return TimeSpan.FromDays(delayDays);
    }

    /// <summary>
    /// Issues a finish signal for a route.
    ///
    /// This adds a release token for the route and triggers AttemptFinish.
    /// Each token allows exactly one vessel to be released.
    /// </summary>
    private void SignalFinish(ServiceRoute route)
    {
        Log(nameof(SignalFinish), route);

        if (Q_FinishSignals.Add(route))
        {
            Schedule(AttemptFinish);
        }
    }
}