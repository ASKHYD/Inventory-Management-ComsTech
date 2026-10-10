using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

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

public sealed class TransactionDocumentPreviewWindow : Window
{
    private readonly FlowDocument _document;

    public TransactionDocumentPreviewWindow(TransactionDocumentData data)
        : this(data.Title, data.Number, data.Date, data.PartyLabel, data.PartyName, data.Phone, data.Address,
            data.ContactPerson, data.PartyCode, data.Lines, data.SubTotal, data.Discount, data.Total, data.Paid, data.Balance, data.PaymentSource, data.Remarks)
    {
    }

    public TransactionDocumentPreviewWindow(IReadOnlyList<TransactionDocumentData> documents)
    {
        if (documents == null || documents.Count is < 1 or > 4)
            throw new ArgumentException("An A4 cash-memo sheet needs between one and four saved invoices.", nameof(documents));
        Title = "Ali Traders Cash Memos — A4 4-up Preview";
        Width = 1000; Height = 760; MinWidth = 760; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _document = CreateMemoSheet(documents);
        var root = new DockPanel();
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12) };
        var printButton = new Button { Content = "Print…", Padding = new Thickness(18, 8, 18, 8), MinWidth = 100 };
        printButton.Click += Print_Click;
        toolbar.Children.Add(printButton);
        toolbar.Children.Add(new TextBlock { Text = "A4 portrait · up to four separate saved cash memos", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Foreground = Brushes.DimGray });
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        root.Children.Add(new FlowDocumentReader { Document = _document, ViewingMode = FlowDocumentReaderViewingMode.Page, IsPrintEnabled = false, IsFindEnabled = false, Zoom = 80 });
        Content = root;
    }

    private static FlowDocument CreateMemoSheet(IReadOnlyList<TransactionDocumentData> documents)
    {
        var doc = new FlowDocument { PagePadding = new Thickness(24), PageWidth = 793.7, PageHeight = 1122.5, ColumnWidth = 745.7, FontFamily = new FontFamily("Segoe UI"), FontSize = 8, Foreground = Brushes.Black };
        var sheet = new Table { CellSpacing = 10 };
        sheet.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        sheet.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        var group = new TableRowGroup(); sheet.RowGroups.Add(group);
        for (var rowIndex = 0; rowIndex < 2; rowIndex++)
        {
            var row = new TableRow(); group.Rows.Add(row);
            for (var colIndex = 0; colIndex < 2; colIndex++)
            {
                var index = rowIndex * 2 + colIndex;
                var cell = new TableCell { Padding = new Thickness(8), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.6) };
                if (index < documents.Count) AddMemo(cell, documents[index]);
                else cell.Blocks.Add(new Paragraph(new Run("")) { FontSize = 1, Margin = new Thickness(0) });
                row.Cells.Add(cell);
            }
        }
        doc.Blocks.Add(sheet);
        return doc;
    }

    private static void AddMemo(TableCell cell, TransactionDocumentData data)
    {
        cell.Blocks.Add(new Paragraph(new Run("ALI TRADERS")) { FontSize = 12, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0) });
        cell.Blocks.Add(new Paragraph(new Run("Latifabad No. 11  |  Cell: 0336-1841496")) { FontSize = 7, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 3) });
        cell.Blocks.Add(new Paragraph(new Run("CASH MEMO")) { FontSize = 9, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 4) });
        cell.Blocks.Add(new Paragraph(new Run($"No: {data.Number}     Date: {data.Date:d}")) { Margin = new Thickness(0) });
        cell.Blocks.Add(new Paragraph(new Run($"Customer: {data.PartyName}     Code: {data.PartyCode}")) { Margin = new Thickness(0, 0, 0, 4) });
        var items = new Table { CellSpacing = 0 };
        foreach (var width in new[] { 4, 1, 1.2, 0.8, 1.5 }) items.Columns.Add(new TableColumn { Width = new GridLength(width, GridUnitType.Star) });
        var rows = new TableRowGroup(); items.RowGroups.Add(rows);
        var heading = new TableRow { FontWeight = FontWeights.Bold };
        foreach (var label in new[] { "Particulars", "Qty", "Rate", "Disc", "Amount" }) heading.Cells.Add(new TableCell(new Paragraph(new Run(label)) { Margin = new Thickness(1) }));
        rows.Rows.Add(heading);
        foreach (var line in data.Lines)
        {
            var lineRow = new TableRow();
            lineRow.Cells.Add(new TableCell(new Paragraph(new Run(line.ProductName)) { Margin = new Thickness(1) }));
            lineRow.Cells.Add(new TableCell(new Paragraph(new Run(line.Quantity.ToString("N2", CultureInfo.CurrentCulture))) { Margin = new Thickness(1), TextAlignment = TextAlignment.Right }));
            lineRow.Cells.Add(new TableCell(new Paragraph(new Run(line.UnitPrice.ToString("N2", CultureInfo.CurrentCulture))) { Margin = new Thickness(1), TextAlignment = TextAlignment.Right }));
            lineRow.Cells.Add(new TableCell(new Paragraph(new Run(line.DiscountPercentage.ToString("N0", CultureInfo.CurrentCulture) + "%")) { Margin = new Thickness(1), TextAlignment = TextAlignment.Right }));
            lineRow.Cells.Add(new TableCell(new Paragraph(new Run(line.LineTotal.ToString("N2", CultureInfo.CurrentCulture))) { Margin = new Thickness(1), TextAlignment = TextAlignment.Right }));
            rows.Rows.Add(lineRow);
        }
        cell.Blocks.Add(items);
        cell.Blocks.Add(new Paragraph(new Run($"Gross: PKR {data.SubTotal:N2}     Discount: PKR {data.Discount:N2}     Net: PKR {data.Total:N2}")) { TextAlignment = TextAlignment.Right, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 0) });
        cell.Blocks.Add(new Paragraph(new Run($"Paid: PKR {data.Paid:N2}     Balance: PKR {data.Balance:N2}")) { TextAlignment = TextAlignment.Right, Margin = new Thickness(0) });
        cell.Blocks.Add(new Paragraph(new Run("Customer Signature __________________     Prepared By __________   Checked By __________")) { FontSize = 6.5, Margin = new Thickness(0, 8, 0, 0) });
    }

    public TransactionDocumentPreviewWindow(
        string documentTitle, string number, DateTime date, string partyLabel,
        string partyName, string? phone, string? address, string? contactPerson, string? partyCode, IReadOnlyList<TransactionDocumentLine> lines,
        decimal subTotal, decimal discount, decimal total, decimal paid, decimal balance,
        string paymentSource, string? remarks)
    {
        Title = $"{documentTitle} {number} — Preview";
        Width = 1000;
        Height = 760;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _document = CreateDocument(documentTitle, number, date, partyLabel, partyName, phone, address, contactPerson, partyCode,
            lines, subTotal, discount, total, paid, balance, paymentSource, remarks);

        var root = new DockPanel();
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12) };
        var printButton = new Button { Content = "Print…", Padding = new Thickness(18, 8, 18, 8), MinWidth = 100 };
        printButton.Click += Print_Click;
        toolbar.Children.Add(printButton);
        toolbar.Children.Add(new TextBlock
        {
            Text = "Choose a printer and paper size in the Windows print dialog.",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            Foreground = Brushes.DimGray
        });
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(new FlowDocumentReader
        {
            Document = _document,
            ViewingMode = FlowDocumentReaderViewingMode.Page,
            IsPrintEnabled = false,
            IsFindEnabled = false,
            Zoom = 80
        });
        Content = root;
    }

    private static FlowDocument CreateDocument(string title, string number, DateTime date, string partyLabel,
        string partyName, string? phone, string? address, string? contactPerson, string? partyCode, IReadOnlyList<TransactionDocumentLine> lines,
        decimal subTotal, decimal discount, decimal total, decimal paid, decimal balance,
        string paymentSource, string? remarks)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(48),
            PageWidth = 793.7,
            PageHeight = 1122.5,
            ColumnWidth = 697.7,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = Brushes.Black
        };
        var content = new Section { Margin = new Thickness(0) };
        document.Blocks.Add(content);
        var business = new Paragraph(new Run("ALI TRADERS"))
        {
            FontSize = 23, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        content.Blocks.Add(business);
        content.Blocks.Add(new Paragraph(new Run("Latifabad No. 11  |  Cell: 0336-1841496"))
        {
            FontSize = 10, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 10)
        });
        content.Blocks.Add(new Paragraph(new Run(title.ToUpperInvariant()))
        {
            FontSize = 15, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 18)
        });

        var info = new Table { CellSpacing = 0 };
        info.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        info.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        var infoRow = new TableRow();
        infoRow.Cells.Add(new TableCell(new Paragraph(new Run($"{partyLabel}: {partyName}{(string.IsNullOrWhiteSpace(partyCode) ? string.Empty : $"  |  Code: {partyCode}")}"))));
        infoRow.Cells.Add(new TableCell(new Paragraph(new Run($"{title} No: {number}\nDate: {date:d}")))
        { TextAlignment = TextAlignment.Right });
        var infoGroup = new TableRowGroup();
        infoGroup.Rows.Add(infoRow);
        info.RowGroups.Add(infoGroup);
        content.Blocks.Add(info);
        if (!string.IsNullOrWhiteSpace(contactPerson)) content.Blocks.Add(new Paragraph(new Run($"Contact: {contactPerson}")) { Margin = new Thickness(0) });
        if (!string.IsNullOrWhiteSpace(phone)) content.Blocks.Add(new Paragraph(new Run($"Phone: {phone}")) { Margin = new Thickness(0) });
        if (!string.IsNullOrWhiteSpace(address)) content.Blocks.Add(new Paragraph(new Run($"Address: {address}")) { Margin = new Thickness(0, 0, 0, 12) });
        else content.Blocks.Add(new Paragraph { Margin = new Thickness(0, 0, 0, 12) });

        var table = new Table { CellSpacing = 0 };
        double[] widths = [70, 1, 70, 75, 60, 85];
        foreach (var width in widths)
            table.Columns.Add(new TableColumn { Width = width == 1 ? new GridLength(1, GridUnitType.Star) : new GridLength(width) });
        var heading = new TableRow { FontWeight = FontWeights.Bold };
        foreach (var label in new[] { "Code", "Product", "Qty", "Rate PKR", "Disc %", "Amount PKR" })
            heading.Cells.Add(new TableCell(new Paragraph(new Run(label)) { Margin = new Thickness(5, 7, 5, 7) })
            { Background = new SolidColorBrush(Color.FromRgb(235, 240, 246)), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 0.5) });
        var tableGroup = new TableRowGroup();
        tableGroup.Rows.Add(heading);
        table.RowGroups.Add(tableGroup);
        foreach (var line in lines)
        {
            var row = new TableRow();
            row.Cells.Add(Cell(line.ProductCode));
            row.Cells.Add(Cell(line.ProductName));
            row.Cells.Add(Cell(line.Quantity.ToString("N2", CultureInfo.CurrentCulture), TextAlignment.Right));
            row.Cells.Add(Cell(Money(line.UnitPrice), TextAlignment.Right));
            row.Cells.Add(Cell(line.DiscountPercentage.ToString("N2", CultureInfo.CurrentCulture), TextAlignment.Right));
            row.Cells.Add(Cell(Money(line.LineTotal), TextAlignment.Right));
            tableGroup.Rows.Add(row);
        }
        content.Blocks.Add(table);

        var totals = new Paragraph
        {
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 16, 4, 0),
            LineHeight = 20
        };
        totals.Inlines.Add(new Run($"Subtotal: {Money(subTotal)}\nDiscount: {Money(discount)}\nTotal: {Money(total)}\nAmount Paid: {Money(paid)}\nBalance Due: {Money(balance)}")
        { FontWeight = FontWeights.SemiBold });
        content.Blocks.Add(totals);
        if (!string.IsNullOrWhiteSpace(remarks))
            content.Blocks.Add(new Paragraph(new Run($"Remarks: {remarks}")) { Margin = new Thickness(0, 18, 0, 0) });
        var signatures = new Table { CellSpacing = 18, Margin = new Thickness(0, 40, 0, 0) };
        signatures.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
        signatures.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        signatures.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        var signatureRow = new TableRow();
        signatureRow.Cells.Add(SignatureCell($"{partyLabel} Signature"));
        signatureRow.Cells.Add(SignatureCell("Prepared By"));
        signatureRow.Cells.Add(SignatureCell("Checked By"));
        var signaturesGroup = new TableRowGroup();
        signaturesGroup.Rows.Add(signatureRow);
        signatures.RowGroups.Add(signaturesGroup);
        content.Blocks.Add(signatures);
        return document;
    }

    private static TableCell Cell(string text, TextAlignment alignment = TextAlignment.Left)
    {
        var paragraph = new Paragraph(new Run(text)) { Margin = new Thickness(5, 7, 5, 7), TextAlignment = alignment };
        return new TableCell(paragraph) { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 0.5) };
    }

    private static string Money(decimal amount) => $"PKR {amount.ToString("N2", CultureInfo.CurrentCulture)}";

    private static TableCell SignatureCell(string label) => new(new Paragraph(new Run($"____________________\n{label}"))
    {
        TextAlignment = TextAlignment.Center, FontSize = 9, Margin = new Thickness(0)
    });

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return;
            var paginator = ((IDocumentPaginatorSource)_document).DocumentPaginator;
            if (dialog.PrintableAreaWidth > 0 && dialog.PrintableAreaHeight > 0)
            {
                _document.PageWidth = dialog.PrintableAreaWidth + _document.PagePadding.Left + _document.PagePadding.Right;
                _document.PageHeight = dialog.PrintableAreaHeight + _document.PagePadding.Top + _document.PagePadding.Bottom;
                _document.ColumnWidth = dialog.PrintableAreaWidth;
                paginator = ((IDocumentPaginatorSource)_document).DocumentPaginator;
            }
            dialog.PrintDocument(paginator, Title);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"The document could not be printed. The saved transaction is unchanged.\n\n{ex.Message}",
                "Print failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
