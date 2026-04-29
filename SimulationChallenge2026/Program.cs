using SimulationChallenge2026;

var context = MaritimeDataInitializer.Create();

var sim = new Model(context, seed: 1);

var totalDays = 360;
var updateIntervalDays = 10;

for (var day = updateIntervalDays; day <= totalDays; day += updateIntervalDays)
{
    sim.Run(TimeSpan.FromDays(updateIntervalDays));

    ClearConsoleScreen();

    Console.WriteLine($"Simulation Progress: Day {day:N0} / {totalDays:N0}");
    Console.WriteLine($"Simulation Clock Time: {sim.ClockTime:yyyy-MM-dd HH:mm:ss}");

    PrintShipmentAndPortStatistics(sim);

    PrintShipmentBeingTransportedStatistics(sim);

    PrintServiceRouteCapacityUtilizationStatistics(sim);

    PrintVesselStatistics(sim);

    Console.WriteLine();
}

Console.WriteLine("Simulation completed.");
Console.WriteLine();

#region Statistics Output

static void ClearConsoleScreen()
{
    if (Console.IsOutputRedirected)
        return;

    // Clear screen + clear scrollback + move cursor to top-left.
    Console.Write("\u001b[2J\u001b[3J\u001b[H");
    Console.Out.Flush();
}

static void PrintShipmentAndPortStatistics(Model sim)
{
    if (sim == null)
        throw new ArgumentNullException(nameof(sim));

    var originActivity = sim.Shipment_WaitingForLoadingAtOriginPort;
    var transshipmentActivity = sim.Shipment_WaitingForLoadingAtTransshipmentPort;
    var vesselQueueActivity = sim.Vessel_QueuingForBerth;

    Console.WriteLine();
    Console.WriteLine("============================================================");
    Console.WriteLine("Shipment Waiting for Loading Statistics");
    Console.WriteLine("============================================================");

    // Keep the original demand matrix.
    PrintTeusByDemandMatrix(originActivity);

    // Print port-level statistics for shipment waiting TEUs and vessel queues.
    PrintPortLevelStatistics(originActivity, transshipmentActivity, vesselQueueActivity);

    Console.WriteLine();
}

static void PrintShipmentBeingTransportedStatistics(Model sim)
{
    if (sim == null)
        throw new ArgumentNullException(nameof(sim));

    var activity = sim.Shipment_BeingTransported;

    Console.WriteLine();
    Console.WriteLine("============================================================");
    Console.WriteLine("Shipment Being Transported Statistics");
    Console.WriteLine("============================================================");

    PrintAverageTeusInTransitionByDemandMatrix(activity);

    PrintCompletedTeusByDemandMatrix(activity);

    Console.WriteLine();
}

static void PrintTeusByDemandMatrix(Shipment_WaitingForLoadingAtOriginPort activity)
{
    Console.WriteLine();
    Console.WriteLine("TEUs by Demand Matrix");
    Console.WriteLine("------------------------------------------------------------");

    var origins = activity.HC_TeusByDemand.Keys
        .Select(demand => demand.OriginPort)
        .Distinct()
        .OrderBy(port => port.Name)
        .ToList();

    var destinations = activity.HC_TeusByDemand.Keys
        .Select(demand => demand.DestinationPort)
        .Distinct()
        .OrderBy(port => port.Name)
        .ToList();

    const int firstColumnWidth = 18;
    const int cellWidth = 14;

    Console.Write($"{"Origin \\ Destination",-firstColumnWidth}");

    foreach (var destination in destinations)
    {
        Console.Write($"{destination.Name,cellWidth}");
    }

    Console.WriteLine();

    Console.Write($"{new string('-', firstColumnWidth),-firstColumnWidth}");

    foreach (var _ in destinations)
    {
        Console.Write($"{new string('-', cellWidth),cellWidth}");
    }

    Console.WriteLine();

    foreach (var origin in origins)
    {
        Console.Write($"{origin.Name,-firstColumnWidth}");

        foreach (var destination in destinations)
        {
            var item = activity.HC_TeusByDemand
                .FirstOrDefault(pair =>
                    pair.Key.OriginPort == origin &&
                    pair.Key.DestinationPort == destination);

            if (item.Key == null)
            {
                Console.Write($"{"-",cellWidth}");
            }
            else
            {
                Console.Write($"{item.Value.AverageCount,cellWidth:N0}");
            }
        }

        Console.WriteLine();
    }
}

static void PrintPortLevelStatistics(
    Shipment_WaitingForLoadingAtOriginPort originActivity,
    Shipment_WaitingForLoadingAtTransshipmentPort transshipmentActivity,
    Vessel_QueuingForBerth vesselQueueActivity)
{
    Console.WriteLine();
    Console.WriteLine("Port-Level Shipment Waiting and Vessel Queue Statistics");
    Console.WriteLine();

    Console.WriteLine(
        $"{"Port",-25}" +
        $"{"Origin Waiting TEU",20}" +
        $"{"Transshipment Waiting TEU",28}" +
        $"{"Total Waiting TEU",20}" +
        $"{"Vessels Waiting",18}");

    Console.WriteLine(new string('-', 111));

    var ports = originActivity.HC_TeusByOriginPort.Keys
        .Union(transshipmentActivity.HC_TeusByTransshipmentPort.Keys)
        .Union(vesselQueueActivity.HC_NumberOfVesselsByPort.Keys)
        .OrderBy(port => port.Name)
        .ToList();

    foreach (var port in ports)
    {
        double originAverageTeu = GetAverageCount_byPort(
            originActivity.HC_TeusByOriginPort,
            port);

        double transshipmentAverageTeu = GetAverageCount_byPort(
            transshipmentActivity.HC_TeusByTransshipmentPort,
            port);

        double totalAverageTeu = originAverageTeu + transshipmentAverageTeu;

        double averageVesselsWaiting = GetAverageCount_byPort(
            vesselQueueActivity.HC_NumberOfVesselsByPort,
            port);

        Console.WriteLine(
            $"{port.Name,-25}" +
            $"{originAverageTeu,20:N0}" +
            $"{transshipmentAverageTeu,28:N0}" +
            $"{totalAverageTeu,20:N0}" +
            $"{averageVesselsWaiting,18:N2}");
    }

    Console.WriteLine(new string('-', 111));

    double totalOriginAverageTeu = originActivity.HC_TeusByOriginPort
        .Values
        .Sum(counter => counter.AverageCount);

    double totalTransshipmentAverageTeu = transshipmentActivity.HC_TeusByTransshipmentPort
        .Values
        .Sum(counter => counter.AverageCount);

    double totalAverageVesselsWaiting = vesselQueueActivity.HC_NumberOfVesselsByPort
        .Values
        .Sum(counter => counter.AverageCount);

    Console.WriteLine(
        $"{"TOTAL",-25}" +
        $"{totalOriginAverageTeu,20:N0}" +
        $"{totalTransshipmentAverageTeu,28:N0}" +
        $"{totalOriginAverageTeu + totalTransshipmentAverageTeu,20:N0}" +
        $"{totalAverageVesselsWaiting,18:N2}");

    Console.WriteLine();
}

static void PrintAverageTeusInTransitionByDemandMatrix(
    Shipment_BeingTransported activity)
{
    Console.WriteLine();
    Console.WriteLine("Average TEUs in Transition by Demand Matrix");
    Console.WriteLine("------------------------------------------------------------");

    PrintDemandMatrix(
        activity.HC_TeusInTransitionByDemand,
        counter => counter.AverageCount,
        "N0");
}

static void PrintCompletedTeusByDemandMatrix(
    Shipment_BeingTransported activity)
{
    Console.WriteLine();
    Console.WriteLine("Completed TEUs by Demand Matrix");
    Console.WriteLine("------------------------------------------------------------");

    PrintDemandMatrix(
        activity.HC_TeusInTransitionByDemand,
        counter => counter.TotalDecrement,
        "N0");
}

static void PrintDemandMatrix(
    Dictionary<Demand, O2DESNet.HourCounter> counters,
    Func<O2DESNet.HourCounter, double> valueSelector,
    string numberFormat)
{
    var origins = counters.Keys
        .Select(demand => demand.OriginPort)
        .Distinct()
        .OrderBy(port => port.Name)
        .ToList();

    var destinations = counters.Keys
        .Select(demand => demand.DestinationPort)
        .Distinct()
        .OrderBy(port => port.Name)
        .ToList();

    const int firstColumnWidth = 18;
    const int cellWidth = 14;

    Console.Write($"{"Origin \\ Destination",-firstColumnWidth}");

    foreach (var destination in destinations)
    {
        Console.Write($"{destination.Name,cellWidth}");
    }

    Console.WriteLine();

    Console.Write($"{new string('-', firstColumnWidth),-firstColumnWidth}");

    foreach (var _ in destinations)
    {
        Console.Write($"{new string('-', cellWidth),cellWidth}");
    }

    Console.WriteLine();

    foreach (var origin in origins)
    {
        Console.Write($"{origin.Name,-firstColumnWidth}");

        foreach (var destination in destinations)
        {
            var item = counters
                .FirstOrDefault(pair =>
                    pair.Key.OriginPort == origin &&
                    pair.Key.DestinationPort == destination);

            if (item.Key == null)
            {
                Console.Write($"{"-",cellWidth}");
            }
            else
            {
                double value = valueSelector(item.Value);
                Console.Write($"{value.ToString(numberFormat),cellWidth}");
            }
        }

        Console.WriteLine();
    }
}

static void PrintVesselStatistics(Model sim)
{
    if (sim == null)
        throw new ArgumentNullException(nameof(sim));

    Console.WriteLine();
    Console.WriteLine("============================================================");
    Console.WriteLine("Vessel Statistics");
    Console.WriteLine("============================================================");

    double averageVesselsSailing =
        sim.Vessel_Sailing.S_HourCounter.AverageCount;

    double averageVesselsWaitingAtPort =
        sim.Vessel_QueuingForBerth.D_HourCounter.AverageCount;

    double averageVesselsBeingServed =
        sim.Vessel_BeingServed.D_HourCounter.AverageCount;

    Console.WriteLine(
        $"{"Metric",-45}" +
        $"{"Average Count",18}");

    Console.WriteLine(new string('-', 63));

    Console.WriteLine(
        $"{"Average vessels sailing",-45}" +
        $"{averageVesselsSailing,18:N2}");

    Console.WriteLine(
        $"{"Average vessels waiting for berth at port",-45}" +
        $"{averageVesselsWaitingAtPort,18:N2}");

    Console.WriteLine(
        $"{"Average vessels being served at berth",-45}" +
        $"{averageVesselsBeingServed,18:N2}");

    Console.WriteLine(new string('-', 63));

    Console.WriteLine(
        $"{"TOTAL average active/port vessels",-45}" +
        $"{averageVesselsSailing + averageVesselsWaitingAtPort + averageVesselsBeingServed,18:N2}");

    Console.WriteLine();
}

static void PrintServiceRouteCapacityUtilizationStatistics(Model sim)
{
    if (sim == null)
        throw new ArgumentNullException(nameof(sim));

    var vesselSailingActivity = sim.Vessel_Sailing;

    Console.WriteLine();
    Console.WriteLine("============================================================");
    Console.WriteLine("Service Route Capacity and Utilization Statistics");
    Console.WriteLine("============================================================");

    Console.WriteLine(
        $"{"Route",-10}" +
        $"{"Name",-32}" +
        $"{"Avg Capacity TEU",20}" +
        $"{"Avg Carried TEU",20}" +
        $"{"Utilization",15}");

    Console.WriteLine(new string('-', 97));

    var serviceRoutes = vesselSailingActivity.HC_TeuCapacityByServiceRoute.Keys
        .Union(vesselSailingActivity.HC_CarriedTeusByServiceRoute.Keys)
        .OrderBy(route => route.Id)
        .ToList();

    foreach (var serviceRoute in serviceRoutes)
    {
        double averageCapacityTeu = GetAverageCount_byServiceRoute(
            vesselSailingActivity.HC_TeuCapacityByServiceRoute,
            serviceRoute);

        double averageCarriedTeu = GetAverageCount_byServiceRoute(
            vesselSailingActivity.HC_CarriedTeusByServiceRoute,
            serviceRoute);

        double utilization = averageCapacityTeu > 0
            ? averageCarriedTeu / averageCapacityTeu
            : 0.0;

        Console.WriteLine(
            $"{serviceRoute.Id,-10}" +
            $"{serviceRoute.Name,-32}" +
            $"{averageCapacityTeu,20:N0}" +
            $"{averageCarriedTeu,20:N0}" +
            $"{utilization,15:P2}");
    }

    Console.WriteLine(new string('-', 97));

    double totalAverageCapacityTeu = vesselSailingActivity
        .HC_TeuCapacityByServiceRoute
        .Values
        .Sum(counter => counter.AverageCount);

    double totalAverageCarriedTeu = vesselSailingActivity
        .HC_CarriedTeusByServiceRoute
        .Values
        .Sum(counter => counter.AverageCount);

    double totalUtilization = totalAverageCapacityTeu > 0
        ? totalAverageCarriedTeu / totalAverageCapacityTeu
        : 0.0;

    Console.WriteLine(
        $"{"TOTAL",-10}" +
        $"{"",-32}" +
        $"{totalAverageCapacityTeu,20:N0}" +
        $"{totalAverageCarriedTeu,20:N0}" +
        $"{totalUtilization,15:P2}");

    Console.WriteLine();
}

static double GetAverageCount_byPort(
    Dictionary<Port, O2DESNet.HourCounter> counters,
    Port port)
{
    return counters.TryGetValue(port, out var counter)
        ? counter.AverageCount
        : 0.0;
}

static double GetAverageCount_byServiceRoute(
    Dictionary<ServiceRoute, O2DESNet.HourCounter> counters,
    ServiceRoute serviceRoute)
{
    return counters.TryGetValue(serviceRoute, out var counter)
        ? counter.AverageCount
        : 0.0;
}

#endregion