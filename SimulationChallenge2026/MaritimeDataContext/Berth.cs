namespace SimulationChallenge2026;

public class Berth
{
    public int Index { get; set; } 

    public Port Port { get; set; } = null!;
            
    public Vessel? OccupyingVessel { get; set; }

    public override string ToString() => $"{Port.Name}-Berth-{Index}";
}
