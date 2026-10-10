using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using FluentAssertions;
using SOFT2027.UI.Views;

namespace SOFT2027.Tests;

public sealed class TransactionDocumentPreviewWindowTests
{
    [Fact]
    public void FourDistinctSavedInvoices_FitOnePortraitA4Sheet()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var documents = new List<TransactionDocumentData>();
                for (var index = 1; index <= 4; index++)
                {
                    documents.Add(new TransactionDocumentData
                    {
                        Title = "Cash Memo", Number = $"INV-{index}", Date = new DateTime(2026, 10, 10),
                        PartyLabel = "Customer", PartyName = $"Customer {index}", PartyCode = $"C-{index}",
                        Lines = new[]
                        {
                            new TransactionDocumentLine { ProductCode = "P-1", ProductName = "Cooking Oil 1L", Quantity = 3, UnitPrice = 100, DiscountPercentage = 5, LineTotal = 285 },
                            new TransactionDocumentLine { ProductCode = "P-2", ProductName = "Basmati Rice 5kg", Quantity = 2, UnitPrice = 500, DiscountPercentage = 0, LineTotal = 1000 }
                        },
                        SubTotal = 1300, Discount = 15, Total = 1285, Paid = 1000, Balance = 285, PaymentSource = "Cash"
                    });
                }

                var window = new TransactionDocumentPreviewWindow(documents);
                var reader = (FlowDocumentReader)((DockPanel)window.Content).Children[1];
                var text = new TextRange(reader.Document.ContentStart, reader.Document.ContentEnd).Text;
                for (var index = 1; index <= 4; index++) text.Should().Contain($"INV-{index}");
                var paginator = ((IDocumentPaginatorSource)reader.Document).DocumentPaginator;
                paginator.ComputePageCount();
                paginator.PageCount.Should().Be(1);
                window.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new InvalidOperationException("A4 cash memo sheet pagination failed.", failure);
    }
}
