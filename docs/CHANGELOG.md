# Changelog

All notable changes to the SOFT2027 FMCG Wholesale & Distribution Management System are documented in this file.

## [Unreleased] - 2026-10-18

### Critical Functional & Bug Fixes
- **Purchase Discount & Price Memory (`PurchaseEntryViewModel`, `SupplierPricingHistoryRepository`):**
  - Resolved purchase discount retrieval failure where previously saved terms for a supplier and product were either not retrieved or overwritten by defaults.
  - Linked supplier and product selection triggers to asynchronously retrieve historical pricing and discount records.
  - Added dedicated read-only historical terms context banner displaying the last purchase rate, discount percentage, and previous invoice reference.
  - Implemented immediate reactive line calculation upon rate, quantity, or discount adjustments.
  - Ensured historical pricing memory updates safely inside `TransactionCoordinator.PostPurchaseAsync` upon transaction commit without corrupting prior transactions.

- **Invoice & Purchase Bill Document Printing (`TransactionDocumentPreviewWindow`):**
  - **Net Amount Visual Priority:** Redesigned the financial summary section to render a dedicated, high-priority visual badge for `NET AMOUNT` featuring a tinted `#EDF2F9` background, 1.2pt navy outline, and bold typography.
  - **Phone Number & Party Details Collision Fix:** Restructured header party information block to prevent text collisions. Cleanly joins party code and phone numbers with dedicated vertical offsets.
  - **Document Terminology:** Explicitly separates `SALE INVOICE` / `CASH MEMO` from `PURCHASE BILL` across titles, party labels, and signature blocks.
  - **Signature Sections:** Added dedicated, non-overlapping signature lines for `Prepared By`, `Checked By`, and `Customer Signature` / `Supplier Signature`.
  - **4-Up A4 Memo Pagination:** Preserved exact 4-memos-per-sheet portrait A4 pagination verified via automated `DocumentPaginator` test suite.

### Global UX, Text Selection & Interaction Improvements
- **Clipboard & Text Copying (`ThemeResources.xaml`):**
  - Enabled native Windows clipboard copying across all application DataGrids via `ClipboardCopyMode="ExcludeHeader"`.
  - Introduced `ReadOnlySelectableTextBoxStyle` enabling standard mouse selection, right-click context menu copying, and `Ctrl+C` shortcuts on read-only fields (identifiers, balances, totals, contact numbers).
- **Administrator CRUD Integrity:**
  - Audited and unified Create, Read, Update, and safe Delete/Void/Archive workflows across all domain entities (Products, Customers, Suppliers, Invoices, Purchases, Expenses).
  - Protected posted financial documents from destructive physical deletion using reversible voiding workflows with complete audit trail integrity.
  - Enforced administrator authentication checks across administrative commands.

### Phase 2: UX Simplification, Workflow Navigation & Professional Icon System
- **Sidebar Workflow Navigation (`MainWindow.xaml`):**
  - Reorganized sidebar navigation hierarchy strictly around wholesale operator workflows:
    1. **OPERATIONS:** Sales Entry, Purchase Entry, Sales Returns, Purchase Returns.
    2. **INVENTORY:** Current Stock & Movements.
    3. **PARTIES & MASTER DATA:** Products, Customers, Suppliers, Categories, Brands & Units.
    4. **ACCOUNTS & FINANCE:** Receipts, Payments, Expenses, Cash & Bank, Customer Ledger, Supplier Ledger.
    5. **REPORTS:** Reports Dashboard.
    6. **SYSTEM:** Backup & Restore, Users & Activity.
  - Linked each navigation destination to dedicated, crisp vector StreamGeometry icons.

- **Master Data & Lookup CRUD Standardization (`MasterDataView.xaml`, `MasterDataViewModel.cs`, `LookupManagementView.xaml`):**
  - Integrated row-level contextual action menus (right-click and toolbar) across all master data grids.
  - Added one-click "Copy Record Details" functionality with formatted clipboard output for WhatsApp, SMS, or Excel sharing.
  - Standardized safe deactivation workflows: replacing destructive physical deletion of historical records with immediate status toggling (`Inactive`), ensuring ledger and invoice integrity.
  - Added vector-embellished toolbar buttons (`+ Add`, `Refresh`, `Save Changes`, `Delete/Deactivate`).
  - Added contextual confirmation dialogs with clear consequences for destructive or deactivation actions.

- **Professional Vector Icon System (`ThemeResources.xaml`):**
  - Expanded vector icon suite with standard 24x24 StreamGeometry paths: `IconPlusGeometry`, `IconEditGeometry`, `IconTrashGeometry`, `IconCopyGeometry`, `IconRefreshGeometry`, `IconMoreHorizontalGeometry`, `IconReceiptGeometry`, `IconCreditCardGeometry`, `IconTagGeometry`, `IconDatabaseGeometry`, `IconShieldGeometry`, `IconDollarGeometry`, `IconHelpCircleGeometry`, and `IconCheckGeometry`.
  - Removed decorative Unicode/emoji symbols from buttons and labels.

### Phase 3: Form Visibility, User Feedback & Loading Animations
- **Visual Add/Create Actions & Centered Modal Dialogs (`MasterDataView.xaml`, `MasterDataViewModel.cs`, `LookupManagementView.xaml`, `LookupManagementViewModel.cs`):**
  - Eliminated ambiguity when clicking "+ Add" buttons across all master data and lookup entities (Products, Customers, Suppliers, Categories, Brands, Units, User Accounts).
  - Designed centered modal cards with dimmed translucent backdrops (`#80000000`) preventing background mis-clicks and duplicate open states.
  - Implemented explicit dynamic titles: **“Add New Product”**, **“Add New Customer”**, **“Add New Supplier”**, **“Add New Category”**, **“Add New Brand”**, **“Add New Unit”**.
  - Clearly displayed required-field indicators (`*`), helpful placeholders, input bounds, and clean two-column grid layouts for complex entities.
  - Formulated explicit primary action buttons (**“Save Product”**, **“Save Customer”**, **“Save Supplier”**, **“Save Item”**) with integrated loading spinners and disabled states during save operations to prevent duplicate submissions.
  - Provided explicit secondary **“Cancel”** buttons that safely discard drafts and close modals without saving.
  - Wired complete entity creation logic with code auto-generation, validation rule checks, database insertion, list refresh, and screen-reader announcements.
- **Application-wide Loading Feedback & Spinners (`ThemeResources.xaml`, `LoginView.xaml`, `AdministrationView.xaml`):**
  - Introduced `SpinningIndicatorStyle`: a smooth, hardware-accelerated, rotating circular SVG vector spinner designed for professional Windows desktop software.
  - Introduced `ModernIndeterminateProgressBarStyle`: a sleek indeterminate progress bar matching corporate navy branding.
  - Integrated loading animations and informative status messages into authentication (**“Signing in…”**, **“Creating administrator account…”**) on `LoginView.xaml`.
  - Integrated loading overlays and spinners into `MasterDataView.xaml` and `LookupManagementView.xaml` for data retrieval and record saving (**“Saving product…”**, **“Saving customer…”**, **“Saving supplier…”**).
  - Integrated spinner animations into `AdministrationView.xaml` during user account creation and audit log retrieval.

### Critical Runtime Fixes for Add Forms & Loading Feedback
- **Root-Level Modal Elevation (`MasterDataView.xaml`, `LookupManagementView.xaml`):**
  - Resolved dialog clipping and containment within inner card boundaries by elevating the modal dialog overlay to a top-level root `<Grid>` at `Panel.ZIndex="1000"` with `HorizontalAlignment="Stretch"` and `VerticalAlignment="Stretch"`.
  - Guarantees the modal dialog floats above all headers, toolbars, and scrollable grids, preventing hidden or off-screen rendering.
- **Viewbox-Scaled Continuous Vector Spinner (`ThemeResources.xaml`):**
  - Resolved spinner invisibility in button states by encapsulating the circular arc path within `<Viewbox Stretch="Uniform">` and targeting `SpinnerRoot`'s `RenderTransform.Angle` in an indeterminate continuous Storyboard.
  - Enables smooth, non-clipping animation across all button sizes (16x16, 18x18, 20x20, 24x24).
- **Draft Creation & Success Announcement (`MasterDataViewModel.cs`):**
  - Fixed post-save notification overwrite by preserving draft entity details and announcing `"{Entity} '{Name}' added successfully."` after collection reloads.
  - Fixed Category singularization in `LookupManagementViewModel.cs` to show `"Add New Category"` and `"Save Category"`.

### Testing & Verification
- **Automated Test Suite:** 133 automated unit and integration tests passing (`133 passed, 0 failed, 0 skipped` across data, coordinator, preview, and master data viewmodels).
- **Release Build:** Clean Release compilation (`0 Warning(s), 0 Error(s)`).
- **XAML Smoke & Contrast Audits:** 15 view roots rendered; 74 DataGrid headers audited meeting WCAG contrast >= 16.48:1.
- **Visual Evidence Generated:** Visual proof captured for Add Modals, validation error states, and live animated loading spinners in `qa-artifacts/live-run-evidence/`.
- **Production Package:** Verified Windows x64 self-contained single-file release package generated at `publish/win-x64-rc.2/SOFT2027.UI.exe` (70.39 MB). Verified via automated `--startup-probe`.
