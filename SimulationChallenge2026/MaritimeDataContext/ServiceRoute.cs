namespace SimulationChallenge2026;

/// <summary>
/// Represents a cyclic liner shipping service route.
///
/// A service route defines an ordered sequence of route segments. Each
/// segment refers to a physical port-to-port leg, while the sequence of
/// segments defines the operational rotation followed by deployed vessels.
///
/// Bookings associated with this route specify how shipments use this
/// service route, including their departure and arrival segment indices.
/// </summary>
public class ServiceRoute
{
    /// <summary>
    /// Unique identifier of the service route.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the service route.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Initial schedule offset of the service route, expressed as a day of week
    /// in the range [0, 7).
    ///
    /// This value is used to phase the initial release of vessels in the simulation.
    /// </summary>
    public double StartDayOfWeek { get; set; }

    /// <summary>
    /// Ordered sequence of segments forming the cyclic service route.
    ///
    /// Each segment belongs to this service route and has a sequence index that
    /// defines its order in the route rotation.
    /// </summary>
    public List<Segment> Segments { get; } = new();

    /// <summary>
    /// Vessels deployed to operate this service route.
    /// </summary>
    public List<Vessel> DeployedVessels { get; } = new();

    /// <summary>
    /// Bookings assigned to this service route.
    ///
    /// A booking links a shipment to this route and records the segment indices
    /// where the shipment should be loaded and discharged.
    /// </summary>
    public List<Booking> AssociatedBookings { get; } = new();

    public override string ToString()
    {
        return $"{Id} | StartDay={StartDayOfWeek:F2}";
    }
}