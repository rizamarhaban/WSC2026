namespace SimulationChallenge2026;

/// <summary>
/// Stores the maritime domain data used by the simulation scenario.
///
/// This context is mainly used as an in-memory data container. It holds all
/// static entities created during scenario initialization, including ports,
/// demands, service routes, physical legs, route segments, vessel classes,
/// and vessels.
/// </summary>
public class MaritimeDataContext
{
    /// <summary>
    /// All ports included in the scenario.
    /// </summary>
    public List<Port> Ports { get; } = new();

    /// <summary>
    /// Origin-destination container demands between ports.
    /// </summary>
    public List<Demand> Demands { get; } = new();

    /// <summary>
    /// Liner service routes operated in the scenario.
    /// </summary>
    public List<ServiceRoute> ServiceRoutes { get; } = new();

    /// <summary>
    /// Physical sailing legs between two ports.
    ///
    /// A leg represents a port-to-port movement and may be reused by
    /// segments from different service routes.
    /// </summary>
    public List<Leg> Legs { get; } = new();

    /// <summary>
    /// Ordered segments belonging to service routes.
    ///
    /// TODO:
    /// This property name is kept for compatibility with earlier code.
    /// It should later be renamed from PartialServiceRoutes to Segments.
    /// </summary>
    public List<Segment> PartialServiceRoutes { get; } = new();

    /// <summary>
    /// Vessel classes, including capacity and sailing characteristics.
    /// </summary>
    public List<VesselClass> VesselClasses { get; } = new();

    /// <summary>
    /// Individual vessels deployed on service routes.
    /// </summary>
    public List<Vessel> Vessels { get; } = new();
}

/// <summary>
/// Creates the baseline maritime simulation dataset.
///
/// The initializer builds the data in the following order:
/// 1. Ports and berths
/// 2. Port-to-port demands
/// 3. Service routes, legs, and route segments
/// 4. Vessel classes
/// 5. Vessels assigned to service routes
///
/// The resulting data context can then be used by the simulation model
/// to generate shipments, deploy vessels, and execute operational logic.
/// </summary>
public static class MaritimeDataInitializer
{
    /// <summary>
    /// Creates and returns a fully initialized maritime data context.
    /// </summary>
    public static MaritimeDataContext Create()
    {
        var context = new MaritimeDataContext();

        var ports = InitializePorts(context);
        InitializeDemands(context, ports);
        InitializeServiceRoutes(context, ports);
        InitializeVesselClasses(context);
        InitializeVessels(context);

        return context;
    }

    /// <summary>
    /// Initializes the ports included in the scenario.
    ///
    /// For simplicity, each port is created with one berth. The berth is
    /// linked back to its owning port, and the port is stored in both the
    /// context list and a name-based lookup dictionary.
    /// </summary>
    /// <returns>
    /// A case-insensitive dictionary for retrieving ports by name.
    /// </returns>
    private static Dictionary<string, Port> InitializePorts(MaritimeDataContext context)
    {
        string[] portNames =
        {
            "Shanghai",
            "Singapore",
            "Shenzhen",
            "Qingdao",
            "Busan",
            "Tianjin",
            "Xiamen",
            "Kaohsiung",
            "Laem Chabang",
            "Ho Chi Minh City",
            "Jakarta",
            "Colombo",
            "Jebel Ali",
            "Rotterdam",
            "Hamburg",
            "Piraeus",
            "Los Angeles",
            "New Jersey",
            "Tanger Med",
            "Cartagena"
        };

        var portDict = new Dictionary<string, Port>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in portNames)
        {
            var port = new Port
            {
                Name = name
            };

            // Baseline assumption: each port has one berth.
            // More berths can be added later when scenario tuning is required.
            var berth = new Berth
            {
                Index = 0,
                Port = port
            };

            port.Berths.Add(berth);

            context.Ports.Add(port);
            portDict[name] = port;
        }

        return portDict;
    }

    /// <summary>
    /// Initializes annual TEU demands between all port pairs.
    ///
    /// The annualTeus matrix is indexed by origin port i and destination port j.
    /// Entries with i == j or non-positive demand are skipped.
    ///
    /// Each created demand is added to:
    /// - the global demand list in the context;
    /// - the origin port's outgoing demand list; and
    /// - the destination port's incoming demand list.
    /// </summary>
    private static void InitializeDemands(
        MaritimeDataContext context,
        Dictionary<string, Port> ports)
    {
        string[] portNames =
        {
            "Shanghai",
            "Singapore",
            "Shenzhen",
            "Qingdao",
            "Busan",
            "Tianjin",
            "Xiamen",
            "Kaohsiung",
            "Laem Chabang",
            "Ho Chi Minh City",
            "Jakarta",
            "Colombo",
            "Jebel Ali",
            "Rotterdam",
            "Hamburg",
            "Piraeus",
            "Los Angeles",
            "New Jersey",
            "Tanger Med",
            "Cartagena"
        };

        int[,] annualTeus =
        {
            { 0, 5400000, 3360000, 3569595, 4320000, 3569595, 1440000, 2880000, 1620000, 1620000, 2160000, 3960000, 6300000, 8613000, 5742000, 4785000, 23250000, 7440000, 3000000, 2160000 },
            { 5400000, 0, 5250000, 2250000, 2400000, 1350000, 2250000, 1600000, 1033308, 1033308, 2296241, 3300000, 5250000, 3915000, 2610000, 2175000, 7750000, 2325000, 1875000, 1200000 },
            { 3360000, 5250000, 0, 1400000, 4200000, 840000, 871250, 2800000, 1575000, 1575000, 2100000, 3850000, 6300000, 8221500, 5481000, 4567500, 19530000, 6510000, 2625000, 1680000 },
            { 5949324, 2250000, 1400000, 0, 1800000, 1487331, 60000, 1200000, 675000, 675000, 900000, 1650000, 2625000, 3588750, 2392500, 1993750, 9687500, 3100000, 1250000, 900000 },
            { 4320000, 2400000, 4200000, 1800000, 0, 1080000, 1800000, 6405000, 720000, 720000, 960000, 1980000, 3600000, 3915000, 2610000, 2175000, 13020000, 3720000, 1500000, 720000 },
            { 2141757, 1350000, 840000, 892399, 1080000, 0, 36000, 720000, 405000, 405000, 540000, 990000, 1575000, 2153250, 1435500, 1196250, 5812500, 1860000, 750000, 540000 },
            { 1440000, 2250000, 5262750, 600000, 1800000, 36000, 0, 1200000, 675000, 675000, 900000, 1650000, 2700000, 3523500, 2349000, 1957500, 8370000, 2790000, 1125000, 720000 },
            { 2880000, 1600000, 2800000, 1200000, 3843000, 720000, 1200000, 0, 480000, 480000, 640000, 1320000, 2400000, 2610000, 1740000, 1450000, 8680000, 2480000, 1000000, 480000 },
            { 1620000, 1722180, 1575000, 675000, 720000, 405000, 675000, 480000, 0, 309992, 688872, 990000, 1575000, 1174500, 783000, 652500, 2325000, 697500, 562500, 360000 },
            { 1620000, 1722180, 1575000, 675000, 720000, 405000, 675000, 480000, 516654, 0, 688872, 990000, 1575000, 1174500, 783000, 652500, 2325000, 697500, 562500, 360000 },
            { 2160000, 1377744, 2100000, 900000, 960000, 540000, 900000, 640000, 413323, 413323, 0, 1320000, 2100000, 1566000, 1044000, 870000, 3100000, 930000, 750000, 480000 },
            { 3420000, 2850000, 3325000, 1425000, 1710000, 855000, 1425000, 1140000, 855000, 855000, 1140000, 0, 5000000, 3600000, 2400000, 2000000, 2000000, 2000000, 2000000, 1000000 },
            { 2730000, 2275000, 2730000, 1137500, 1560000, 682500, 1170000, 1040000, 682500, 682500, 910000, 5000000, 0, 3825000, 2550000, 2125000, 3000000, 2000000, 3000000, 1000000 },
            { 4158000, 1890000, 3969000, 1732500, 1890000, 1039500, 1701000, 1260000, 567000, 567000, 756000, 3600000, 5400000, 0, 1667093, 1389244, 2250000, 3600000, 3645000, 1800000 },
            { 2772000, 1260000, 2646000, 1155000, 1260000, 693000, 1134000, 840000, 378000, 378000, 504000, 2400000, 3600000, 2778488, 0, 1543605, 1500000, 2400000, 2430000, 1200000 },
            { 2310000, 1050000, 2205000, 962500, 1050000, 577500, 945000, 700000, 315000, 315000, 420000, 2000000, 3000000, 2315407, 926163, 0, 1250000, 2000000, 2025000, 1000000 },
            { 9000000, 3000000, 7560000, 3750000, 5040000, 2250000, 3240000, 3360000, 900000, 900000, 1200000, 2000000, 3000000, 2250000, 1500000, 1250000, 0, 6000000, 1000000, 2300000 },
            { 2880000, 900000, 2520000, 1200000, 1440000, 720000, 1080000, 960000, 270000, 270000, 360000, 2000000, 2000000, 3600000, 2400000, 2000000, 6000000, 0, 1000000, 3450000 },
            { 2040000, 1275000, 1785000, 850000, 1020000, 510000, 765000, 680000, 382500, 382500, 510000, 2000000, 3000000, 2025000, 1350000, 1125000, 1000000, 1000000, 0, 1000000 },
            { 1620000, 900000, 1260000, 675000, 540000, 405000, 540000, 360000, 270000, 270000, 360000, 1000000, 1000000, 1800000, 1200000, 1000000, 1800000, 2700000, 1000000, 0 }
        };

        for (int i = 0; i < portNames.Length; i++)
        {
            for (int j = 0; j < portNames.Length; j++)
            {
                if (i == j) continue;

                int teus = annualTeus[i, j];
                if (teus <= 0) continue;

                var origin = ports[portNames[i]];
                var destination = ports[portNames[j]];

                var demand = new Demand
                {
                    OriginPort = origin,
                    DestinationPort = destination,
                    AnnualTEUs = teus
                };

                context.Demands.Add(demand);
                origin.OutgoingDemands.Add(demand);
                destination.IncomingDemands.Add(demand);
            }
        }
    }

    /// <summary>
    /// Initializes liner service routes and their ordered segments.
    ///
    /// Each route is defined by an ordered list of segment data:
    /// - sequence index;
    /// - departure port;
    /// - arrival port; and
    /// - sailing distance.
    ///
    /// For each segment, the initializer creates:
    /// - a physical Leg;
    /// - a Segment associated with that leg and service route;
    /// - bidirectional references among ports, legs, segments, and routes.
    /// </summary>
    private static void InitializeServiceRoutes(
        MaritimeDataContext context,
        Dictionary<string, Port> ports)
    {
        /// <summary>
        /// Local helper for adding one complete service route.
        ///
        /// The route is stored first, then its segment records are sorted by
        /// sequence index and added one by one.
        /// </summary>
        void AddRoute(
            string id,
            string name,
            double startDayOfWeek,
            List<(int seq, string from, string to, double distanceNm)> legsData)
        {
            var route = new ServiceRoute
            {
                Id = id,
                Name = name,
                StartDayOfWeek = startDayOfWeek,
            };

            context.ServiceRoutes.Add(route);

            foreach (var (seq, from, to, distanceNm) in legsData.OrderBy(x => x.seq))
            {
                // Physical port-to-port leg.
                var leg = new Leg
                {
                    DeparturePort = ports[from],
                    ArrivalPort = ports[to],
                    SailingDistance = distanceNm
                };

                context.Legs.Add(leg);

                ports[from].OutgoingLegs.Add(leg);
                ports[to].IncomingLegs.Add(leg);

                // Service-route segment using this physical leg.
                var segment = new Segment
                {
                    SequenceIndex = seq,
                    AssociatedLeg = leg,
                    AssociatedServiceRoute = route,
                };

                context.PartialServiceRoutes.Add(segment);

                leg.Segments.Add(segment);
                route.Segments.Add(segment);
            }
        }

        AddRoute(
            "S1",
            "Asia-Europe-NorthEurope",
            0.92,
            new()
            {
                (1, "Shanghai", "Shenzhen", 664),
                (2, "Shenzhen", "Singapore", 1417),
                (3, "Singapore", "Colombo", 1476),
                (4, "Colombo", "Jebel Ali", 1788),
                (5, "Jebel Ali", "Piraeus", 7842),
                (6, "Piraeus", "Tanger Med", 1398),
                (7, "Tanger Med", "Rotterdam", 1048),
                (8, "Rotterdam", "Hamburg", 233),
                (9, "Hamburg", "Rotterdam", 233),
                (10, "Rotterdam", "Tanger Med", 1048),
                (11, "Tanger Med", "Piraeus", 1398),
                (12, "Piraeus", "Jebel Ali", 10164),
                (13, "Jebel Ali", "Colombo", 1788),
                (14, "Colombo", "Singapore", 1476),
                (15, "Singapore", "Shanghai", 2075)
            });

        AddRoute(
            "S2",
            "Intra-EastAsia",
            1.88,
            new()
            {
                (1, "Tianjin", "Qingdao", 213),
                (2, "Qingdao", "Shanghai", 294),
                (3, "Shanghai", "Xiamen", 459),
                (4, "Xiamen", "Shenzhen", 238),
                (5, "Shenzhen", "Kaohsiung", 334),
                (6, "Kaohsiung", "Busan", 879),
                (7, "Busan", "Tianjin", 590)
            });

        AddRoute(
            "S3",
            "SEA-Feeder",
            2.76,
            new()
            {
                (1, "Singapore", "Ho Chi Minh City", 596),
                (2, "Ho Chi Minh City", "Laem Chabang", 369),
                (3, "Laem Chabang", "Singapore", 731),
                (4, "Singapore", "Jakarta", 479),
                (5, "Jakarta", "Singapore", 479)
            });

        AddRoute(
            "S4",
            "Transpacific-WestCoast",
            3.64,
            new()
            {
                (1, "Busan", "Qingdao", 429),
                (2, "Qingdao", "Shanghai", 294),
                (3, "Shanghai", "Kaohsiung", 531),
                (4, "Kaohsiung", "Los Angeles", 12501),
                (5, "Los Angeles", "Shanghai", 16380),
                (6, "Shanghai", "Busan", 428)
            });

        AddRoute(
            "S5",
            "Asia-US-East",
            4.51,
            new()
            {
                (1, "Shanghai", "Shenzhen", 664),
                (2, "Shenzhen", "Singapore", 1417),
                (3, "Singapore", "Colombo", 1476),
                (4, "Colombo", "New Jersey", 10433),
                (5, "New Jersey", "Colombo", 15416),
                (6, "Colombo", "Singapore", 1476),
                (7, "Singapore", "Shanghai", 2075)
            });

        AddRoute(
            "S6",
            "Transatlantic-SouthConnector",
            5.39,
            new()
            {
                (1, "Hamburg", "Rotterdam", 233),
                (2, "Rotterdam", "Tanger Med", 1048),
                (3, "Tanger Med", "New Jersey", 3161),
                (4, "New Jersey", "Cartagena", 1819),
                (5, "Cartagena", "New Jersey", 1819),
                (6, "New Jersey", "Tanger Med", 3161),
                (7, "Tanger Med", "Rotterdam", 1048),
                (8, "Rotterdam", "Hamburg", 233)
            });

        AddRoute(
            "S7",
            "IndianOcean-MiddleEast",
            6.27,
            new()
            {
                (1, "Singapore", "Colombo", 1476),
                (2, "Colombo", "Jebel Ali", 1788),
                (3, "Jebel Ali", "Colombo", 1788),
                (4, "Colombo", "Singapore", 1476)
            });
    }

    /// <summary>
    /// Initializes vessel classes used in the scenario.
    ///
    /// Each vessel class defines the capacity, sailing speed, and length
    /// overall of vessels belonging to that class.
    /// </summary>
    private static void InitializeVesselClasses(MaritimeDataContext context)
    {
        var vesselClasses = new[]
        {
            new VesselClass { Name = "Feeder", TeuCapacity = 1800, SailingSpeed = 20, LOA = 150 },
            new VesselClass { Name = "Feedermax", TeuCapacity = 2800, SailingSpeed = 20, LOA = 200 },
            new VesselClass { Name = "Panamax", TeuCapacity = 5000, SailingSpeed = 20, LOA = 275 },
            new VesselClass { Name = "Post-Panamax", TeuCapacity = 8000, SailingSpeed = 20, LOA = 300 },
            new VesselClass { Name = "Neo-Panamax", TeuCapacity = 13000, SailingSpeed = 20, LOA = 366 },
            new VesselClass { Name = "ULCV", TeuCapacity = 18000, SailingSpeed = 20, LOA = 400 }
        };

        context.VesselClasses.AddRange(vesselClasses);
    }

    /// <summary>
    /// Initializes vessels and assigns them to service routes.
    ///
    /// The route plan specifies how many vessels of each class are deployed
    /// on each service route. Each created vessel is linked to:
    /// - the global vessel list;
    /// - its vessel class; and
    /// - its assigned service route.
    /// </summary>
    private static void InitializeVessels(MaritimeDataContext context)
    {
        var vesselClassByName = context.VesselClasses
            .ToDictionary(vc => vc.Name, StringComparer.OrdinalIgnoreCase);

        var routePlan = new[]
        {
            new { RouteId = "S1", VesselClass = "ULCV", Count = 11 },
            new { RouteId = "S2", VesselClass = "Post-Panamax", Count = 1 },
            new { RouteId = "S3", VesselClass = "Feedermax", Count = 1 },
            new { RouteId = "S4", VesselClass = "Neo-Panamax", Count = 10 },
            new { RouteId = "S5", VesselClass = "Neo-Panamax", Count = 10 },
            new { RouteId = "S6", VesselClass = "Post-Panamax", Count = 4 },
            new { RouteId = "S7", VesselClass = "Post-Panamax", Count = 2 }
        };

        int vesselIndex = 1;

        foreach (var item in routePlan)
        {
            var route = context.ServiceRoutes.Single(r => r.Id == item.RouteId);
            var vesselClass = vesselClassByName[item.VesselClass];

            for (int i = 0; i < item.Count; i++)
            {
                var vessel = new Vessel
                {
                    Index = vesselIndex++,
                    VesselClass = vesselClass,
                    AssignedServiceRoute = route,
                };

                context.Vessels.Add(vessel);
                vesselClass.Vessels.Add(vessel);
                route.DeployedVessels.Add(vessel);
            }
        }
    }
}