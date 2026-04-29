using MathNet.Numerics.Distributions;

namespace SimulationChallenge2026;

public class Shipment_Generator : Generator<Shipment>
{
    public MaritimeDataContext MaritimeDataContext { get; }

    private int _nextShipmentIndex = 1;

    /// <summary>
    /// Mean number of shipments generated per demand per day.
    /// Default = 1 shipment/day.
    /// </summary>
    public double MeanShipmentsPerDay { get; }
   
    public Shipment_Generator(
        MaritimeDataContext maritimeDataContext,
        double meanShipmentsPerDay = 10,
        int seed = 0) : base(id: nameof(Shipment_Generator), seed: seed)
    {
        MaritimeDataContext = maritimeDataContext ?? throw new ArgumentNullException(nameof(maritimeDataContext));

        if (meanShipmentsPerDay <= 0)
            throw new ArgumentOutOfRangeException(nameof(meanShipmentsPerDay), "Mean shipments per day must be positive.");

        MeanShipmentsPerDay = meanShipmentsPerDay;

        foreach (var demand in MaritimeDataContext.Demands)
        {
            Schedule(() => Generate(demand), NextInterarrivalTime());
        }
    }

    private void Generate(Demand demand)
    {
        if (demand == null) throw new ArgumentNullException(nameof(demand));

        double expectedTeuPerShipment = demand.AnnualTEUs / 365.0 / MeanShipmentsPerDay;

        int teuSize = SampleShipmentTeuSize(expectedTeuPerShipment);

        var shipment = new Shipment
        {
            Index = _nextShipmentIndex++,
            TeuSize = teuSize,
            Demand = demand,
            CurrentStoragePort = demand.OriginPort
        };

        demand.Shipments.Add(shipment);
        demand.OriginPort.ShipmentsInStorage.Add(shipment);

        Schedule(() => Generate(demand), NextInterarrivalTime());

        Arrive(shipment);
    }

    private TimeSpan NextInterarrivalTime()
    {
        // Exponential.Sample(random, rate), where mean = 1 / rate
        double days = Exponential.Sample(DefaultRS, MeanShipmentsPerDay);
        return TimeSpan.FromDays(days);
    }

    private int SampleShipmentTeuSize(double expectedTeuPerShipment)
    {
        if (expectedTeuPerShipment <= 0)
            return 1;

        int sampled = Poisson.Sample(DefaultRS, expectedTeuPerShipment);

        // Avoid zero-size shipments
        return Math.Max(1, sampled);
    }
}
