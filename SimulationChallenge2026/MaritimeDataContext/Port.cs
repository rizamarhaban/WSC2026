namespace SimulationChallenge2026;

public class Port
{
    public string Name { get; set; } = string.Empty;

    public List<Berth> Berths { get; set; } = new();
    public List<Leg> OutgoingLegs { get; set; } = new();
    public List<Leg> IncomingLegs { get; set; } = new();

    public List<Demand> OutgoingDemands { get; set; } = new();
    public List<Demand> IncomingDemands { get; set; } = new();
    public List<Shipment> ShipmentsInStorage { get; set; } = new();

    public override string ToString() => Name;
}
