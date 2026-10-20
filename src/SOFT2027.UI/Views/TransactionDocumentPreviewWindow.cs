using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Shell;

namespace SOFT2027.UI.Views;

public sealed class TransactionDocumentLine
{
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal DiscountPercentage { get; init; }
    public decimal LineTotal { get; init; }
}

public sealed class TransactionDocumentData
{
    public string Title { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public string PartyLabel { get; init; } = string.Empty;
    public string PartyName { get; init; } = string.Empty;
    public string? PartyCode { get; init; }
    public string? AreaTerritory { get; init; }
    public string? ContactPerson { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public IReadOnlyList<TransactionDocumentLine> Lines { get; init; } = Array.Empty<TransactionDocumentLine>();
    public decimal SubTotal { get; init; }
    public decimal Discount { get; init; }
    public decimal Total { get; init; }
    public decimal Paid { get; init; }
    public decimal Balance { get; init; }
    public string PaymentSource { get; init; } = string.Empty;
    public string? Remarks { get; init; }
}

public sealed class TransactionDocumentHistoryItem
{
    public string Number { get; init; } = string.Empty;
    public string DisplayText { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public decimal Total { get; init; }
}

/// <summary>Presentation-only business details used by printed memo headers.</summary>
internal static class MemoBusinessConfiguration
{
    // The address is intentionally blank until the client confirms the correct print address.
    internal const string Name = "ALI TRADERS";
    internal const string Cell = "0336-1841496";
    internal const string Address = "";
}

public sealed class TransactionDocumentPreviewWindow : Window
{
    private const double PageWidthDip = 793.7;
    private const double PageHeightDip = 1122.5;
    private const double SingleMargin = 48;
    private const int FourUpLinesPerCell = 12;
    private const string Navy = "#17194A";
    private const string Orange = "#FF5A1F";
    private const string GrayCanvas = "#E8EBF1";
    private readonly FixedDocument _document;
    private readonly ScrollViewer _pageScroller;
    private readonly StackPanel _pagesPanel;
    private readonly List<Border> _pageHosts = [];
    private TextBlock _zoomText = null!;
    private double _zoomPercent = 100;

    public TransactionDocumentPreviewWindow(TransactionDocumentData data)
        : this(data, false)
    {
    }

    public TransactionDocumentPreviewWindow(TransactionDocumentData data, bool isThermal)
        : this(isThermal ? CreateThermalPreviewDocument(data) : CreateSinglePreviewDocument(data),
               isThermal ? $"Thermal Slip (80mm) {data.Number} — Preview" : $"{data.Title} {data.Number} — Preview")
    {
    }

    public TransactionDocumentPreviewWindow(IReadOnlyList<TransactionDocumentData> documents)
        : this(CreateFourUpPreviewDocument(documents), "Ali Traders Cash Memos — A4 4-up Preview")
    {
    }

    private TransactionDocumentPreviewWindow(FixedDocument document, string title)
    {
        _document = document;
        Title = title;
        Width = 1080;
        Height = 820;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        Background = Brush("#F5F6FA");
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 38,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false
        });

        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(BuildCaption());
        var toolbar = BuildToolbar();
        Grid.SetRow(toolbar, 1);
        root.Children.Add(toolbar);
        _pagesPanel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Background = Brush(GrayCanvas) };
        foreach (var pageContent in _document.Pages)
        {
            if (pageContent.Child is not FixedPage fixedPage) continue;
            var w = fixedPage.Width > 0 ? fixedPage.Width : PageWidthDip;
            var h = fixedPage.Height > 0 ? fixedPage.Height : PageHeightDip;
            var bitmap = RenderScreenPage(fixedPage, w, h);
            var image = new Image { Source = bitmap, Width = w, Height = h, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            var host = new Border
            {
                Tag = "PreviewPageHost",
                Width = w,
                Height = h,
                Margin = new Thickness(0, 14, 0, 14),
                Background = Brushes.White,
                BorderBrush = Brush("#CCD2DD"),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 12, ShadowDepth = 3, Opacity = .2 },
                Child = image
            };
            _pageHosts.Add(host);
            _pagesPanel.Children.Add(host);
        }
        _pageScroller = new ScrollViewer { Content = _pagesPanel, Background = Brush(GrayCanvas), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _pageScroller.PreviewMouseWheel += Viewer_PreviewMouseWheel;
        Grid.SetRow(_pageScroller, 2);
        root.Children.Add(_pageScroller);
        Content = root;
        PreviewKeyDown += PreviewWindow_PreviewKeyDown;
        Loaded += (_, _) => FitToWidth();
    }

    public FixedDocument PreviewDocument => _document;
    public IReadOnlyList<Border> PageHosts => _pageHosts;
    public double ZoomPercent => _zoomPercent;

    /// <summary>Sets the displayed page scale; the print paginator is not affected.</summary>
    public void SetZoomPercent(double percent)
    {
        _zoomPercent = Math.Clamp(percent, 25, 400);
        var transform = new ScaleTransform(_zoomPercent / 100d, _zoomPercent / 100d);
        foreach (var host in _pageHosts) host.LayoutTransform = transform;
        if (_zoomText is not null) _zoomText.Text = $"{_zoomPercent:F0}%";
        _pagesPanel.UpdateLayout();
    }

    public static FixedDocument CreateSinglePreviewDocument(TransactionDocumentData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var document = NewFixedDocument();
        var lines = data.Lines ?? Array.Empty<TransactionDocumentLine>();
        var chunks = SplitLineItems(lines, 354, 10, 22, 690, 30);
        for (var pageIndex = 0; pageIndex < chunks.Count; pageIndex++)
        {
            AddFixedPage(document, page => DrawSingleMemo(page, data, chunks[pageIndex], pageIndex > 0, pageIndex == chunks.Count - 1));
        }
        return document;
    }

    public static FixedDocument CreateFourUpPreviewDocument(IReadOnlyList<TransactionDocumentData> documents)
    {
        if (documents == null || documents.Count is < 1 or > 4)
            throw new ArgumentException("An A4 cash-memo sheet needs between one and four saved invoices.", nameof(documents));

        // Overflow is continued into the next fixed cell. It never shrinks below 8.5 pt or drops rows.
        var slots = new List<MemoSlot>();
        foreach (var data in documents)
        {
            var lines = data.Lines ?? Array.Empty<TransactionDocumentLine>();
            var chunks = SplitLineItems(lines, 128, 8.5, 15.3, 300, FourUpLinesPerCell);
            for (var chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
                slots.Add(new MemoSlot(data, chunks[chunkIndex], chunkIndex > 0, chunkIndex == chunks.Count - 1));
        }

        var document = NewFixedDocument();
        var pageCount = Math.Max(1, (int)Math.Ceiling(slots.Count / 4d));
        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var pageSlots = slots.Skip(pageIndex * 4).Take(4).ToArray();
            AddFixedPage(document, page => DrawFourUpPage(page, pageSlots));
        }
        return document;
    }

    private static FixedDocument NewFixedDocument() => new()
    {
        DocumentPaginator = { PageSize = new Size(PageWidthDip, PageHeightDip) }
    };

    private static void AddFixedPage(FixedDocument document, Action<FixedPage> draw)
    {
        var page = new FixedPage { Width = PageWidthDip, Height = PageHeightDip, Background = Brushes.White };
        TextOptions.SetTextFormattingMode(page, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(page, TextRenderingMode.ClearType);
        draw(page);
        page.Measure(new Size(PageWidthDip, PageHeightDip));
        page.Arrange(new Rect(0, 0, PageWidthDip, PageHeightDip));
        page.UpdateLayout();
        var content = new PageContent { Child = page };
        document.Pages.Add(content);
    }

    private static string FormatDocumentTitle(TransactionDocumentData data, bool continued)
    {
        var raw = (data.Title ?? string.Empty).Trim().ToUpperInvariant();
        string baseTitle;
        if (raw.Contains("PURCHASE") || string.Equals(data.PartyLabel, "Supplier", StringComparison.OrdinalIgnoreCase))
        {
            baseTitle = "PURCHASE BILL";
        }
        else
        {
            baseTitle = "SALE INVOICE";
        }
        return continued ? $"{baseTitle} (CONTINUED)" : baseTitle;
    }

    private static void DrawSingleMemo(FixedPage page, TransactionDocumentData data,
        IReadOnlyList<TransactionDocumentLine> lines, bool continued, bool isLastPage)
    {
        var x = SingleMargin;
        var width = PageWidthDip - SingleMargin * 2;
        var y = 28d;
        AddText(page, MemoBusinessConfiguration.Name, x, y, width, 32, 22, FontWeights.Bold, TextAlignment.Center); y += 33;
        AddBusinessContact(page, x, y, width, 14, 9.5, TextAlignment.Center); y += 20;

        var memoTitle = FormatDocumentTitle(data, continued);
        AddText(page, memoTitle, x, y, width, 24, 15, FontWeights.Bold, TextAlignment.Center); y += 30;

        // Structured Header: Left side Party Info, Right side Document Info
        var leftColWidth = width * 0.58;
        var rightColWidth = width * 0.42;
        var docHeaderY = y;

        // Document Number & Date on right
        var docNumberLabel = memoTitle.Contains("PURCHASE") ? "Bill No:" : "Invoice No:";
        AddText(page, $"{docNumberLabel} {data.Number}", x + leftColWidth, docHeaderY, rightColWidth, 18, 10, FontWeights.SemiBold, TextAlignment.Right);
        AddText(page, $"Date: {data.Date:dd/MM/yyyy}", x + leftColWidth, docHeaderY + 18, rightColWidth, 18, 9.5, FontWeights.Normal, TextAlignment.Right);

        // Party details on left (Party Name, Code, Phone, Address on clean separate lines to avoid collision)
        AddText(page, $"{data.PartyLabel}: {data.PartyName}", x, y, leftColWidth, 18, 10, FontWeights.SemiBold); y += 18;

        var partyMeta = JoinNonEmpty(
            data.PartyCode is null ? null : $"Code: {data.PartyCode}",
            data.AreaTerritory is null ? null : $"Area: {data.AreaTerritory}",
            data.Phone is null ? null : $"Phone: {data.Phone}"
        );
        if (!string.IsNullOrWhiteSpace(partyMeta))
        {
            AddText(page, partyMeta, x, y, leftColWidth, 16, 9, FontWeights.Normal);
            y += 18;
        }

        if (!string.IsNullOrWhiteSpace(data.Address))
        {
            AddText(page, $"Address: {data.Address}", x, y, leftColWidth, 16, 8.5, FontWeights.Normal);
            y += 18;
        }
        else y += 4;

        y = Math.Max(y, docHeaderY + 40);

        var columns = new[] { 340d, 52d, 88d, 63d, width - 543d };
        var headers = new[] { "Particulars", "Qty", "Rate (PKR)", "Disc %", "Amount (PKR)" };
        DrawTableHeader(page, headers, columns, x, y, 26, 9.5);
        y += 27;
        const double rowHeight = 22;
        foreach (var line in lines)
        {
            var values = new[]
            {
                line.ProductName,
                line.Quantity.ToString("N2", CultureInfo.InvariantCulture),
                line.UnitPrice.ToString("N2", CultureInfo.InvariantCulture),
                $"{line.DiscountPercentage.ToString("N2", CultureInfo.InvariantCulture)}%",
                line.LineTotal.ToString("N2", CultureInfo.InvariantCulture)
            };
            y += DrawTableRow(page, values, columns, x, y, rowHeight, 9.5, true);
        }
        if (!isLastPage)
        {
            AddText(page, "Continued on next page", x, Math.Min(y + 6, PageHeightDip - 40), width, 18, 9, FontWeights.Normal, TextAlignment.Right);
            return;
        }
        var totalsY = y + 14;
        DrawTotals(page, data, x, totalsY, width, 11);
        var footerY = totalsY + 5 * 20;
        if (!string.IsNullOrWhiteSpace(data.Remarks))
        {
            footerY += 16;
            AddText(page, $"Remarks: {data.Remarks}", x, footerY, width, 20, 9, FontWeights.Normal);
            footerY += 24;
        }
        footerY += 60; // Generous space (~45pt) between totals and signature lines for physical signing
        DrawSignatures(page, data, x, footerY, width, 9.5);
    }

    private static void DrawFourUpPage(FixedPage page, IReadOnlyList<MemoSlot> slots)
    {
        const double outer = 18;
        var cellWidth = (PageWidthDip - outer * 2) / 2;
        var cellHeight = (PageHeightDip - outer * 2) / 2;
        var cut = new Pen(Brush("#677184"), .7) { DashStyle = DashStyles.Dash };
        AddLine(page, PageWidthDip / 2, outer, PageWidthDip / 2, PageHeightDip - outer, cut.Brush, cut.Thickness, true);
        AddLine(page, outer, PageHeightDip / 2, PageWidthDip - outer, PageHeightDip / 2, cut.Brush, cut.Thickness, true);
        AddText(page, "✂", PageWidthDip / 2 - 8, PageHeightDip / 2 - 10, 18, 18, 12, FontWeights.Normal, TextAlignment.Center, "Segoe UI Symbol");
        for (var index = 0; index < 4; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var bounds = new Rect(outer + column * cellWidth, outer + row * cellHeight, cellWidth, cellHeight);
            var cellOutline = new System.Windows.Shapes.Rectangle
            {
                Width = bounds.Width - 1, Height = bounds.Height - 1,
                Stroke = Brush("#AAB2C0"), StrokeThickness = .6,
                StrokeDashArray = [2, 2], Fill = Brushes.Transparent
            };
            FixedPage.SetLeft(cellOutline, bounds.Left);
            FixedPage.SetTop(cellOutline, bounds.Top);
            page.Children.Add(cellOutline);
            if (index < slots.Count) DrawFourUpMemo(page, bounds, slots[index]);
        }
    }

    private static void DrawFourUpMemo(FixedPage page, Rect bounds, MemoSlot slot)
    {
        var data = slot.Data;
        var x = bounds.Left + 10;
        var y = bounds.Top + 8;
        var width = bounds.Width - 20;
        AddText(page, MemoBusinessConfiguration.Name, x, y, width, 18, 11, FontWeights.Bold, TextAlignment.Center); y += 18;
        AddBusinessContact(page, x, y, width, 12, 8, TextAlignment.Center); y += 14;

        var memoTitle = FormatDocumentTitle(data, slot.Continued);
        AddText(page, memoTitle, x, y, width, 15, 9, FontWeights.Bold, TextAlignment.Center); y += 16;
        AddText(page, $"{data.PartyLabel}: {data.PartyName}", x, y, width, 14, 8.5, FontWeights.SemiBold); y += 14;

        var partyDetails = JoinNonEmpty(
            data.PartyCode is null ? null : $"Code: {data.PartyCode}",
            data.Phone is null ? null : $"Ph: {data.Phone}"
        );
        if (!string.IsNullOrWhiteSpace(partyDetails))
        {
            AddText(page, partyDetails, x, y, width, 13, 8, FontWeights.Normal); y += 13;
        }

        AddText(page, $"No: {data.Number}       Date: {data.Date:dd/MM/yyyy}", x, y, width, 13, 8, FontWeights.Normal); y += 15;

        // Keep enough room for a right-aligned quantity formatted to two decimals.
        // Particulars remains the widest column while the explicit widths fill the cell.
        var widths = new[] { 124d, 40d, 53d, 40d, width - 257d };
        DrawTableHeader(page, ["Particulars", "Qty", "Rate PKR", "Disc %", "Amount PKR"], widths, x, y, 18, 8);
        y += 18;
        const double rowHeight = 15.3;
        foreach (var line in slot.Lines)
        {
            y += DrawTableRow(page,
                [line.ProductName, line.Quantity.ToString("N2", CultureInfo.InvariantCulture), line.UnitPrice.ToString("N2", CultureInfo.InvariantCulture), $"{line.DiscountPercentage:N2}%", line.LineTotal.ToString("N2", CultureInfo.InvariantCulture)],
                widths, x, y, rowHeight, 8, false);
        }

        if (slot.IsLastChunk)
        {
            var totalsY = bounds.Bottom - 118;
            DrawTotals(page, data, x, totalsY, width, 8);

            var partySignLabel = string.Equals(data.PartyLabel, "Supplier", StringComparison.OrdinalIgnoreCase)
                ? "Supplier Signature"
                : "Customer Signature";

            AddText(page, $"{partySignLabel} __________________", x, bounds.Bottom - 20, width * .50, 13, 7.5, FontWeights.Normal);
            AddText(page, "Prep By: ________   Chk By: ________", x + width * .50, bounds.Bottom - 20, width * .50, 13, 7.5, FontWeights.Normal, TextAlignment.Right);
        }
    }

    private static void DrawTableHeader(FixedPage page, IReadOnlyList<string> labels, IReadOnlyList<double> widths,
        double x, double y, double height, double pointSize)
    {
        AddRectangle(page, x, y, widths.Sum(), height, Brush("#EDF1F6"), Brush("#7F8997"), .7);
        var cursor = x;
        for (var i = 0; i < labels.Count; i++)
        {
            AddText(page, labels[i], cursor + 3, y + 1, widths[i] - 6, height - 2, pointSize, FontWeights.SemiBold, i == 1 ? TextAlignment.Left : TextAlignment.Center);
            cursor += widths[i];
        }
    }

    private static double DrawTableRow(FixedPage page, IReadOnlyList<string> values, IReadOnlyList<double> widths,
        double x, double y, double height, double pointSize, bool drawRule)
    {
        var rowHeight = Math.Max(height, MeasureWrappedTextHeight(values[0], widths[0] - 6, pointSize) + 2);
        var cursor = x;
        for (var i = 0; i < values.Count; i++)
        {
            var isNumber = i > 0;
            AddText(page, values[i], cursor + 3, y + 1, widths[i] - 6, rowHeight - 2,
                pointSize, FontWeights.Normal, isNumber ? TextAlignment.Right : TextAlignment.Left,
                trim: isNumber);
            cursor += widths[i];
        }
        if (drawRule)
            page.Children.Add(new System.Windows.Shapes.Line { X1 = x, X2 = x + widths.Sum(), Y1 = y + rowHeight, Y2 = y + rowHeight, Stroke = Brush("#D5DAE3"), StrokeThickness = .45 });
        return rowHeight;
    }

    private static double MeasureWrappedTextHeight(string text, double width, double pointSize)
    {
        var probe = new TextBlock { Text = text, Width = width, FontFamily = new FontFamily("Segoe UI"), FontSize = pointSize * 96d / 72d, TextWrapping = TextWrapping.Wrap };
        probe.Measure(new Size(width, double.PositiveInfinity));
        return Math.Ceiling(probe.DesiredSize.Height);
    }

    private static List<IReadOnlyList<TransactionDocumentLine>> SplitLineItems(IReadOnlyList<TransactionDocumentLine> lines,
        double nameWidth, double pointSize, double minimumRowHeight, double maximumChunkHeight, int maximumLineCount)
    {
        var result = new List<IReadOnlyList<TransactionDocumentLine>>();
        var current = new List<TransactionDocumentLine>();
        var currentHeight = 0d;
        foreach (var line in lines)
        {
            var lineHeight = Math.Max(minimumRowHeight, MeasureWrappedTextHeight(line.ProductName, nameWidth, pointSize) + 2);
            if (current.Count > 0 && (current.Count >= maximumLineCount || currentHeight + lineHeight > maximumChunkHeight))
            {
                result.Add(current.ToArray());
                current.Clear();
                currentHeight = 0;
            }
            current.Add(line);
            currentHeight += lineHeight;
        }
        if (current.Count > 0 || result.Count == 0) result.Add(current.ToArray());
        return result;
    }

    private static void DrawTotals(FixedPage page, TransactionDocumentData data, double x, double y, double width, double pointSize)
    {
        var amountWidth = Math.Min(340, width * .72);
        var left = x + width - amountWidth;
        var rowHeight = pointSize <= 9 ? 13 : 20;
        var labels = new[] { "Gross Amount", "Discount", "NET AMOUNT", "Amount Paid", "Balance Due" };
        var amounts = new[] { Money(data.SubTotal), Money(data.Discount), Money(data.Total), Money(data.Paid), Money(data.Balance) };
        for (var i = 0; i < labels.Length; i++)
        {
            var isNet = i == 2;
            var isGross = i == 0;
            var rowY = y + i * rowHeight;
            var currentPointSize = isNet ? pointSize + 1.5 : pointSize;
            var weight = isNet ? FontWeights.Bold : (isGross ? FontWeights.SemiBold : FontWeights.Normal);

            if (isNet)
            {
                // Prominent dedicated badge for NET AMOUNT: Soft tinted background with crisp navy outline
                AddRectangle(page, left, rowY - 1, amountWidth, rowHeight + 2, Brush("#EDF2F9"), Brush(Navy), 1.2);
            }

            AddText(page, labels[i], left + 6, rowY, amountWidth * .54, rowHeight, currentPointSize, weight);
            AddText(page, amounts[i], left + amountWidth * .54, rowY, amountWidth * .43, rowHeight, currentPointSize, weight, TextAlignment.Right);
        }
    }

    private static void DrawSignatures(FixedPage page, TransactionDocumentData data, double x, double y, double width, double pointSize)
    {
        var partySignLabel = string.Equals(data.PartyLabel, "Supplier", StringComparison.OrdinalIgnoreCase)
            ? "Supplier Signature"
            : "Customer Signature";

        var labels = new[] { "Prepared By", "Checked By", partySignLabel };
        var segment = width / 3;
        for (var i = 0; i < labels.Length; i++)
        {
            AddLine(page, x + i * segment + 12, y, x + (i + 1) * segment - 12, y, Brush("#4B5563"), .8);
            AddText(page, labels[i], x + i * segment, y + 6, segment, 18, pointSize, FontWeights.Normal, TextAlignment.Center);
        }
    }

    private static void AddBusinessContact(FixedPage page, double x, double y, double width, double height, double pointSize, TextAlignment alignment)
    {
        var contact = string.IsNullOrWhiteSpace(MemoBusinessConfiguration.Address)
            ? $"Cell: {MemoBusinessConfiguration.Cell}"
            : $"{MemoBusinessConfiguration.Address}  |  Cell: {MemoBusinessConfiguration.Cell}";
        AddText(page, contact, x, y, width, height, pointSize, FontWeights.Normal, alignment);
    }

    private static void AddRectangle(FixedPage page, double x, double y, double width, double height, Brush fill, Brush stroke, double thickness)
    {
        var rectangle = new System.Windows.Shapes.Rectangle { Width = width, Height = height, Fill = fill, Stroke = stroke, StrokeThickness = thickness };
        FixedPage.SetLeft(rectangle, x);
        FixedPage.SetTop(rectangle, y);
        page.Children.Add(rectangle);
    }

    private static void AddLine(FixedPage page, double x1, double y1, double x2, double y2, Brush stroke, double thickness, bool dashed = false)
    {
        var line = new System.Windows.Shapes.Line
        {
            X1 = 0, Y1 = 0, X2 = x2 - x1, Y2 = y2 - y1,
            Stroke = stroke, StrokeThickness = thickness,
            StrokeDashArray = dashed ? [3, 3] : null
        };
        FixedPage.SetLeft(line, x1);
        FixedPage.SetTop(line, y1);
        page.Children.Add(line);
    }

    private static void AddText(FixedPage page, string? value, double x, double y, double width, double height,
        double pointSize, FontWeight weight, TextAlignment alignment = TextAlignment.Left,
        string? fontFamily = null, bool trim = false)
    {
        if (string.IsNullOrEmpty(value)) return;
        var block = new TextBlock
        {
            Text = trim ? value : value,
            FontFamily = new FontFamily(fontFamily ?? "Segoe UI"),
            FontSize = pointSize * 96d / 72d,
            FontWeight = weight,
            Foreground = Brushes.Black,
            TextAlignment = alignment,
            TextTrimming = trim ? TextTrimming.CharacterEllipsis : TextTrimming.None,
            TextWrapping = trim ? TextWrapping.NoWrap : TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Height = height,
            Width = width,
            ToolTip = value
        };
        FixedPage.SetLeft(block, x);
        FixedPage.SetTop(block, y);
        page.Children.Add(block);
    }

    private static string Money(decimal amount) => $"PKR {amount.ToString("N2", CultureInfo.InvariantCulture)}";
    private static string JoinNonEmpty(params string?[] values) => string.Join("     ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private sealed record MemoSlot(TransactionDocumentData Data, IReadOnlyList<TransactionDocumentLine> Lines, bool Continued, bool IsLastChunk);

    public static FixedDocument CreateThermalPreviewDocument(TransactionDocumentData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        const double thermalWidth = 302.36; // 80mm @ 96 DPI
        var lines = data.Lines ?? Array.Empty<TransactionDocumentLine>();
        double calculatedHeight = Math.Max(420, 260 + lines.Count * 36 + 140);

        var document = new FixedDocument
        {
            DocumentPaginator = { PageSize = new Size(thermalWidth, calculatedHeight) }
        };

        var page = new FixedPage
        {
            Width = thermalWidth,
            Height = calculatedHeight,
            Background = Brushes.White
        };
        TextOptions.SetTextFormattingMode(page, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(page, TextRenderingMode.ClearType);

        DrawThermalSlip(page, data, thermalWidth, calculatedHeight);

        page.Measure(new Size(thermalWidth, calculatedHeight));
        page.Arrange(new Rect(0, 0, thermalWidth, calculatedHeight));
        page.UpdateLayout();

        document.Pages.Add(new PageContent { Child = page });
        return document;
    }

    private static void DrawThermalSlip(FixedPage page, TransactionDocumentData data, double width, double height)
    {
        const double margin = 12;
        var printableWidth = width - margin * 2;
        var y = 14d;

        // Header: Shop Name & Title
        AddText(page, MemoBusinessConfiguration.Name, margin, y, printableWidth, 24, 15, FontWeights.Bold, TextAlignment.Center);
        y += 24;
        AddText(page, "FMCG Wholesale & Distribution", margin, y, printableWidth, 16, 9, FontWeights.SemiBold, TextAlignment.Center);
        y += 18;
        if (!string.IsNullOrWhiteSpace(MemoBusinessConfiguration.Cell))
        {
            AddText(page, $"Cell: {MemoBusinessConfiguration.Cell}", margin, y, printableWidth, 16, 9, FontWeights.Normal, TextAlignment.Center);
            y += 18;
        }

        AddLine(page, margin, y, margin + printableWidth, y, Brushes.Black, 1, true);
        y += 8;

        var titleLabel = FormatDocumentTitle(data, false);
        AddText(page, $"{titleLabel}: {data.Number}", margin, y, printableWidth, 18, 10, FontWeights.Bold, TextAlignment.Left);
        y += 18;
        AddText(page, $"Date: {data.Date:dd/MM/yyyy HH:mm}", margin, y, printableWidth, 16, 8.5, FontWeights.Normal, TextAlignment.Left);
        y += 18;
        AddText(page, $"{data.PartyLabel}: {data.PartyName}", margin, y, printableWidth, 18, 9, FontWeights.SemiBold, TextAlignment.Left);
        y += 20;

        AddLine(page, margin, y, margin + printableWidth, y, Brushes.Black, 1, true);
        y += 6;

        // Table Header
        AddText(page, "ITEM", margin, y, printableWidth * 0.44, 16, 8, FontWeights.Bold, TextAlignment.Left);
        AddText(page, "QTY", margin + printableWidth * 0.44, y, printableWidth * 0.14, 16, 8, FontWeights.Bold, TextAlignment.Right);
        AddText(page, "RATE (PKR)", margin + printableWidth * 0.58, y, printableWidth * 0.20, 16, 8, FontWeights.Bold, TextAlignment.Right);
        AddText(page, "TOTAL (PKR)", margin + printableWidth * 0.78, y, printableWidth * 0.22, 16, 8, FontWeights.Bold, TextAlignment.Right);
        y += 18;
        AddLine(page, margin, y, margin + printableWidth, y, Brushes.Gray, 0.75, false);
        y += 6;

        // Line Items
        foreach (var line in data.Lines ?? Array.Empty<TransactionDocumentLine>())
        {
            AddText(page, line.ProductName, margin, y, printableWidth, 16, 8.5, FontWeights.SemiBold, TextAlignment.Left, trim: true);
            y += 16;
            AddText(page, $"{line.Quantity:N0}", margin + printableWidth * 0.44, y, printableWidth * 0.14, 15, 8, FontWeights.Normal, TextAlignment.Right);
            AddText(page, $"{line.UnitPrice:N2}", margin + printableWidth * 0.58, y, printableWidth * 0.20, 15, 8, FontWeights.Normal, TextAlignment.Right);
            AddText(page, $"{line.LineTotal:N2}", margin + printableWidth * 0.78, y, printableWidth * 0.22, 15, 8.5, FontWeights.Bold, TextAlignment.Right);
            y += 18;
        }

        AddLine(page, margin, y, margin + printableWidth, y, Brushes.Black, 1, true);
        y += 8;

        // Totals Block
        void ThermalRow(string label, decimal amount, bool isBold = false, double pointSize = 9)
        {
            AddText(page, label, margin + printableWidth * 0.25, y, printableWidth * 0.38, 18, pointSize, isBold ? FontWeights.Bold : FontWeights.Normal, TextAlignment.Left);
            AddText(page, $"PKR {amount:N2}", margin + printableWidth * 0.63, y, printableWidth * 0.37, 18, pointSize, isBold ? FontWeights.Bold : FontWeights.Normal, TextAlignment.Right);
            y += 18;
        }

        ThermalRow("Subtotal:", data.SubTotal);
        if (data.Discount > 0) ThermalRow("Discount:", data.Discount);
        ThermalRow("TOTAL:", data.Total, isBold: true, pointSize: 10.5);
        ThermalRow("Paid:", data.Paid);
        ThermalRow("Balance Due:", data.Balance, isBold: true);

        y += 8;
        AddLine(page, margin, y, margin + printableWidth, y, Brushes.Black, 1, true);
        y += 12;

        AddText(page, "Thank you for your business!", margin, y, printableWidth, 18, 9, FontWeights.SemiBold, TextAlignment.Center);
        y += 16;
        AddText(page, "Ali Traders POS · System by ComsTech", margin, y, printableWidth, 14, 7.5, FontWeights.Normal, TextAlignment.Center);
    }

    public static void DirectPrint(TransactionDocumentData data, bool isThermal = false)
    {
        var doc = isThermal ? CreateThermalPreviewDocument(data) : CreateSinglePreviewDocument(data);
        var printDialog = new PrintDialog();
        printDialog.PrintDocument(doc.DocumentPaginator, isThermal ? "Thermal Receipt" : data.Title);
    }

    private static BitmapSource RenderScreenPage(FixedPage page, double width, double height)
    {
        const double scale = 2.0; // 2x super-sampling (192 DPI) ensures vector text renders razor sharp at 100%-150% zoom
        var pixelWidth = (int)Math.Ceiling(width * scale);
        var pixelHeight = (int)Math.Ceiling(height * scale);

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(page);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource RenderScreenPage(FixedPage page) =>
        RenderScreenPage(page, PageWidthDip, PageHeightDip);

    private UIElement BuildCaption()
    {
        var caption = new Grid { Background = Brush(Navy) };
        caption.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        caption.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock { Text = Title, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 8, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        title.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        };
        caption.Children.Add(title);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(CaptionButton("—", "Minimize", (_, _) => WindowState = WindowState.Minimized));
        buttons.Children.Add(CaptionButton("□", "Maximize / restore", (_, _) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized));
        buttons.Children.Add(CaptionButton("×", "Close", (_, _) => Close(), true));
        Grid.SetColumn(buttons, 1);
        caption.Children.Add(buttons);
        return caption;
    }

    private Button CaptionButton(string text, string tip, RoutedEventHandler click, bool close = false)
    {
        var button = new Button
        {
            Content = text, Width = 46, Height = 32, Margin = new Thickness(0, 3, 0, 3), Padding = new Thickness(0),
            Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0),
            FontSize = 15, ToolTip = tip, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Click += click;
        button.MouseEnter += (_, _) => button.Background = Brush(close ? "#E81123" : "#3B3D68");
        button.MouseLeave += (_, _) => button.Background = Brushes.Transparent;
        WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        return button;
    }

    private UIElement BuildToolbar()
    {
        var bar = new DockPanel { Margin = new Thickness(12, 8, 12, 8), LastChildFill = true };
        var print = new Button { Content = "Print", MinWidth = 112, Height = 36, Padding = new Thickness(18, 6, 18, 6), Background = Brush(Navy), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0), ToolTip = "Print this saved memo (Ctrl+P)" };
        print.Click += Print_Click;
        DockPanel.SetDock(print, Dock.Right);
        bar.Children.Add(print);
        var zoom = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        zoom.Children.Add(ToolButton("Fit width", "Fit to width", (_, _) => FitToWidth()));
        zoom.Children.Add(ToolButton("Fit page", "Fit to page", (_, _) => FitToPage()));
        zoom.Children.Add(ToolButton("−", "Zoom out", (_, _) => ChangeZoom(-10)));
        _zoomText = new TextBlock { Text = "100%", Width = 50, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush(Navy), FontWeight = FontWeights.SemiBold };
        zoom.Children.Add(_zoomText);
        zoom.Children.Add(ToolButton("+", "Zoom in", (_, _) => ChangeZoom(10)));
        zoom.Children.Add(ToolButton("100%", "Actual size", (_, _) => SetZoomPercent(100)));
        bar.Children.Add(zoom);
        return bar;
    }

    private static Button ToolButton(string text, string tip, RoutedEventHandler click)
    {
        var button = new Button { Content = text, ToolTip = tip, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 0), MinHeight = 32, Background = Brushes.White, Foreground = Brush(Navy), BorderBrush = Brush("#BCC5D3") };
        button.Click += click;
        return button;
    }

    private void FitToWidth()
    {
        if (_pageScroller.ViewportWidth <= 0) return;
        var docWidth = _document.DocumentPaginator.PageSize.Width > 0 ? _document.DocumentPaginator.PageSize.Width : PageWidthDip;
        SetZoomPercent((_pageScroller.ViewportWidth - 60) / docWidth * 100d);
    }

    private void FitToPage()
    {
        if (_pageScroller.ViewportWidth <= 0 || _pageScroller.ViewportHeight <= 0) return;
        var docWidth = _document.DocumentPaginator.PageSize.Width > 0 ? _document.DocumentPaginator.PageSize.Width : PageWidthDip;
        var docHeight = _document.DocumentPaginator.PageSize.Height > 0 ? _document.DocumentPaginator.PageSize.Height : PageHeightDip;
        var zoom = Math.Min((_pageScroller.ViewportWidth - 60) / docWidth, (_pageScroller.ViewportHeight - 40) / docHeight) * 100d;
        SetZoomPercent(zoom);
    }

    private void ChangeZoom(double delta)
    {
        SetZoomPercent(_zoomPercent + delta);
    }

    private void Viewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        ChangeZoom(e.Delta > 0 ? 10 : -10);
        e.Handled = true;
    }

    private void PreviewWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.P) { Print_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key is Key.Add or Key.OemPlus) { ChangeZoom(10); e.Handled = true; }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key is Key.Subtract or Key.OemMinus) { ChangeZoom(-10); e.Handled = true; }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key is Key.D0 or Key.NumPad0) { SetZoomPercent(100); e.Handled = true; }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return;
            if (dialog.PrintTicket is not null)
                dialog.PrintTicket.PageMediaSize = new System.Printing.PageMediaSize(System.Printing.PageMediaSizeName.ISOA4);
            dialog.PrintDocument(_document.DocumentPaginator, Title);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The document could not be printed. The saved transaction is unchanged.\n\n{ex.Message}",
                "Print failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
