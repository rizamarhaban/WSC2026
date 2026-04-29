using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using SimulationChallenge2026;

static class PdfReportGenerator
{
    // ── Colours ────────────────────────────────────────────────────────────────
    static readonly XColor ColHeaderBg  = XColor.FromArgb(31,  73, 125);
    static readonly XColor ColHeaderFg  = XColors.White;
    static readonly XColor ColAltRow    = XColor.FromArgb(229, 240, 255);
    static readonly XColor ColTotalRow  = XColor.FromArgb(180, 210, 240);
    static readonly XColor ColBorder    = XColor.FromArgb(150, 170, 200);
    static readonly XColor ColSubsecFg  = XColor.FromArgb(60, 100, 160);

    // ── Fonts — created lazily after FontResolver is registered ───────────────
    static XFont? _fontPageTitle;
    static XFont? _fontSubsection;
    static XFont? _fontTableHeader;
    static XFont? _fontTableData;
    static XFont? _fontTableTotal;
    static XFont? _fontTitleLarge;
    static XFont? _fontTitleSub;
    static XFont? _fontTitleMeta;

    static XFont FontPageTitle   => _fontPageTitle   ??= new XFont("Arial", 13, XFontStyleEx.Bold);
    static XFont FontSubsection  => _fontSubsection  ??= new XFont("Arial",  9, XFontStyleEx.Bold);
    static XFont FontTableHeader => _fontTableHeader ??= new XFont("Arial",  6, XFontStyleEx.Bold);
    static XFont FontTableData   => _fontTableData   ??= new XFont("Arial",  6, XFontStyleEx.Regular);
    static XFont FontTableTotal  => _fontTableTotal  ??= new XFont("Arial",  6.5, XFontStyleEx.Bold);
    static XFont FontTitleLarge  => _fontTitleLarge  ??= new XFont("Arial", 34, XFontStyleEx.Bold);
    static XFont FontTitleSub    => _fontTitleSub    ??= new XFont("Arial", 16, XFontStyleEx.Regular);
    static XFont FontTitleMeta   => _fontTitleMeta   ??= new XFont("Arial", 11, XFontStyleEx.Regular);

    // ── Layout ─────────────────────────────────────────────────────────────────
    const double Margin          = 28;   // ~10 mm
    const double RowHeight       = 11;
    const double HeaderRowHeight = 13;
    const double SectionGap      = 10;
    const double SubsectionGap   =  8;
    const double CellPadH        =  3;   // horizontal text padding inside a cell

    // ── Public entry point ────────────────────────────────────────────────────
    public static void Generate(Model sim, string outputPath)
    {
        // Must be set before any XFont is constructed.
        GlobalFontSettings.FontResolver ??= new WindowsFontResolver();

        using var doc = new PdfDocument();
        doc.Info.Title   = "WSC2026 - Simulation Output";
        doc.Info.Creator = "Winter Simulation Challenge 2026";

        AddTitlePage(doc, sim);
        AddShipmentWaitingPage(doc, sim);
        AddPortLevelStatisticsPage(doc, sim);
        AddShipmentTransportedPage(doc, sim);
        AddServiceRouteAndVesselPage(doc, sim);

        doc.Save(outputPath);
    }

    // ── Page helpers ──────────────────────────────────────────────────────────
    static (PdfPage page, XGraphics gfx) NewPage(PdfDocument doc)
    {
        var page = doc.AddPage();
        page.Size        = PageSize.A4;
        page.Orientation = PageOrientation.Landscape;
        var gfx = XGraphics.FromPdfPage(page);

        return (page, gfx);
    }

    static double UsableWidth(PdfPage page)  => page.Width  - Margin * 2;
    static double UsableHeight(PdfPage page) => page.Height - Margin * 2;

    // ── Title page ────────────────────────────────────────────────────────────
    static void AddTitlePage(PdfDocument doc, Model sim)
    {
        var (page, gfx) = NewPage(doc);
        using var _ = gfx;

        double w = page.Width;
        double h = page.Height;

        // Background
        gfx.DrawRectangle(new XSolidBrush(ColHeaderBg), new XRect(0, 0, w, h));

        // Horizontal accent stripe
        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(20, 50, 100)),
            new XRect(0, h / 2 - 55, w, 120));

        var fmtCenter = new XStringFormat
        {
            Alignment     = XStringAlignment.Center,
            LineAlignment = XLineAlignment.Center
        };

        // Main title
        gfx.DrawString("Winter Simulation Challenge 2026",
            FontTitleLarge, XBrushes.White,
            new XRect(0, h / 2 - 55, w, 60), fmtCenter);

        // Subtitle
        gfx.DrawString("Final Simulation Output Report",
            FontTitleSub, new XSolidBrush(XColor.FromArgb(190, 215, 255)),
            new XRect(0, h / 2 + 10, w, 36), fmtCenter);

        // Metadata
        var metaBrush = new XSolidBrush(XColor.FromArgb(160, 190, 230));

        gfx.DrawString($"Simulation Clock: {sim.ClockTime:yyyy-MM-dd HH:mm:ss}",
            FontTitleMeta, metaBrush,
            new XRect(0, h / 2 + 58, w, 24), fmtCenter);

        gfx.DrawString($"Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            FontTitleMeta, metaBrush,
            new XRect(0, h / 2 + 82, w, 24), fmtCenter);
    }

    // ── Section: Shipment Waiting for Loading ─────────────────────────────────
    static void AddShipmentWaitingPage(PdfDocument doc, Model sim)
    {
        var originAct         = sim.Shipment_WaitingForLoadingAtOriginPort;
        var transshipmentAct  = sim.Shipment_WaitingForLoadingAtTransshipmentPort;
        var vesselQueueAct    = sim.Vessel_QueuingForBerth;

        var (page, gfx) = NewPage(doc);
        using var _ = gfx;

        double x = Margin;
        double y = Margin;
        double maxW = UsableWidth(page);

        y = DrawPageTitle(gfx, x, y, "Shipment Waiting for Loading Statistics");
        y += SubsectionGap;

        y = DrawSubsectionTitle(gfx, x, y, "TEUs by Demand Matrix");
        y += 4;

        // Build matrix data
        var origins = originAct.HC_TeusByDemand.Keys
            .Select(d => d.OriginPort).Distinct().OrderBy(p => p.Name).ToList();
        var destinations = originAct.HC_TeusByDemand.Keys
            .Select(d => d.DestinationPort).Distinct().OrderBy(p => p.Name).ToList();

        string GetWaitCell(Port o, Port d)
        {
            var item = originAct.HC_TeusByDemand
                .FirstOrDefault(p => p.Key.OriginPort == o && p.Key.DestinationPort == d);

            return item.Key == null ? "-" : item.Value.AverageCount.ToString("N0");
        }

        y = DrawMatrixTable(gfx, x, y, maxW,
            origins.Select(p => p.Name).ToList(),
            destinations.Select(p => p.Name).ToList(),
            (oi, di) => GetWaitCell(origins[oi], destinations[di]));
    }

    // ── Section: Port-Level Statistics (own page) ─────────────────────────────
    static void AddPortLevelStatisticsPage(PdfDocument doc, Model sim)
    {
        var originAct        = sim.Shipment_WaitingForLoadingAtOriginPort;
        var transshipmentAct = sim.Shipment_WaitingForLoadingAtTransshipmentPort;
        var vesselQueueAct   = sim.Vessel_QueuingForBerth;

        var (page, gfx) = NewPage(doc);
        using var _ = gfx;

        double x = Margin, y = Margin;
        double maxW = UsableWidth(page);

        y = DrawPageTitle(gfx, x, y, "Shipment Waiting for Loading Statistics");
        y += SubsectionGap;

        y = DrawSubsectionTitle(gfx, x, y, "Port-Level Shipment Waiting and Vessel Queue Statistics");
        y += 4;

        var ports = originAct.HC_TeusByOriginPort.Keys
            .Union(transshipmentAct.HC_TeusByTransshipmentPort.Keys)
            .Union(vesselQueueAct.HC_NumberOfVesselsByPort.Keys)
            .OrderBy(p => p.Name)
            .ToList();

        var portHeaders = new[] { "Port", "Origin Waiting TEU", "Transshipment Waiting TEU", "Total Waiting TEU", "Vessels Waiting" };

        var portRows = ports.Select(port =>
        {
            double orig  = GetAvgByPort(originAct.HC_TeusByOriginPort, port);
            double trans = GetAvgByPort(transshipmentAct.HC_TeusByTransshipmentPort, port);
            double vess  = GetAvgByPort(vesselQueueAct.HC_NumberOfVesselsByPort, port);

            return new[] { port.Name, orig.ToString("N0"), trans.ToString("N0"), (orig + trans).ToString("N0"), vess.ToString("N2") };
        }).ToList();

        double totalOrig  = originAct.HC_TeusByOriginPort.Values.Sum(c => c.AverageCount);
        double totalTrans = transshipmentAct.HC_TeusByTransshipmentPort.Values.Sum(c => c.AverageCount);
        double totalVess  = vesselQueueAct.HC_NumberOfVesselsByPort.Values.Sum(c => c.AverageCount);

        var portTotal  = new[] { "TOTAL", totalOrig.ToString("N0"), totalTrans.ToString("N0"), (totalOrig + totalTrans).ToString("N0"), totalVess.ToString("N2") };
        var portAligns = new[] { XStringAlignment.Near, XStringAlignment.Far, XStringAlignment.Far, XStringAlignment.Far, XStringAlignment.Far };

        DrawSimpleTable(gfx, x, y, maxW, portHeaders, portRows, portTotal, portAligns);
    }

    // ── Section: Shipment Being Transported ───────────────────────────────────
    static void AddShipmentTransportedPage(PdfDocument doc, Model sim)
    {
        var activity = sim.Shipment_BeingTransported;

        var origins = activity.HC_TeusInTransitionByDemand.Keys
            .Select(d => d.OriginPort).Distinct().OrderBy(p => p.Name).ToList();
        var destinations = activity.HC_TeusInTransitionByDemand.Keys
            .Select(d => d.DestinationPort).Distinct().OrderBy(p => p.Name).ToList();

        // Page A – Average TEUs in Transition
        {
            var (page, gfx) = NewPage(doc);
            using var _ = gfx;

            double x = Margin, y = Margin;
            double maxW = UsableWidth(page);

            y = DrawPageTitle(gfx, x, y, "Shipment Being Transported Statistics");
            y += SubsectionGap;
            y = DrawSubsectionTitle(gfx, x, y, "Average TEUs in Transition by Demand Matrix");
            y += 4;

            DrawMatrixTable(gfx, x, y, maxW,
                origins.Select(p => p.Name).ToList(),
                destinations.Select(p => p.Name).ToList(),
                (oi, di) =>
                {
                    var item = activity.HC_TeusInTransitionByDemand
                        .FirstOrDefault(p => p.Key.OriginPort == origins[oi] && p.Key.DestinationPort == destinations[di]);

                    return item.Key == null ? "-" : item.Value.AverageCount.ToString("N0");
                });
        }

        // Page B – Completed TEUs
        {
            var (page, gfx) = NewPage(doc);
            using var _ = gfx;

            double x = Margin, y = Margin;
            double maxW = UsableWidth(page);

            y = DrawPageTitle(gfx, x, y, "Shipment Being Transported Statistics");
            y += SubsectionGap;
            y = DrawSubsectionTitle(gfx, x, y, "Completed TEUs by Demand Matrix");
            y += 4;

            DrawMatrixTable(gfx, x, y, maxW,
                origins.Select(p => p.Name).ToList(),
                destinations.Select(p => p.Name).ToList(),
                (oi, di) =>
                {
                    var item = activity.HC_TeusInTransitionByDemand
                        .FirstOrDefault(p => p.Key.OriginPort == origins[oi] && p.Key.DestinationPort == destinations[di]);

                    return item.Key == null ? "-" : item.Value.TotalDecrement.ToString("N0");
                });
        }
    }

    // ── Section: Service Route + Vessel Statistics (combined page) ───────────
    static void AddServiceRouteAndVesselPage(PdfDocument doc, Model sim)
    {
        var (page, gfx) = NewPage(doc);
        using var _ = gfx;

        double x = Margin, y = Margin;
        double maxW = UsableWidth(page);

        y = DrawPageTitle(gfx, x, y, "Operational Statistics");
        y += SubsectionGap;

        // ── Service Route Capacity and Utilization ───────────────────────────
        y = DrawSubsectionTitle(gfx, x, y, "Service Route Capacity and Utilization Statistics");
        y += 4;

        var sailingAct = sim.Vessel_Sailing;

        var routes = sailingAct.HC_TeuCapacityByServiceRoute.Keys
            .Union(sailingAct.HC_CarriedTeusByServiceRoute.Keys)
            .OrderBy(r => r.Id)
            .ToList();

        var routeHeaders = new[] { "Route ID", "Route Name", "Avg Capacity TEU", "Avg Carried TEU", "Utilization" };

        var routeRows = routes.Select(r =>
        {
            double cap  = GetAvgByRoute(sailingAct.HC_TeuCapacityByServiceRoute, r);
            double carr = GetAvgByRoute(sailingAct.HC_CarriedTeusByServiceRoute, r);
            double util = cap > 0 ? carr / cap : 0;

            return new[] { r.Id.ToString(), r.Name, cap.ToString("N0"), carr.ToString("N0"), util.ToString("P2") };
        }).ToList();

        double totalCap  = sailingAct.HC_TeuCapacityByServiceRoute.Values.Sum(c => c.AverageCount);
        double totalCarr = sailingAct.HC_CarriedTeusByServiceRoute.Values.Sum(c => c.AverageCount);
        double totalUtil = totalCap > 0 ? totalCarr / totalCap : 0;

        var routeTotal  = new[] { "TOTAL", "", totalCap.ToString("N0"), totalCarr.ToString("N0"), totalUtil.ToString("P2") };
        var routeAligns = new[] { XStringAlignment.Near, XStringAlignment.Near, XStringAlignment.Far, XStringAlignment.Far, XStringAlignment.Far };

        y = DrawSimpleTable(gfx, x, y, maxW, routeHeaders, routeRows, routeTotal, routeAligns);
        y += SectionGap;

        // ── Vessel Statistics ─────────────────────────────────────────────────
        y = DrawSubsectionTitle(gfx, x, y, "Vessel Statistics");
        y += 4;

        double sailing = sim.Vessel_Sailing.S_HourCounter.AverageCount;
        double waiting = sim.Vessel_QueuingForBerth.D_HourCounter.AverageCount;
        double serving = sim.Vessel_BeingServed.D_HourCounter.AverageCount;

        var vesselHeaders = new[] { "Metric", "Average Count" };

        var vesselRows = new List<string[]>
        {
            new[] { "Average vessels sailing",                   sailing.ToString("N2") },
            new[] { "Average vessels waiting for berth at port", waiting.ToString("N2") },
            new[] { "Average vessels being served at berth",     serving.ToString("N2") },
        };

        var vesselTotal  = new[] { "TOTAL average active/port vessels", (sailing + waiting + serving).ToString("N2") };
        var vesselAligns = new[] { XStringAlignment.Near, XStringAlignment.Far };

        DrawSimpleTable(gfx, x, y, maxW, vesselHeaders, vesselRows, vesselTotal, vesselAligns);
    }

    // ── Drawing: page/section titles ──────────────────────────────────────────
    static double DrawPageTitle(XGraphics gfx, double x, double y, string text)
    {
        double titleH = 28;

        gfx.DrawRectangle(new XSolidBrush(ColHeaderBg), new XRect(x, y, gfx.PageSize.Width - Margin * 2, titleH));

        var fmt = new XStringFormat { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Center };
        gfx.DrawString(text, FontPageTitle, new XSolidBrush(ColHeaderFg),
            new XRect(x + CellPadH, y, gfx.PageSize.Width - Margin * 2 - CellPadH, titleH), fmt);

        return y + titleH;
    }

    static double DrawSubsectionTitle(XGraphics gfx, double x, double y, string text)
    {
        double h = 20;
        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(210, 225, 245)), new XRect(x, y, gfx.PageSize.Width - Margin * 2, h));

        var fmt = new XStringFormat { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Center };
        gfx.DrawString(text, FontSubsection, new XSolidBrush(ColSubsecFg),
            new XRect(x + CellPadH, y, gfx.PageSize.Width - Margin * 2 - CellPadH, h), fmt);

        return y + h;
    }

    // ── Drawing: simple column table ──────────────────────────────────────────
    static double DrawSimpleTable(
        XGraphics gfx,
        double x, double y,
        double maxWidth,
        string[] headers,
        List<string[]> rows,
        string[]? totalRow,
        XStringAlignment[] alignments)
    {
        // Compute column widths from content
        int cols = headers.Length;
        var widths = new double[cols];

        for (int c = 0; c < cols; c++)
        {
            widths[c] = MeasureText(gfx, headers[c], FontTableHeader);

            foreach (var row in rows)
                if (c < row.Length)
                    widths[c] = Math.Max(widths[c], MeasureText(gfx, row[c], FontTableData));

            if (totalRow != null && c < totalRow.Length)
                widths[c] = Math.Max(widths[c], MeasureText(gfx, totalRow[c], FontTableTotal));

            widths[c] += CellPadH * 2;
        }

        // Scale to fit if needed
        double total = widths.Sum();

        if (total > maxWidth)
        {
            double scale = maxWidth / total;
            for (int c = 0; c < cols; c++)
                widths[c] *= scale;
        }

        // Draw header row
        double cx = x;

        for (int c = 0; c < cols; c++)
        {
            DrawCell(gfx, cx, y, widths[c], HeaderRowHeight, headers[c],
                FontTableHeader, ColHeaderFg, ColHeaderBg, XStringAlignment.Center);
            cx += widths[c];
        }

        y += HeaderRowHeight;

        // Draw data rows
        for (int r = 0; r < rows.Count; r++)
        {
            cx = x;
            var rowBg = r % 2 == 0 ? XColors.White : ColAltRow;

            for (int c = 0; c < cols; c++)
            {
                string text = c < rows[r].Length ? rows[r][c] : "";
                DrawCell(gfx, cx, y, widths[c], RowHeight, text,
                    FontTableData, XColors.Black, rowBg, alignments[c]);
                cx += widths[c];
            }

            y += RowHeight;
        }

        // Draw total row
        if (totalRow != null)
        {
            cx = x;

            for (int c = 0; c < cols; c++)
            {
                string text = c < totalRow.Length ? totalRow[c] : "";
                DrawCell(gfx, cx, y, widths[c], RowHeight, text,
                    FontTableTotal, XColors.Black, ColTotalRow, alignments[c]);
                cx += widths[c];
            }

            y += RowHeight;
        }

        return y;
    }

    // ── Drawing: origin × destination matrix table ────────────────────────────
    static double DrawMatrixTable(
        XGraphics gfx,
        double x, double y,
        double maxWidth,
        List<string> originNames,
        List<string> destinationNames,
        Func<int, int, string> getCellValue)
    {
        int nRows = originNames.Count;
        int nCols = destinationNames.Count;

        const string firstHeader = "Origin \\ Destination";

        // Compute natural widths
        double firstColW = MeasureText(gfx, firstHeader, FontTableHeader);

        foreach (var name in originNames)
            firstColW = Math.Max(firstColW, MeasureText(gfx, name, FontTableData));

        firstColW += CellPadH * 2;

        var colWidths = new double[nCols];

        for (int c = 0; c < nCols; c++)
        {
            colWidths[c] = MeasureText(gfx, destinationNames[c], FontTableHeader);

            for (int r = 0; r < nRows; r++)
                colWidths[c] = Math.Max(colWidths[c], MeasureText(gfx, getCellValue(r, c), FontTableData));

            colWidths[c] += CellPadH * 2;
        }

        // Scale to fit
        double totalW = firstColW + colWidths.Sum();

        if (totalW > maxWidth)
        {
            double scale = maxWidth / totalW;
            firstColW *= scale;

            for (int c = 0; c < nCols; c++)
                colWidths[c] *= scale;
        }

        // Header row
        DrawCell(gfx, x, y, firstColW, HeaderRowHeight, firstHeader,
            FontTableHeader, ColHeaderFg, ColHeaderBg, XStringAlignment.Near);

        double cx = x + firstColW;

        for (int c = 0; c < nCols; c++)
        {
            DrawCell(gfx, cx, y, colWidths[c], HeaderRowHeight, destinationNames[c],
                FontTableHeader, ColHeaderFg, ColHeaderBg, XStringAlignment.Center);
            cx += colWidths[c];
        }

        y += HeaderRowHeight;

        // Data rows with alternating background
        for (int r = 0; r < nRows; r++)
        {
            var rowBg = r % 2 == 0 ? XColors.White : ColAltRow;
            cx = x;

            DrawCell(gfx, cx, y, firstColW, RowHeight, originNames[r],
                FontTableData, XColors.Black, rowBg, XStringAlignment.Near);

            cx += firstColW;

            for (int c = 0; c < nCols; c++)
            {
                DrawCell(gfx, cx, y, colWidths[c], RowHeight, getCellValue(r, c),
                    FontTableData, XColors.Black, rowBg, XStringAlignment.Far);
                cx += colWidths[c];
            }

            y += RowHeight;
        }

        return y;
    }

    // ── Drawing: single cell ──────────────────────────────────────────────────
    static void DrawCell(
        XGraphics gfx,
        double x, double y,
        double width, double height,
        string text,
        XFont font,
        XColor foreground,
        XColor background,
        XStringAlignment alignment)
    {
        var rect = new XRect(x, y, width, height);

        gfx.DrawRectangle(new XSolidBrush(background), rect);
        gfx.DrawRectangle(new XPen(ColBorder, 0.3), rect);

        var textRect = new XRect(x + CellPadH, y, width - CellPadH * 2, height);
        var fmt = new XStringFormat
        {
            Alignment     = alignment,
            LineAlignment = XLineAlignment.Center
        };

        gfx.DrawString(text, font, new XSolidBrush(foreground), textRect, fmt);
    }

    // ── Utilities ─────────────────────────────────────────────────────────────
    static double MeasureText(XGraphics gfx, string text, XFont font)
        => gfx.MeasureString(text, font).Width;

    static double GetAvgByPort(Dictionary<Port, O2DESNet.HourCounter> counters, Port port)
        => counters.TryGetValue(port, out var c) ? c.AverageCount : 0.0;

    static double GetAvgByRoute(Dictionary<ServiceRoute, O2DESNet.HourCounter> counters, ServiceRoute route)
        => counters.TryGetValue(route, out var c) ? c.AverageCount : 0.0;
}

// ── Font resolver for Windows system fonts ────────────────────────────────────
sealed class WindowsFontResolver : IFontResolver
{
    static readonly string FontsDir =
        Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    // Map face name → filename in C:\Windows\Fonts
    static readonly Dictionary<string, string> FaceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Arial#400",    "arial.ttf"  },
        { "Arial#700",    "arialbd.ttf" },
        { "Arial#400i",   "ariali.ttf" },
        { "Arial#700i",   "arialbi.ttf" },
    };

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        string style = $"{(isBold ? 700 : 400)}{(isItalic ? "i" : "")}";
        string key   = $"{familyName}#{style}";

        if (FaceMap.ContainsKey(key))
            return new FontResolverInfo(key);

        // Fallback: return regular weight
        string fallback = $"{familyName}#400";

        if (FaceMap.ContainsKey(fallback))
            return new FontResolverInfo(fallback);

        return null;
    }

    public byte[]? GetFont(string faceName)
    {
        if (!FaceMap.TryGetValue(faceName, out var fileName))
            return null;

        string path = Path.Combine(FontsDir, fileName);

        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}
