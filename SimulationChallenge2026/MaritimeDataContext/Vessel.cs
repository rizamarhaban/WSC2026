namespace SimulationChallenge2026;

/// <summary>
/// Represents a vessel operating on an assigned liner service route.
///
/// A vessel may be at a berth, carry shipments, and move through the ordered
/// segments of its assigned service route. The current segment represents the
/// segment that the vessel has most recently completed or is currently associated with.
///
/// Loading is matched against the next segment because shipments loaded during
/// berth service will travel on the vessel's next sailing segment. Discharging is
/// matched against the current segment because those shipments have arrived through
/// the vessel's current segment.
/// </summary>
public class Vessel
{
    /// <summary>
    /// Unique index of the vessel within the scenario.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Vessel class defining capacity, speed, and physical characteristics.
    /// </summary>
    public VesselClass VesselClass { get; set; } = null!;

    /// <summary>
    /// Berth currently occupied by the vessel.
    ///
    /// Null means the vessel is not currently at berth.
    /// </summary>
    public Berth? CurrentBerth { get; set; }

    /// <summary>
    /// Shipments currently carried onboard this vessel.
    /// </summary>
    public List<Shipment> CarriedShipments { get; } = new();

    /// <summary>
    /// Service route assigned to this vessel.
    /// </summary>
    public ServiceRoute? AssignedServiceRoute { get; set; }

    /// <summary>
    /// Current segment associated with the vessel.
    ///
    /// This may be null before the vessel enters its first route segment.
    /// </summary>
    public Segment? CurrentSegment { get; set; }

    public override string ToString() =>
        $"Vessel-{Index} [{VesselClass.Name}]";

    /// <summary>
    /// Retrieves carried shipments whose current booking belongs to the vessel's
    /// assigned service route and whose selected booking segment index matches
    /// the specified segment.
    /// </summary>
    /// <param name="segment">
    /// The route segment used for matching.
    /// </param>
    /// <param name="segmentIndexSelector">
    /// Selects the booking segment index to compare with the segment sequence index.
    /// Use DepartureSegmentIndex for loading and ArrivalSegmentIndex for discharging.
    /// </param>
    /// <returns>
    /// Shipments matching the specified segment.
    /// </returns>
    private HashSet<Shipment> GetShipmentsAtSegment(
        Segment segment,
        Func<Booking, int> segmentIndexSelector)
    {
        var assignedServiceRoute = AssignedServiceRoute
            ?? throw new InvalidOperationException(
                $"Vessel {Index} has no assigned service route.");

        if (segment == null)
            throw new ArgumentNullException(nameof(segment));

        return CarriedShipments
            .Where(shipment =>
            {
                var booking = shipment.GetCurrentBooking();

                return booking.ServiceRoute == assignedServiceRoute
                    && segmentIndexSelector(booking) == segment.SequenceIndex;
            })
            .ToHashSet();
    }

    /// <summary>
    /// Retrieves shipments that should be loaded for the vessel's next segment.
    ///
    /// Loading uses the next segment because shipments loaded while the vessel
    /// is being served at berth will depart on the next sailing movement.
    /// </summary>
    public HashSet<Shipment> GetLoadingShipmentsAtNextSegment()
    {
        var nextSegment = GetNextSegment();

        return GetShipmentsAtSegment(
            nextSegment,
            booking => booking.DepartureSegmentIndex);
    }

    /// <summary>
    /// Retrieves shipments that should be discharged at the vessel's current segment.
    ///
    /// Discharging uses the current segment because these shipments have completed
    /// the sailing movement represented by the current segment.
    ///
    /// If CurrentSegment is null, the vessel has not yet entered any segment, so
    /// there are no shipments to discharge.
    /// </summary>
    public HashSet<Shipment> GetDischargingShipmentsAtCurrentSegment()
    {
        if (CurrentSegment == null)
        {
            return new HashSet<Shipment>();
        }

        return GetShipmentsAtSegment(
            CurrentSegment,
            booking => booking.ArrivalSegmentIndex);
    }

    /// <summary>
    /// Retrieves the next segment in the vessel's assigned service route.
    ///
    /// Rules:
    /// - If CurrentSegment is null, return the first segment in the route.
    /// - Otherwise, return the next segment by sequence index.
    /// - If the current segment is the last segment, wrap around to the first
    ///   segment, forming a cyclic service route.
    /// </summary>
    /// <returns>
    /// The next segment in the cyclic service route.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the vessel has no assigned service route, or the route has no segments.
    /// </exception>
    public Segment GetNextSegment()
    {
        var assignedServiceRoute = AssignedServiceRoute
            ?? throw new InvalidOperationException(
                $"Vessel {Index} has no assigned service route.");

        var segments = assignedServiceRoute.Segments;

        if (segments.Count == 0)
        {
            throw new InvalidOperationException(
                $"Service route {assignedServiceRoute.Id} has no segments.");
        }

        var firstSegment = segments
            .OrderBy(segment => segment.SequenceIndex)
            .First();

        if (CurrentSegment == null)
        {
            return firstSegment;
        }

        var nextSegment = segments
            .Where(segment => segment.SequenceIndex > CurrentSegment.SequenceIndex)
            .OrderBy(segment => segment.SequenceIndex)
            .FirstOrDefault();

        return nextSegment ?? firstSegment;
    }
}