namespace SimulationChallenge2026;

public class Demand
{
    public Port OriginPort { get; set; } = null!;
    public Port DestinationPort { get; set; } = null!;
    public int AnnualTEUs { get; set; }

    public List<Shipment> Shipments { get; } = new();

    public override string ToString() =>
        $"{OriginPort.Name} -> {DestinationPort.Name}: {AnnualTEUs}";
}
