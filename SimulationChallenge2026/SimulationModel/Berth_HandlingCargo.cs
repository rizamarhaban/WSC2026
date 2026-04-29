namespace SimulationChallenge2026;

/// <summary>
/// Cargo handling activity at berth.
/// 
/// This activity models the loading and unloading operations performed
/// by quay cranes at a berth.
/// 
/// Key characteristics:
/// - Start requires an external vessel signal.
/// - Start matches vessel to its current berth.
/// - Duration depends on vessel workload at the current port.
/// </summary>
public class Berth_HandlingCargo : ActivityHandler<Berth>
{
    /// <summary>
    /// Vessel start signals waiting to trigger cargo handling at berth.
    /// </summary>
    public HashSet<Vessel> P_StartSignals { get; } = new();

    public Berth_HandlingCargo(int seed = 0)
        : base(id: nameof(Berth_HandlingCargo), seed: seed)
    {
        // Duration is defined based on berth state.
        T_Duration = (berth, rs) => GetDuration(berth, rs);
    }

    /// <summary>
    /// Computes cargo handling duration based on total loading/discharging TEU
    /// and quay crane productivity.
    /// </summary>
    private TimeSpan GetDuration(Berth berth, Random rs)
    {
        var vessel = RequireNotNull(
            berth.OccupyingVessel,
            nameof(GetDuration),
            $"Berth {berth} occupying vessel");

        var vesselClass = RequireNotNull(
            vessel.VesselClass,
            nameof(GetDuration),
            $"Vessel {vessel.Index} vessel class");

        const double singleQcProductivityTeuPerHour = 30.0;
        const double shorelinePerQcMeter = 100.0;

        int qcCount = (int)Math.Floor(vesselClass.LOA / shorelinePerQcMeter);
        qcCount = Math.Max(1, qcCount);

        int dischargingTeu = vessel.GetDischargingShipmentsAtCurrentSegment()
            .Sum(shipment => shipment.TeuSize);

        int loadingTeu = vessel.GetLoadingShipmentsAtNextSegment()
            .Sum(shipment => shipment.TeuSize);

        int totalTeuToHandle = dischargingTeu + loadingTeu;

        double totalProductivity = qcCount * singleQcProductivityTeuPerHour;
        double hours = totalTeuToHandle / totalProductivity;

        return TimeSpan.FromHours(hours);
    }

    /// <summary>
    /// Signals that a vessel is ready for cargo handling.
    /// </summary>
    public void SignalStart(Vessel vessel)
    {
        if (vessel == null)
            throw new ArgumentNullException(nameof(vessel));

        Log("SignalStart", vessel);

        RequireNotNull(
            vessel.CurrentBerth,
            nameof(SignalStart),
            $"Vessel {vessel.Index} current berth");

        if (P_StartSignals.Add(vessel))
        {
            Schedule(AttemptStart);
        }
    }

    /// <summary>
    /// Matches vessel start signals with requested berth loads and starts cargo handling.
    /// </summary>
    protected override void AttemptStart()
    {
        Log("AttemptStart");

        if (R_LoadsRequestedStart.Count == 0 || P_StartSignals.Count == 0)
            return;

        foreach (var vessel in P_StartSignals.ToList())
        {
            var berth = RequireNotNull(
                vessel.CurrentBerth,
                nameof(AttemptStart),
                $"Vessel {vessel.Index} current berth");

            if (!R_LoadsRequestedStart.Contains(berth))
                continue;

            berth.OccupyingVessel = vessel;

            P_StartSignals.Remove(vessel);

            Start(berth);
        }
    }
}