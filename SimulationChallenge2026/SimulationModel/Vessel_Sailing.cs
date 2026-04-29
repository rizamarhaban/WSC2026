using O2DESNet;

namespace SimulationChallenge2026;

/// <summary>
/// Represents the sailing activity of a vessel along its current segment.
///
/// This is a duration-driven activity:
/// - Vessels start immediately once the sailing activity is requested.
/// - Vessels finish automatically once the sailing duration elapses.
/// - No additional start or finish gate is required.
///
/// The sailing duration is calculated dynamically from:
/// - the sailing distance of the segment's associated leg; and
/// - the sailing speed of the vessel's class.
///
/// A small random variation is applied to represent stochastic sailing time.
/// </summary>
public class Vessel_Sailing : ActivityHandler<Vessel>
{
    public Vessel_Sailing(int seed = 0)
        : base(
              id: nameof(Vessel_Sailing),
              seed: seed)
    {
        // Use a vessel-specific duration function.
        // The random stream rs is provided by the activity framework.
        T_Duration = (vessel, rs) => GetDuration(vessel, rs);
    }

    /// <summary>
    /// Computes the sailing duration for the given vessel.
    ///
    /// The vessel is assumed to sail along its CurrentSegment.
    /// If CurrentSegment is null, the vessel is not assigned to any sailing segment,
    /// so the duration is zero.
    /// </summary>
    private TimeSpan GetDuration(Vessel vessel, Random rs)
    {
        var currentSegment = vessel.CurrentSegment;

        // If the vessel has no current segment, no sailing is required.
        // This may occur before the vessel enters its first segment or during
        // an immediate transition state.
        if (currentSegment == null)
        {
            return TimeSpan.Zero;
        }

        var leg = RequireNotNull(
            currentSegment.AssociatedLeg,
            nameof(GetDuration),
            $"Vessel {vessel.Index} current segment {currentSegment.SequenceIndex} associated leg");

        var vesselClass = RequireNotNull(
            vessel.VesselClass,
            nameof(GetDuration),
            $"Vessel {vessel.Index} vessel class");

        double speed = vesselClass.SailingSpeed;

        Require(
            speed > 0,
            nameof(GetDuration),
            $"Vessel {vessel.Index} has non-positive sailing speed: {speed}.");

        double hours = leg.SailingDistance / speed;

        // Apply a small stochastic variation to sailing duration.
        // The factor ranges from 0.95 to 1.05.
        double variationFactor = 0.95 + 0.10 * rs.NextDouble();
        hours *= variationFactor;

        return TimeSpan.FromHours(hours);
    }

    #region Statistics - TEU Capacity and Carried TEUs by Service Route

    /// <summary>
    /// Time-weighted TEU capacity counters grouped by assigned service route.
    ///
    /// Each counter tracks the total TEU capacity of vessels currently sailing
    /// on the corresponding service route.
    ///
    /// The counter increases when a vessel starts sailing and decreases when
    /// the vessel departs from this activity.
    /// </summary>
    public Dictionary<ServiceRoute, HourCounter> HC_TeuCapacityByServiceRoute { get; } = new();

    /// <summary>
    /// Time-weighted carried TEU counters grouped by assigned service route.
    ///
    /// Each counter tracks the total carried shipment TEU volume of vessels
    /// currently sailing on the corresponding service route.
    ///
    /// The counter increases when a vessel starts sailing and decreases when
    /// the vessel departs from this activity.
    /// </summary>
    public Dictionary<ServiceRoute, HourCounter> HC_CarriedTeusByServiceRoute { get; } = new();

    protected override void Start(Vessel vessel)
    {
        base.Start(vessel);

        var serviceRoute = RequireNotNull(
            vessel.AssignedServiceRoute,
            nameof(Start),
            $"Assigned service route of Vessel {vessel.Index}");

        var vesselClass = RequireNotNull(
            vessel.VesselClass,
            nameof(Start),
            $"Vessel class of Vessel {vessel.Index}");

        int teuCapacity = vesselClass.TeuCapacity;
        int carriedTeus = vessel.CarriedShipments.Sum(shipment => shipment.TeuSize);

        var capacityCounter = GetOrCreateServiceRouteCounter(
            HC_TeuCapacityByServiceRoute,
            serviceRoute);

        var carriedTeusCounter = GetOrCreateServiceRouteCounter(
            HC_CarriedTeusByServiceRoute,
            serviceRoute);

        capacityCounter.ObserveChange(teuCapacity);
        carriedTeusCounter.ObserveChange(carriedTeus);
    }

    public override void Depart(Vessel vessel)
    {
        if (F_LoadsFinished.Contains(vessel))
        {
            var serviceRoute = RequireNotNull(
                vessel.AssignedServiceRoute,
                nameof(Depart),
                $"Assigned service route of Vessel {vessel.Index}");

            var vesselClass = RequireNotNull(
                vessel.VesselClass,
                nameof(Depart),
                $"Vessel class of Vessel {vessel.Index}");

            int teuCapacity = vesselClass.TeuCapacity;
            int carriedTeus = vessel.CarriedShipments.Sum(shipment => shipment.TeuSize);

            base.Depart(vessel);

            if (!HC_TeuCapacityByServiceRoute.TryGetValue(serviceRoute, out var capacityCounter))
            {
                ThrowActivityException(
                    nameof(Depart),
                    $"Cannot update sailing TEU capacity statistics for Vessel {vessel.Index} " +
                    $"because no HourCounter exists for service route {serviceRoute.Id}. " +
                    "This indicates that the vessel may not have started this activity before departure.");
            }

            if (!HC_CarriedTeusByServiceRoute.TryGetValue(serviceRoute, out var carriedTeusCounter))
            {
                ThrowActivityException(
                    nameof(Depart),
                    $"Cannot update sailing carried TEU statistics for Vessel {vessel.Index} " +
                    $"because no HourCounter exists for service route {serviceRoute.Id}. " +
                    "This indicates that the vessel may not have started this activity before departure.");
            }

            capacityCounter.ObserveChange(-teuCapacity);
            carriedTeusCounter.ObserveChange(-carriedTeus);
        }
    }

    /// <summary>
    /// Gets an existing service-route counter or creates a new one if it does not exist.
    /// </summary>
    private HourCounter GetOrCreateServiceRouteCounter(
        Dictionary<ServiceRoute, HourCounter> counters,
        ServiceRoute serviceRoute)
    {
        if (!counters.TryGetValue(serviceRoute, out var counter))
        {
            counter = AddHourCounter();
            counters[serviceRoute] = counter;
        }

        return counter;
    }

    #endregion
}