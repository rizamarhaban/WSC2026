using System.Text;
using SimulationChallenge2026;

Console.Title = "Winter Simulation Challenge 2026";

var context = MaritimeDataInitializer.Create();

var sim = new Model(context, seed: 1);

var totalDays = 360;
var updateIntervalDays = 10;

for (var day = updateIntervalDays; day <= totalDays; day += updateIntervalDays)
{
    sim.Run(TimeSpan.FromDays(updateIntervalDays));

    var sb = new StringBuilder();

    sb.AppendLine($"Simulation Progress: Day {day:N0} / {totalDays:N0}");
    sb.AppendLine($"Simulation Clock Time: {sim.ClockTime:yyyy-MM-dd HH:mm:ss}");

    AppendShipmentAndPortStatistics(sb, sim);

    AppendShipmentBeingTransportedStatistics(sb, sim);

    AppendServiceRouteCapacityUtilizationStatistics(sb, sim);

    AppendVesselStatistics(sb, sim);

    sb.AppendLine();

    ClearConsoleScreen();
    Console.Write(sb.ToString());
}

Console.WriteLine("Simulation completed.");
Console.WriteLine();

var pdfPath = Path.Combine(AppContext.BaseDirectory, "WSC2026 - Simulation Output.pdf");
PdfReportGenerator.Generate(sim, pdfPath);
Console.WriteLine($"PDF report saved to: {pdfPath}");
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

static void AppendShipmentAndPortStatistics(StringBuilder sb, Model sim)
{
    if (sim == null)
        throw new ArgumentNullException(nameof(sim));

    var originActivity = sim.Shipment_WaitingForLoadingAtOriginPort;
    var transshipmentActivity = sim.Shipment_WaitingForLoadingAtTransshipmentPort;
    var vesselQueueActivity = sim.Vessel_QueuingForBerth;

    sb.AppendLine();
    sb.AppendLine("============================================================");
    sb.AppendLine("Shipment Waiting for Loading Statistics");
    sb.AppendLine("============================================================");

    // Keep the original demand matrix.
    AppendTeusByDemandMatrix(sb, originActivity);

    // Print port-level statistics for shipment waiting TEUs and vessel queues.
    AppendPortLevelStatistics(sb, originActivity, transshipmentActivity, vesselQueueActivity);

    sb.AppendLine();
}

static void AppendShipmentBeingTransportedStatistics(StringBuilder sb, Model sim)
{
    if (sim == null)
        throw new ArgumentNullException(nameof(sim));

    var activity = sim.Shipment_BeingTransported;

    sb.AppendLine();
    sb.AppendLine("============================================================");
    sb.AppendLine("Shipment Being Transported Statistics");
    sb.AppendLine("============================================================");

    AppendAverageTeusInTransitionByDemandMatrix(sb, activity);

    AppendCompletedTeusByDemandMatrix(sb, activity);

    sb.AppendLine();
}

static void AppendTeusByDemandMatrix(StringBuilder sb, Shipment_WaitingForLoadingAtOriginPort activity)
{
    sb.AppendLine();
    sb.AppendLine("TEUs by Demand Matrix");
    sb.AppendLine("------------------------------------------------------------");

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

    string GetCellValue(Port origin, Port dest)
    {
        var item = activity.HC_TeusByDemand
            .FirstOrDefault(pair =>
                pair.Key.OriginPort == origin &&
                pair.Key.DestinationPort == dest);

        return item.Key == null ? "-" : item.Value.AverageCount.ToString("N0");
    }

    AppendMatrixTable(sb, origins, destinations, GetCellValue);
}

static void AppendPortLevelStatistics(
    StringBuilder sb,
    Shipment_WaitingForLoadingAtOriginPort originActivity,
    Shipment_WaitingForLoadingAtTransshipmentPort transshipmentActivity,
    Vessel_QueuingForBerth vesselQueueActivity)
{
    sb.AppendLine();
    sb.AppendLine("Port-Level Shipment Waiting and Vessel Queue Statistics");
    sb.AppendLine();

    sb.AppendLine(
        $"{"Port",-25}" +
        $"{"Origin Waiting TEU",20}" +
        $"{"Transshipment Waiting TEU",28}" +
        $"{"Total Waiting TEU",20}" +
        $"{"Vessels Waiting",18}");

    sb.AppendLine(new string('-', 111));

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

        sb.AppendLine(
            $"{port.Name,-25}" +
            $"{originAverageTeu,20:N0}" +
            $"{transshipmentAverageTeu,28:N0}" +
            $"{totalAverageTeu,20:N0}" +
            $"{averageVesselsWaiting,18:N2}");
    }

    sb.AppendLine(new string('-', 111));

    double totalOriginAverageTeu = originActivity.HC_TeusByOriginPort
        .Values
        .Sum(counter => counter.AverageCount);

    double totalTransshipmentAverageTeu = transshipmentActivity.HC_TeusByTransshipmentPort
        .Values
        .Sum(counter => counter.AverageCount);

    double totalAverageVesselsWaiting = vesselQueueActivity.HC_NumberOfVesselsByPort
        .Values
        .Sum(counter => counter.AverageCount);

    sb.AppendLine(
        $"{"TOTAL",-25}" +
        $"{totalOriginAverageTeu,20:N0}" +
        $"{totalTransshipmentAverageTeu,28:N0}" +
        $"{totalOriginAverageTeu + totalTransshipmentAverageTeu,20:N0}" +
        $"{totalAverageVesselsWaiting,18:N2}");

    sb.AppendLine();
}

static void AppendAverageTeusInTransitionByDemandMatrix(
    StringBuilder sb,
    Shipment_BeingTransported activity)
{
    sb.AppendLine();
    sb.AppendLine("Average TEUs in Transition by Demand Matrix");
    sb.AppendLine("------------------------------------------------------------");

    AppendDemandMatrix(
        sb,
        activity.HC_TeusInTransitionByDemand,
        counter => counter.AverageCount,
        "N0");
}

static void AppendCompletedTeusByDemandMatrix(
    StringBuilder sb,
    Shipment_BeingTransported activity)
{
    sb.AppendLine();
    sb.AppendLine("Completed TEUs by Demand Matrix");
    sb.AppendLine("------------------------------------------------------------");

    AppendDemandMatrix(
        sb,
        activity.HC_TeusInTransitionByDemand,
        counter => counter.TotalDecrement,
        "N0");
}

static void AppendDemandMatrix(
    StringBuilder sb,
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

    string GetCellValue(Port origin, Port dest)
    {
        var item = counters
            .FirstOrDefault(pair =>
                pair.Key.OriginPort == origin &&
                pair.Key.DestinationPort == dest);

        return item.Key == null ? "-" : valueSelector(item.Value).ToString(numberFormat);
    }

    AppendMatrixTable(sb, origins, destinations, GetCellValue);
}

static void AppendMatrixTable(
    StringBuilder sb,
    List<Port> origins,
    List<Port> destinations,
    Func<Port, Port, string> getCellValue)
{
    const string firstHeader = "Origin \\ Destination";

    int firstColWidth = Math.Max(
        firstHeader.Length,
        origins.Count > 0 ? origins.Max(o => o.Name.Length) : 0) + 2;

    int[] colWidths = destinations.Select(dest =>
    {
        int maxValueLen = origins.Count > 0
            ? origins.Max(o => getCellValue(o, dest).Length)
            : 0;

        return Math.Max(dest.Name.Length + 2, maxValueLen + 2);
    }).ToArray();

    sb.Append(firstHeader.PadRight(firstColWidth));

    for (int i = 0; i < destinations.Count; i++)
    {
        sb.Append(destinations[i].Name.PadLeft(colWidths[i]));
    }

    sb.AppendLine();

    sb.Append(new string('-', firstColWidth));

    for (int i = 0; i < destinations.Count; i++)
    {
        sb.Append(new string('-', colWidths[i]));
    }

    sb.AppendLine();

    for (int row = 0; row < origins.Count; row++)
    {
        // Alternate rows: even = white, odd = yellow.
        sb.Append(row % 2 == 0 ? "\u001b[97m" : "\u001b[93m");

        sb.Append(origins[row].Name.PadRight(firstColWidth));

        for (int i = 0; i < destinations.Count; i++)
        {
            string value = getCellValue(origins[row], destinations[i]);
            sb.Append(value.PadLeft(colWidths[i]));
        }

        sb.Append("\u001b[0m");
        sb.AppendLine();
    }
}

static void AppendVesselStatistics(StringBuilder sb, Model sim)
{
    if (sim == null)
        throw new ArgumentNullException(nameof(sim));

    sb.AppendLine();
    sb.AppendLine("============================================================");
    sb.AppendLine("Vessel Statistics");
    sb.AppendLine("============================================================");

    double averageVesselsSailing =
        sim.Vessel_Sailing.S_HourCounter.AverageCount;

    double averageVesselsWaitingAtPort =
        sim.Vessel_QueuingForBerth.D_HourCounter.AverageCount;

    double averageVesselsBeingServed =
        sim.Vessel_BeingServed.D_HourCounter.AverageCount;

    sb.AppendLine(
        $"{"Metric",-45}" +
        $"{"Average Count",18}");

    sb.AppendLine(new string('-', 63));

    sb.AppendLine(
        $"{"Average vessels sailing",-45}" +
        $"{averageVesselsSailing,18:N2}");

    sb.AppendLine(
        $"{"Average vessels waiting for berth at port",-45}" +
        $"{averageVesselsWaitingAtPort,18:N2}");

    sb.AppendLine(
        $"{"Average vessels being served at berth",-45}" +
        $"{averageVesselsBeingServed,18:N2}");

    sb.AppendLine(new string('-', 63));

    sb.AppendLine(
        $"{"TOTAL average active/port vessels",-45}" +
        $"{averageVesselsSailing + averageVesselsWaitingAtPort + averageVesselsBeingServed,18:N2}");

    sb.AppendLine();
}

static void AppendServiceRouteCapacityUtilizationStatistics(StringBuilder sb, Model sim)
{
    if (sim == null)
        throw new ArgumentNullException(nameof(sim));

    var vesselSailingActivity = sim.Vessel_Sailing;

    sb.AppendLine();
    sb.AppendLine("============================================================");
    sb.AppendLine("Service Route Capacity and Utilization Statistics");
    sb.AppendLine("============================================================");

    sb.AppendLine(
        $"{"Route",-10}" +
        $"{"Name",-32}" +
        $"{"Avg Capacity TEU",20}" +
        $"{"Avg Carried TEU",20}" +
        $"{"Utilization",15}");

    sb.AppendLine(new string('-', 97));

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

        sb.AppendLine(
            $"{serviceRoute.Id,-10}" +
            $"{serviceRoute.Name,-32}" +
            $"{averageCapacityTeu,20:N0}" +
            $"{averageCarriedTeu,20:N0}" +
            $"{utilization,15:P2}");
    }

    sb.AppendLine(new string('-', 97));

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

    sb.AppendLine(
        $"{"TOTAL",-10}" +
        $"{"",-32}" +
        $"{totalAverageCapacityTeu,20:N0}" +
        $"{totalAverageCarriedTeu,20:N0}" +
        $"{totalUtilization,15:P2}");

    sb.AppendLine();
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