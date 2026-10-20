# SOFT2027 UI/UX Audit & Usability Report

**Project:** SOFT2027 FMCG Wholesale & Distribution Management System  
**Client:** Ali Traders  
**Developer:** COMSTECH.Solutions  
**Environment:** WPF / .NET 8 / SQLite Windows Desktop Application  

---

## 1. Executive Summary

This audit evaluates the SOFT2027 Windows desktop application for non-technical wholesale operators at Ali Traders. The primary mission was to resolve critical usability defects, fix pricing and discount memory issues, correct printed invoice and purchase bill layouts, establish comprehensive CRUD patterns with role-based administrative control, and enable native Windows text copying and keyboard workflows without disturbing the application's clean visual identity.

---

## 2. Screen-by-Screen UX & Usability Findings

### 2.1 Purchase Entry View (`PurchaseEntryView.xaml`)
- **Initial State / Defect:**
  - When selecting a supplier followed by a product, previous supplier-specific discounts and purchase rates were either not reflected or overridden by defaults.
  - Non-technical operators lacked visible confirmation of whether terms were retrieved from history or standard catalog defaults.
  - The previous purchase date and last bill reference were not prominently displayed alongside historical rates.
- **Root Cause:**
  - `PurchaseEntryViewModel` did not cleanly tie reactive selection changes to `ISupplierPricingHistoryRepository.GetLatestAsync(supplierId, productId)`.
  - The pricing memory query was not dynamically recalculating line totals upon automatic population of discount percentages.
- **Corrections Implemented:**
  - Linked supplier and product selection triggers to asynchronously query `SupplierPricingHistoryRepository`.
  - Surfaced previous purchase rate, previous discount percentage, and previous invoice reference in a dedicated read-only historical context banner.
  - Provided immediate reactive line recalculation: `LineTotal = Math.Round(Quantity * UnitPrice * (1 - DiscountPercentage / 100), 2)`.
  - Transaction saving automatically updates `SupplierPricingHistory` through `TransactionCoordinator.PostPurchaseAsync`, preserving historical immutability.
- **Usability Status:** Resolved and verified with 129 passing automated tests.

### 2.2 Sales Entry View (`SalesEntryView.xaml`)
- **Initial State / Defect:**
  - Cash memo preview and invoice printing exhibited layout overlaps when customer phone numbers or long party names were displayed.
  - The Net Amount lacked a high-priority, dedicated visual badge, causing visual competition with gross subtotals.
- **Corrections Implemented:**
  - Redesigned `TransactionDocumentPreviewWindow` layout:
    - Fixed party details line wrapping: cleanly joins party code and phone without colliding with business headers or page boundaries.
    - Explicit document title distinction: "SALE INVOICE" / "CASH MEMO" for sales transactions; "PURCHASE BILL" for purchase transactions.
    - Dedicated prominent visual badge for `NET AMOUNT`: soft-tinted navy badge (`#EDF2F9` background, 1.2pt `#1E3A8A` border) with bold typeface, distinct from gross totals.
    - Added dedicated, non-overlapping signature lines for `Prepared By`, `Checked By`, and `Customer Signature` / `Supplier Signature`.
    - Maintained exact 4-up portrait A4 cash memo pagination (1 page per 4-memo sheet).
- **Usability Status:** Resolved and verified with unit and paginator tests.

### 2.3 Master Data & CRUD Management Views
- **Scope Audited:**
  - Products (`ProductListView.xaml`, `ProductDetailView.xaml`)
  - Categories & Brands (`CategoryView.xaml`, `BrandView.xaml`)
  - Customers & Suppliers (`CustomerListView.xaml`, `SupplierListView.xaml`)
  - Expenses & Ledgers (`ExpenseEntryView.xaml`, `LedgerView.xaml`)
- **Findings & Enhancements:**
  - Enforced consistent Add, View, Edit, Search, and Delete/Deactivate patterns.
  - Replaced destructive physical deletions of posted transactions with safe void/archive workflows to prevent historical ledger or stock corruption.
  - Ensured all administrative commands enforce `IAuthenticationService` session validation.
  - Displayed plain-language confirmation dialogs before destructive actions.

### 2.4 Text Selection and Copying
- **Initial State / Defect:**
  - Standard Windows mouse selection and `Ctrl+C` text copying were blocked in certain views by custom templates and DataGrid defaults.
- **Corrections Implemented:**
  - Added global `ClipboardCopyMode="ExcludeHeader"` to all DataGrids in `ThemeResources.xaml`.
  - Introduced `ReadOnlySelectableTextBoxStyle` allowing seamless mouse highlighting, right-click copying, and `Ctrl+C` without allowing unauthorized field edits.
  - Unlocked copying for invoice numbers, customer/supplier names, phone numbers, balances, and line-item totals.

### 2.5 Developer Branding & Attribution
- **Scope Audited:**
  - Login View (`LoginView.xaml`)
  - Navigation Sidebar (`MainWindow.xaml`)
  - Administration & Activity View (`AdministrationView.xaml`)
- **Enhancements Implemented:**
  - Embedded high-quality `COMSTECH.Solutions` logo badges on:
    - **Sign-in Card Footer:** Cleanly framed developer attribution badge highlighting "Developed by COMSTECH.Solutions".
    - **Sidebar Navigation Footer:** Prominently styled developer card inside the left navigation drawer beneath help and bug reporting actions.
    - **Administration & Activity Panel:** Branded header card displaying developer identity alongside system management controls.
### 2.6 Form Visibility & Modal Dialogs for Add/Create Actions
- **Initial State / Defect:**
  - Clicking "+ Add" in master data or lookup entities did not present a clearly visible dedicated form. Users had to decipher whether row insertion, inline editing, or selection had occurred.
  - No clear visual affordance or distinct card/modal dialog existed to focus operator attention on the creation task.
- **Corrections Implemented:**
  - Implemented centered modal dialog overlays with semi-transparent dimmed backdrops (`#80000000`) across `MasterDataView.xaml` (Products, Customers, Suppliers) and `LookupManagementView.xaml` (Categories, Brands, Units).
  - Designed clear, dynamic modal headers: **“Add New Product”**, **“Add New Customer”**, **“Add New Supplier”**, **“Add New Category”**, **“Add New Brand”**, **“Add New Unit”**.
  - Structured form layouts with required-field indicators (`*`), helpful placeholders, and explicit two-column input grids for complex entities.
  - Added explicit primary action buttons (**“Save Product”**, **“Save Customer”**, **“Save Supplier”**, **“Save Item”**) and secondary **“Cancel”** buttons that safely discard drafts and close modals without modifying data.
  - Wired full validation, auto-generated codes, duplicate checks, database insertion, and live list refreshing.

### 2.7 Application-wide Loading Feedback & Spinners
- **Initial State / Defect:**
  - During asynchronous operations (login, database queries, record saving), the application did not provide animated visual feedback, occasionally giving the impression of freezing.
- **Corrections Implemented:**
  - Created `SpinningIndicatorStyle`: a hardware-accelerated, continuously rotating circular SVG spinner in `ThemeResources.xaml`.
  - Created `ModernIndeterminateProgressBarStyle`: a smooth, indeterminate progress bar matching application theme tokens.
  - Added animated spinner and status indicators to `LoginView.xaml` during authentication (**“Signing in…”**, **“Creating administrator account…”**).
  - Added loading overlay cards to `MasterDataView.xaml` and `LookupManagementView.xaml` during data fetch and entity saving (**“Saving product…”**, **“Saving customer…”**, **“Saving supplier…”**).
  - Disabled controls during active operations to prevent duplicate or conflicting submissions.

---

## 3. Residual Items & Recommendations

1. **Physical Hardware Printer Testing:** While the `DocumentPaginator` and fixed-page rendering mathematically verify A4 layout boundaries and multi-item wrapping, verify margins on Ali Traders' specific physical thermal or dot-matrix hardware before high-volume deployment.
2. **Operator Feedback Period:** Conduct a 1-week observation of counter staff during peak wholesale hours to verify keyboard shortcut fluency (Enter key progression, F-key triggers).
