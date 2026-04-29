namespace SimulationChallenge2026;

public class Booking
{
    public int SequenceIndex { get; set; }
    public ServiceRoute ServiceRoute { get; set; } = null!;
    public Shipment Shipment { get; set; } = null!;
    public int DepartureSegmentIndex { get; set; }
    public int ArrivalSegmentIndex { get; set; }
}
