namespace SimulationChallenge2026;

public class VesselClass
{
    public string Name { get; set; } = string.Empty;
    public int TeuCapacity { get; set; }
    public double SailingSpeed { get; set; }

    /// <summary>
    /// Length Overall (meters)
    /// </summary>
    public double LOA { get; set; }

    public List<Vessel> Vessels { get; } = new();

    public override string ToString()
    {
        return $"{Name} | {TeuCapacity} TEU | {LOA} m";
    }
}
