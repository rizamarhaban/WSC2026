namespace SimulationChallenge2026;

public class Vessel_Generator : Generator<Vessel>
{
    public MaritimeDataContext MaritimeDataContext { get; }

    public Vessel_Generator(
        MaritimeDataContext maritimeDataContext,
        int seed = 0) : base(id: nameof(Vessel_Generator), seed: seed)
    {
        MaritimeDataContext = maritimeDataContext ?? throw new ArgumentNullException(nameof(maritimeDataContext));
        foreach (var vessel in MaritimeDataContext.Vessels)
            Schedule(() => Arrive(vessel), TimeSpan.Zero);
    }
}