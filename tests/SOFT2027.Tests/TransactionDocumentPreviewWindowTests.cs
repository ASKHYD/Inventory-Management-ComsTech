using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using FluentAssertions;
using Moq;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.ViewModels;
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
                var doc = window.PreviewDocument;
                var paginator = doc.DocumentPaginator;
                paginator.ComputePageCount();
                paginator.PageCount.Should().Be(1);
                doc.Pages.Count.Should().Be(1);
                window.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new InvalidOperationException("A4 cash memo sheet pagination failed.", failure);
    }

    [Fact]
    public void SingleDocumentPreview_RendersSuccessfullyWithSignatureSpacing()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var docData = new TransactionDocumentData
                {
                    Title = "Sale Invoice",
                    Number = "INV-20261019-001",
                    Date = new DateTime(2026, 10, 19),
                    PartyLabel = "Customer",
                    PartyName = "Malik General Store",
                    PartyCode = "C-001",
                    Phone = "0300-1234567",
                    Address = "Main Market",
                    Lines = new[]
                    {
                        new TransactionDocumentLine { ProductCode = "P-1", ProductName = "Cooking Oil 1L", Quantity = 10, UnitPrice = 500, DiscountPercentage = 0, LineTotal = 5000 }
                    },
                    SubTotal = 5000,
                    Discount = 0,
                    Total = 5000,
                    Paid = 5000,
                    Balance = 0,
                    PaymentSource = "Cash",
                    Remarks = "Standard Delivery"
                };

                var window = new TransactionDocumentPreviewWindow(docData);
                var doc = window.PreviewDocument;
                var paginator = doc.DocumentPaginator;
                paginator.ComputePageCount();
                paginator.PageCount.Should().Be(1);
                doc.Pages.Count.Should().Be(1);
                window.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new InvalidOperationException("Single document preview failed.", failure);
    }

    public class TestModel : System.ComponentModel.INotifyPropertyChanged
    {
        private decimal _amount;
        public decimal Amount
        {
            get => _amount;
            set
            {
                if (_amount == value) return;
                _amount = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Amount)));
            }
        }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void NumericTextBox_BoundToDecimal_KeepsCommasWhenTyped()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var model = new TestModel { Amount = 0m };
                var tb = new TextBox();
                SOFT2027.UI.Behaviors.NumericTextBoxBehavior.SetIsEnabled(tb, true);
                var binding = new System.Windows.Data.Binding("Amount")
                {
                    Source = model,
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
                    Mode = System.Windows.Data.BindingMode.TwoWay
                };
                tb.SetBinding(TextBox.TextProperty, binding);

                tb.Text = "2000000";

                var frame = new System.Windows.Threading.DispatcherFrame();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new System.Windows.Threading.DispatcherOperationCallback(f =>
                    {
                        ((System.Windows.Threading.DispatcherFrame)f!).Continue = false;
                        return null;
                    }),
                    frame);
                System.Windows.Threading.Dispatcher.PushFrame(frame);

                tb.Text.Should().Be("2,000,000");
                model.Amount.Should().Be(2000000m);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw failure;
    }

    [Fact]
    public void NumericTextBox_BoundViaDataContext_FormatsModelAndInputWithCommas()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var model = new TestModel { Amount = 1500000m };
                var tb = new TextBox { DataContext = model };
                SOFT2027.UI.Behaviors.NumericTextBoxBehavior.SetIsEnabled(tb, true);
                var binding = new System.Windows.Data.Binding("Amount")
                {
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
                    Mode = System.Windows.Data.BindingMode.TwoWay
                };
                tb.SetBinding(TextBox.TextProperty, binding);

                // Initial format from model
                tb.Text.Should().Be("1,500,000");

                // Typing a new raw number updates model and keeps commas
                tb.Text = "8500000";
                var frame = new System.Windows.Threading.DispatcherFrame();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new System.Windows.Threading.DispatcherOperationCallback(f =>
                    {
                        ((System.Windows.Threading.DispatcherFrame)f!).Continue = false;
                        return null;
                    }),
                    frame);
                System.Windows.Threading.Dispatcher.PushFrame(frame);

                tb.Text.Should().Be("8,500,000");
                model.Amount.Should().Be(8500000m);

                // Model update from background (e.g. product select) updates textbox with commas
                model.Amount = 45000m;
                tb.Text.Should().Be("45,000");

                // Decimal amounts (with paisa/cents)
                model.Amount = 1250.75m;
                tb.Text.Should().Be("1,250.75");

                tb.Text = "3450000.50";
                var decFrame = new System.Windows.Threading.DispatcherFrame();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new System.Windows.Threading.DispatcherOperationCallback(f =>
                    {
                        ((System.Windows.Threading.DispatcherFrame)f!).Continue = false;
                        return null;
                    }),
                    decFrame);
                System.Windows.Threading.Dispatcher.PushFrame(decFrame);

                tb.Text.Should().Be("3,450,000.50");
                model.Amount.Should().Be(3450000.50m);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw failure;
    }

    [Fact]
    public void SingleDocumentPreview_TableHeadersIncludePKR_AndRowsAreCleanNumbers()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var docData = new TransactionDocumentData
                {
                    Title = "Sale Invoice",
                    Number = "INV-20261019-002",
                    Date = new DateTime(2026, 10, 19),
                    PartyLabel = "Customer",
                    PartyName = "Ali Super Store",
                    Lines = new[]
                    {
                        new TransactionDocumentLine { ProductCode = "P-101", ProductName = "Basmati Rice 5kg", Quantity = 2, UnitPrice = 1450, DiscountPercentage = 0, LineTotal = 2900 }
                    },
                    SubTotal = 2900,
                    Discount = 0,
                    Total = 2900,
                    Paid = 2900,
                    Balance = 0
                };

                var window = new TransactionDocumentPreviewWindow(docData);
                var doc = window.PreviewDocument;
                var page = (FixedPage)((PageContent)doc.Pages[0]).Child;

                var textBlocks = new List<TextBlock>();
                foreach (var child in page.Children)
                {
                    if (child is TextBlock tb) textBlocks.Add(tb);
                }

                // Verify table headers have (PKR)
                textBlocks.Should().Contain(tb => tb.Text == "Rate (PKR)");
                textBlocks.Should().Contain(tb => tb.Text == "Amount (PKR)");

                // Verify line items have clean numbers without repeating "PKR" in rows
                textBlocks.Should().Contain(tb => tb.Text == "1,450.00");
                textBlocks.Should().Contain(tb => tb.Text == "2,900.00");
                textBlocks.Should().NotContain(tb => tb.Text == "PKR 1,450.00");

                // Verify preview image bitmap is rendered at 192 DPI with full page width (no clipping)
                window.PageHosts.Should().HaveCount(1);
                var image = (Image)window.PageHosts[0].Child;
                var bitmap = (System.Windows.Media.Imaging.BitmapSource)image.Source;
                bitmap.PixelWidth.Should().Be(1588);
                bitmap.PixelHeight.Should().BeInRange(2244, 2246);
                bitmap.DpiX.Should().Be(192);
                bitmap.DpiY.Should().Be(192);
                bitmap.Width.Should().BeApproximately(794, 1.0);
                bitmap.Height.Should().BeApproximately(1123, 1.0);

                window.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new InvalidOperationException("Table headers and clean numbers verification failed.", failure);
    }

    [Fact]
    public async Task LookupManagementViewModel_AddCategory_AnnouncesSingularCategory()
    {
        var repo = new Mock<IBusinessLookupRepository>();
        repo.Setup(r => r.GetAllAsync("Categories", It.IsAny<CancellationToken>())).ReturnsAsync(new List<LookupItem>());
        repo.Setup(r => r.SaveAsync("Categories", It.IsAny<LookupItem>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var vm = new LookupManagementViewModel(repo.Object) { Kind = "Categories" };
        vm.NewItemName = "Personal Care";
        await vm.SaveNewItemAsync();

        vm.StatusAnnouncement.Should().Be("Category 'Personal Care' added successfully.");
        vm.IsBusy.Should().BeFalse();
    }
}
