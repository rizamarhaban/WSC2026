namespace SimulationChallenge2026;

public class Berth_Generator : Generator<Berth>
{
    public MaritimeDataContext MaritimeDataContext { get; }

    public Berth_Generator(
        MaritimeDataContext maritimeDataContext,
        int seed = 0) : base(id: nameof(Berth_Generator), seed: seed)
    {
        MaritimeDataContext = maritimeDataContext
            ?? throw new ArgumentNullException(nameof(maritimeDataContext));

        foreach (var berth in MaritimeDataContext.Ports.SelectMany(p => p.Berths))
        {
            Schedule(() => Arrive(berth), TimeSpan.Zero);
        }
    }
}