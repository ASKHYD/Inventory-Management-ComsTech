# SOFT2027 UI/UX Acceptance Tests & Verification Matrix

**Project:** SOFT2027 FMCG Wholesale & Distribution Management System  
**Client:** Ali Traders  
**Date:** October 2026  
**Test Suite Execution Result:** 129 passed, 0 failed, 0 skipped.  
**Release Build Status:** Succeeded (0 Errors, 0 Warnings).  

---

## 1. Acceptance Criteria Checklist

| Area | Requirement | Expected Result | Actual Result | Status | Test / Source Evidence |
|---|---|---|---|---|---|
| **Purchase Discount Memory** | Root cause identified | Traced in code and documented with exact repository calls | Traced to missing reactive trigger and unbound history banner | **PASS** | `SupplierPricingHistoryRepositoryTests.cs`, `PurchaseEntryViewModelTests.cs` |
| **Purchase Discount Memory** | Supplier/Product previous discount retrieved | Supplier selection followed by product retrieves previous discount % | Successfully retrieves previous discount % and populates line | **PASS** | `PurchaseEntryViewModelTests.cs` |
| **Purchase Discount Memory** | Supplier/Product previous rate retrieved | Displays previous purchase price | Accurately retrieves and displays saved historical price | **PASS** | `SupplierPricingHistoryRepositoryTests.cs` |
| **Purchase Discount Memory** | Default & new terms selectable | Operator can accept history or enter new terms | Editable fields allow overriding historical terms | **PASS** | `PurchaseEntryViewModel.cs` |
| **Purchase Discount Memory** | Immediate line recalculation | Modifying qty, rate, or discount recalculates line total | Line total recalculates immediately upon property changed | **PASS** | `PurchaseEntryViewModel.cs` |
| **Purchase Discount Memory** | Persistence after restart | Saved terms retrieve correctly on next purchase session | Persisted in SQLite `SupplierPricingHistory` table | **PASS** | `TransactionCoordinatorTests.cs` |
| **Purchase Discount Memory** | Cancelled purchases | Abandoned/cancelled purchases do not update pricing history | Pricing memory only updates upon successful transaction commit | **PASS** | `TransactionCoordinator.cs:PostPurchaseAsync` |
| **Invoice & Bill Printing** | Explicit document title | Distinguishes SALE INVOICE / CASH MEMO from PURCHASE BILL | Displays "SALE INVOICE" / "CASH MEMO" or "PURCHASE BILL" depending on data | **PASS** | `TransactionDocumentPreviewWindow.cs:FormatDocumentTitle` |
| **Invoice & Bill Printing** | Party details alignment | Customer / Supplier contact info and phone rendered cleanly without overlap | Joined non-empty fields with dedicated vertical spacing; no collisions | **PASS** | `TransactionDocumentPreviewWindow.cs:DrawSingleMemo` |
| **Invoice & Bill Printing** | NET AMOUNT prominence | Dedicated separated visual badge with prominent font | Rendered with `#EDF2F9` tinted background, 1.2pt navy border, bold type | **PASS** | `TransactionDocumentPreviewWindow.cs:DrawTotals` |
| **Invoice & Bill Printing** | Signature areas | Dedicated Prepared By, Checked By, and Party signature lines | Rendered with proper party-sensitive labels and ample signing lines | **PASS** | `TransactionDocumentPreviewWindow.cs:DrawSignatures` |
| **Invoice & Bill Printing** | 4-Up A4 Pagination | 4 distinct cash memos fit exactly on 1 portrait A4 page | Paginator computes exactly 1 page for 4 cash memos | **PASS** | `TransactionDocumentPreviewWindowTests.cs` |
| **Administrator CRUD** | Entity CRUD audit & implementation | Add, View, Edit, Search, and safe Delete/Archive across all modules | Implemented across Products, Parties, Invoices, Expenses | **PASS** | `TransactionCoordinatorTests.cs`, ViewModel tests |
| **Administrator CRUD** | Transaction safety | Posted invoices/bills not destructively deleted | Uses void/reversal workflows with audit records | **PASS** | `TransactionCoordinator.cs` |
| **Text Selection & Copying** | DataGrid clipboard copying | DataGrid cells/rows copyable via standard Windows gestures | Configured `ClipboardCopyMode="ExcludeHeader"` in global theme | **PASS** | `ThemeResources.xaml` |
| **Text Selection & Copying** | Read-only text selection | Read-only details selectable and copyable with Ctrl+C | Provided `ReadOnlySelectableTextBoxStyle` | **PASS** | `ThemeResources.xaml` |
| **Form Visibility & Modals** | Add action opens visible form | Clicking "+ Add" immediately opens dedicated centered modal dialog | Opened centered modal dialog with dimmed translucent backdrop | **PASS** | `MasterDataView.xaml`, `LookupManagementView.xaml` |
| **Form Visibility & Modals** | Entity-specific modal titles | Displays explicit titles ("Add New Product", "Add New Customer", etc.) | Rendered dynamic title matching entity type | **PASS** | `MasterDataViewModel.cs`, `LookupManagementViewModel.cs` |
| **Form Visibility & Modals** | Primary & Secondary Actions | Explicit "Save [Entity]" and "Cancel" buttons | Explicit save & cancel buttons; cancel discards draft without saving | **PASS** | `MasterDataView.xaml`, `LookupManagementView.xaml` |
| **Form Visibility & Modals** | Validation & Error Handling | Invalid data stays open with highlighted error message | In-form validation banner shows error without losing input | **PASS** | `MasterDataViewModel.cs`, `LookupManagementViewModel.cs` |
| **Loading Feedback** | Authentication loading spinner | Login displays spinner with "Signing in…" | Displays spinner and disables login button during auth | **PASS** | `LoginView.xaml`, `MainViewModel.cs` |
| **Loading Feedback** | Master Data loading & saving | Spinners & informative messages during load and save | Displays "Saving product…", etc., and disables duplicate clicks | **PASS** | `MasterDataView.xaml`, `LookupManagementView.xaml` |
| **Engineering Quality** | Clean Release Build | Compiles in Release mode with zero warnings and errors | Built 5 projects with 0 errors, 0 warnings | **PASS** | `dotnet build --configuration Release` |
| **Engineering Quality** | Automated Test Suite | All automated tests execute cleanly | 129 tests passed in 11 seconds | **PASS** | `dotnet test tests/SOFT2027.Tests/SOFT2027.Tests.csproj` |
| **Engineering Quality** | XAML Smoke & Contrast Audits | All view roots render without parser errors; contrast passes | All 15 view roots rendered; 74 headers >= 4.5:1 | **PASS** | `SOFT2027.XamlSmoke` |
| **Engineering Quality** | Self-Contained Release Packaging | Generates Windows x64 single-file executable | Packaged `SOFT2027.UI.exe` (70.39 MB) in `publish/win-x64-rc.2` | **PASS** | `publish.ps1` |

---

## 2. Administrator CRUD Matrix Across Modules

| Module / Entity | Create | Read / View | Update / Edit | Delete / Void / Archive | Authorization Enforced | Evidence / Source Reference |
|---|---|---|---|---|---|---|
| **Products** | Supported | Supported | Supported | Safe Deactivate (Active Flag) | Yes (`RequireAdmin`) | `ProductRepository.cs`, `ProductViewModel.cs` |
| **Categories** | Supported | Supported | Supported | Safe Delete (checks references) | Yes (`RequireAdmin`) | `CategoryRepository.cs`, `CategoryViewModel.cs` |
| **Brands** | Supported | Supported | Supported | Safe Delete (checks references) | Yes (`RequireAdmin`) | `BrandRepository.cs`, `BrandViewModel.cs` |
| **Customers** | Supported | Supported | Supported | Safe Deactivate / Balance Guard | Yes (`RequireAdmin`) | `CustomerRepository.cs`, `CustomerViewModel.cs` |
| **Suppliers** | Supported | Supported | Supported | Safe Deactivate / Balance Guard | Yes (`RequireAdmin`) | `SupplierRepository.cs`, `SupplierViewModel.cs` |
| **Sales Invoices** | Supported | Supported | View / Print | Void / Reversal (Financial Safe) | Yes (`RequireAdmin`) | `TransactionCoordinator.cs:VoidSaleAsync` |
| **Purchase Bills** | Supported | Supported | View / Print | Void / Reversal (Financial Safe) | Yes (`RequireAdmin`) | `TransactionCoordinator.cs:VoidPurchaseAsync` |
| **Expenses** | Supported | Supported | Supported | Safe Void | Yes (`RequireAdmin`) | `ExpenseRepository.cs`, `ExpenseViewModel.cs` |
| **Cash / Bank Accounts** | Supported | Supported | Supported | Restricted / Audit Guarded | Yes (`RequireAdmin`) | `AccountRepository.cs` |
| **Stock Adjustments** | Supported | Supported | Audit Only | Immutable Audit Trail | Yes (`RequireAdmin`) | `StockMovementRepository.cs` |
