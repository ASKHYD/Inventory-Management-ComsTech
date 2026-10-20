# Changes so far

Working copy: `SOFT2027-UX-QA-Phase-Copy` (separate from the original checkout). Baseline commit: `6383834d2510ea24fd1e42ac532a18ebcf1eac62`.

| File | Reason |
|---|---|
| `src/SOFT2027.UI/App.xaml` | Load shared design tokens before shared theme resources. |
| `src/SOFT2027.UI/DesignTokens.xaml` | Add centralized palette, spacing, typography, format, control-size, and layout tokens. |
| `src/SOFT2027.UI/ThemeResources.xaml` | Align shared styles to Phase 1; fix `Color` resources that incorrectly referenced `SolidColorBrush` resources and caused startup XAML failure. |
| `src/SOFT2027.UI/MainWindow.xaml` | Carry forward the pre-existing shell/report-a-bug XAML work; Phase 1 replaces screen-level literal colors and sizes with shared resources. |
| `src/SOFT2027.UI/Views/AdministrationView.xaml` | Replace screen-level literal colors and sizes with shared resources. |
| `src/SOFT2027.UI/Views/AppTourWindow.xaml` | Replace screen-level literal colors and sizes with shared resources. |
| `src/SOFT2027.UI/Views/BackupView.xaml` | Replace screen-level literal colors and sizes with shared resources and use shared page card. |
| `src/SOFT2027.UI/Views/CashBankView.xaml` | Replace screen-level literal colors and sizes with shared resources. |
| `src/SOFT2027.UI/Views/FinancialEntryView.xaml` | Replace screen-level literal colors and sizes with shared resources and use shared page card. |
| `src/SOFT2027.UI/Views/InventoryView.xaml` | Replace screen-level literal colors and sizes with shared resources. |
| `src/SOFT2027.UI/Views/LedgerView.xaml` | Replace screen-level literal colors and sizes with shared resources. |
| `src/SOFT2027.UI/Views/LoginView.xaml` | Replace screen-level literal colors and sizes with shared resources. |
| `src/SOFT2027.UI/Views/LookupManagementView.xaml` | Replace screen-level literal colors and sizes with shared resources. |
| `src/SOFT2027.UI/Views/MasterDataView.xaml` | Replace screen-level literal colors and sizes with shared resources. |
| `src/SOFT2027.UI/Views/PurchaseEntryView.xaml` | Replace screen-level literal colors and sizes with shared resources. |
| `src/SOFT2027.UI/Views/ReportsDashboardView.xaml` | Replace screen-level literal colors and sizes with shared resources. |
| `src/SOFT2027.UI/Views/ReturnEntryView.xaml` | Replace screen-level literal colors and sizes with shared resources. |
| `src/SOFT2027.UI/Views/SalesEntryView.xaml` | Carry forward pre-existing invoice-row layout work; Phase 1 applies shared resources and standard rate-button wording. |
| `src/SOFT2027.UI/MainWindow.xaml.cs` | Pre-existing report-a-bug action work carried with the working copy; not edited in Phase 1. |
| `qa-artifacts/Run-SOFT2027-QA-Dummy.cmd` | Synthetic QA database launcher carried with the working copy for later manual testing. |
| `qa-artifacts/SOFT2027-QA-Dummy-Data-Backup-20261013.db` | Synthetic QA-only seed carried with the working copy; not changed in Phase 1. |
| `docs/CHANGES_SO_FAR.md` | Maintain this merge-oriented file inventory. |

No view-model files or `.cs` files were changed in Phase 1. No database files were changed.

## Rejection-fix pass (2026-10-13)

| File | Reason |
|---|---|
| `src/SOFT2027.UI/App.xaml` | Register presentation-only status and current-page navigation converters. |
| `src/SOFT2027.UI/Converters/StatusMessageConverter.cs` | Add status icon/color classification and current view mapping for navigation state. |
| `src/SOFT2027.UI/DesignTokens.xaml` | Improve muted/disabled text contrast, focus-ring contrast, and fixed two-decimal quantity formatting. |
| `src/SOFT2027.UI/ThemeResources.xaml` | Fix DataGrid header text rendering, DatePicker inner border, sidebar hover/current-page state, and semantic contrast tokens. |
| `src/SOFT2027.UI/MainWindow.xaml` | Reflect current view in sidebar selection and disable Categories/Brands/Units for employees under existing admin-only access. |
| `src/SOFT2027.UI/Views/AdministrationView.xaml` | Add visible add-user labels/helper text, inline validation, XAML-only password Show/Hide, and semantic status colors. |
| `src/SOFT2027.UI/Views/BackupView.xaml` | Give status/error messages an icon and semantic color. |
| `src/SOFT2027.UI/Views/CashBankView.xaml` | Give error messages an icon and semantic color. |
| `src/SOFT2027.UI/Views/FinancialEntryView.xaml` | Give status/error messages an icon and semantic color. |
| `src/SOFT2027.UI/Views/InventoryView.xaml` | Restore two-decimal stock values and give errors an icon and semantic color. |
| `src/SOFT2027.UI/Views/LoginView.xaml` | Give errors an icon and semantic color. |
| `src/SOFT2027.UI/Views/LookupManagementView.xaml` | Give status messages an icon and semantic color. |
| `src/SOFT2027.UI/Views/MasterDataView.xaml` | Give errors an icon and semantic color. |
| `src/SOFT2027.UI/Views/PurchaseEntryView.xaml` | Give status/error messages an icon and semantic color. |
| `src/SOFT2027.UI/Views/ReportsDashboardView.xaml` | Give errors an icon and semantic color. |
| `src/SOFT2027.UI/Views/ReturnEntryView.xaml` | Show return success/info/error status with an icon and the matching semantic color. |
| `src/SOFT2027.UI/Views/SalesEntryView.xaml` | Give errors an icon and semantic color. |
| `tests/SOFT2027.XamlSmoke/Program.cs` | Extend startup smoke check to audit color resource types and compare DataGrid header foreground/background. |
| `docs/FINAL_TEST_REPORT.md` | Record read-only customer-ledger defect CALC-04 and supplier comparison. |

No view-model, data-access, calculation, permission, schema, code-behind, or QA database files were changed in this pass. The add-user password Show/Hide toggle is implemented by changing the PasswordBox mask character from XAML.


| `SOFT2027.sln` | Include the standalone XAML smoke-check utility in Release builds. |
| `tests/SOFT2027.XamlSmoke/SOFT2027.XamlSmoke.csproj` | Add a no-new-package WPF smoke-check executable for view initialization. |
| `tests/SOFT2027.XamlSmoke/Program.cs` | Instantiate App resources, all XAML-backed views and preview windows with synthetic QA-only data; report each exception. |
| `qa-artifacts/Run-SOFT2027-QA-Dummy.cmd` | Run the XAML smoke check before starting SOFT2027 with the QA database path. |

## Phase 1 rejection fix — rendered DataGrid text (2026-10-13)

| File | Reason |
|---|---|
| `src/SOFT2027.UI/ThemeResources.xaml` | Fix implicit TextBlock foreground precedence in shared DataGrid header/cell/sidebar presenters; make all DataGrids use the shared header style; add a shared secondary ToggleButton style for password Show/Hide. |
| `src/SOFT2027.UI/Views/SalesEntryView.xaml` | Make custom numeric/action column header styles derive directly from the shared header style. |
| `src/SOFT2027.UI/Views/ReportsDashboardView.xaml` | Make custom report column header styles derive directly from the shared header style. |
| `src/SOFT2027.UI/Views/AdministrationView.xaml` | Apply the shared secondary toggle style to the password Show/Hide control. |
| `tests/SOFT2027.XamlSmoke/Program.cs` | Replace style-value inspection with rendered dummy-row DataGrid header TextBlock inspection and WCAG contrast checks; add rendered probes for selected rows, disabled buttons, tabs, sidebar, tooltips, placeholders and status messages. |
| `docs/CHANGES_SO_FAR.md` | Record this rejection-fix pass and its scope. |

No business logic, calculations, data access, database/schema, permissions, or view models were changed in this pass. The only C# change is the standalone QA smoke utility.

Rendered QA outcome: 83 actual header TextBlocks and the listed state probes passed contrast at or above 4.5:1. The QA launcher then started the app process, but its main-window title was `Startup Error`; its message was not exposed to the smoke runner. The app itself was therefore not visually inspected or accepted in this pass. Hover states were code-audited but not physically rendered by the smoke check.

## Report a problem replacement (2026-10-13)

| File | Reason |
|---|---|
| `src/SOFT2027.UI/BugReports/BugReportDelivery.cs` | Add the delivery interface/implementations, approved destination configuration, report sanitization, and bounded escaped URL builders for email, Gmail and WhatsApp. |
| `src/SOFT2027.UI/Views/BugReportWindow.xaml` | Add the themed, custom-chrome report dialog with visible fields, metadata, validation and delivery controls. |
| `src/SOFT2027.UI/Views/BugReportWindow.xaml.cs` | Implement required-field checks, copy/save/open-folder, explicit draft launching, launch failure feedback, and discard confirmation. |
| `src/SOFT2027.UI/MainWindow.xaml` | Shorten the sidebar tooltip to “Report a problem.” |
| `src/SOFT2027.UI/MainWindow.xaml.cs` | Replace the inline mailto launch with the report dialog and current UI metadata. |
| `tests/SOFT2027.XamlSmoke/Program.cs` | Instantiate the report dialog and check URL encoding/length for punctuation, line breaks, Urdu and long text. |
| `docs/CHANGES_SO_FAR.md` | Record the phase files and rationale. |

No business logic, calculations, data access, database/schema, permissions, or view models were changed. No network sender, screenshot capture, or NuGet package was added. The existing mailto code already escaped subject/body, used `UseShellExecute=true`, and generated a short URI. The blank browser tab therefore points to Windows' mailto protocol association/handler routing; the previous read-only registry probes found no handler at the checked per-user/class locations, though that does not rule out other Windows association mechanisms. Release build succeeded with 0 errors and 5 NU1900 vulnerability-feed warnings; QA-launcher smoke passed 83 rendered grid headers, state probes, dialog XAML, and URL checks. The launcher then started the app, whose window title again read `Startup Error`, so live navigation/use of the new dialog was not visually verified.

## Bug report dialog viewport fix (2026-10-13)

| File | Reason |
|---|---|
| `src/SOFT2027.UI/Views/BugReportWindow.xaml` | Keep the close X in the title header, constrain and center the dialog to the available work area, make only the form body scroll, and pin the delivery actions to a fixed footer. |
| `tests/SOFT2027.XamlSmoke/Program.cs` | Assert the named Close control and that the bottom action bar stays within the dialog at the 1366x768 layout envelope. |
| `docs/CHANGES_SO_FAR.md` | Record the viewport fix and build/test limitations. |

No application code-behind, business logic, data access, database/schema, permission, or view-model code changed. Release build succeeded using an isolated `qa-artifacts/build-test` output path (5 NU1900 vulnerability-feed warnings, 0 errors). The standard Release output was locked by an already-running SOFT2027 process; the smoke assertion compiled but was not run through the QA launcher in this pass.

## Printing and Preview phase (2026-10-13)

| File | Reason |
|---|---|
| `src/SOFT2027.UI/Views/TransactionDocumentPreviewWindow.cs` | Replace the non-scaling FlowDocumentReader preview with zoomable fixed-page preview hosts and a separate fixed print paginator; add app chrome and keyboard shortcuts; size memo columns explicitly; keep equal 4-up cells and paginate overflowing lines; align labels and type sizes; show configured business contact and customer area. |
| `src/SOFT2027.UI/ViewModels/SalesEntryViewModel.cs` | Add existing customer `AreaTerritory` to the preview DTO at line 406; presentation mapping only. |
| `tests/SOFT2027.XamlSmoke/Program.cs` | Measure rendered zoom at 50% and 100%, check print page size remains fixed, assert equal 4-up geometry, and generate QA-only PNG samples for the requested scenarios. |
| `qa-artifacts/preview-samples/` | Store rendered samples, preview-window capture and a read-only copy of the QA database used for row-count comparison. |
| `qa-artifacts/phase5-release-build.log` | Preserve complete Release solution build output. |
| `qa-artifacts/phase5-rendered-smoke-final.log` | Preserve final XAML/rendered preview smoke output. |
| `docs/CHANGES_SO_FAR.md` | Record phase files and test evidence. |

Release solution build succeeded with 0 errors and 5 NU1900 warnings because the NuGet vulnerability feed at `https://api.nuget.org/v3/index.json` was unreachable. Rendered zoom measured 397×561 DIP at 50% and 794×1122 DIP at 100% in a fixed 794×1122 host (2.00× width ratio); the print paginator stayed at A4 dimensions. Prior FlowDocumentReader samples at 50% and 100% had identical rendered image hashes. The old cash memo used a star-sized Product column which collapsed to a narrow strip; explicit widths now make Particulars the widest column. The read-only row-count comparison on `SOFT2027-QA-preview-readonly-copy.db` passed: all 27 table counts matched before and after sample generation. No full app or printer was launched. No business logic, calculations, data access, database/schema, permissions, or new NuGet packages were changed; the sole view-model edit is the allowed `AreaTerritory` preview mapping. `UnitName` remains unavailable without a repository/model dependency change and is not shown or fabricated. `MemoBusinessConfiguration.Address` is blank; any address printing needs client confirmation. Overflow splits into continuation cells/pages; no line is silently dropped, and totals/signatures appear with the final chunk. See the QA report for sample paths and Windows manual checks.

## Phase 3 — initial data loading and grid headers (2026-10-13)

| File | Reason |
|---|---|
| `src/SOFT2027.UI/Views/MasterDataView.xaml.cs` | Reload master data on Loaded, visible activation, and DataContext replacement with one activation guard; install explicit columns for Products, Customers, and Suppliers; show loading/empty state. |
| `src/SOFT2027.UI/Views/MasterDataView.xaml` | Disable auto-generated columns and add the loading/empty-state surface while keeping Refresh. |
| `src/SOFT2027.UI/Views/LookupManagementView.xaml.cs` | Reload lookups on Loaded, visible activation, and DataContext replacement with an activation guard; report loading/empty state. |
| `src/SOFT2027.UI/Views/LookupManagementView.xaml` | Declare the Name column even with zero rows and add the loading/empty-state surface. |
| `src/SOFT2027.UI/Views/AdministrationView.xaml.cs` | Reload User Accounts on view activation and Activity Log on tab selection/activation with duplicate-load guards and activity loading/empty text. |
| `src/SOFT2027.UI/Views/AdministrationView.xaml` | Declare User Accounts and Activity Log columns independently of data rows; add activity loading/empty state. |
| `src/SOFT2027.UI/Views/SalesEntryView.xaml.cs` | Refresh customer/product lists whenever Sales becomes visible or receives a new view model. |
| `src/SOFT2027.UI/Views/PurchaseEntryView.xaml.cs` | Refresh supplier/product lists whenever Purchase becomes visible or receives a new view model. |
| `src/SOFT2027.UI/Views/FinancialEntryView.xaml.cs` | Refresh customer/supplier lists for Receipts/Payments on view activation and DataContext replacement. |
| `src/SOFT2027.UI/Views/LedgerView.xaml.cs` | Refresh customer/supplier party lists on view activation and DataContext replacement. |
| `src/SOFT2027.UI/Views/ReportsDashboardView.xaml.cs` | Refresh report filter lists when the dashboard becomes visible or receives a new view model. |
| `tests/SOFT2027.XamlSmoke/Program.cs` | Reproduce initial loads using seeded QA data from a separate copy; check rows and headers without Refresh, hide/show and DataContext re-navigation, and probe application startup for a Startup Error. |
| `qa-artifacts/Run-SOFT2027-QA-Dummy.cmd` | Fail the QA launcher if the startup probe sees a Startup Error or cannot find the login shell; permit a QA-directory override for sandboxed testing and smoke-only completion. |
| `qa-artifacts/data-loading-smoke/` | Store the disposable seeded database copy used for data-load reproduction. |
| `qa-artifacts/launcher-runtime-qa/` | Store the disposable QA database copy used for login-shell startup verification. |
| `qa-artifacts/phase3-final-build.log` | Preserve isolated Release solution build output. |
| `qa-artifacts/phase3-standard-release-build.log` | Preserve the Release build used by the QA launcher. |
| `qa-artifacts/phase3-final-smoke.log` | Preserve final seeded-view smoke output. |
| `qa-artifacts/phase3-qa-launcher.log` | Preserve full QA launcher smoke and login-shell startup evidence. |
| `docs/CHANGES_SO_FAR.md` | Record Phase 3 implementation and verification. |

Diagnosis: Customers, Suppliers, and Products share `MasterDataView`; its Loaded handler already executed `LoadCommand`, but its grid used auto-generated columns over `FilteredRecords` (an `ICollectionView` of `ObservableCollection<object>`), so no columns existed until a row arrived. On seeded copy, the current loader returned 2 rows for each; before the fix, the smoke reproduction observed 9/7/13 generated headers only after items loaded, without Refresh. Lookups likewise already loaded on Loaded; its auto-generated Name property produced one header only after its seeded row arrived. Administration Loaded loaded Users; Activity Log loads only when its tab is selected. The Activity grid also auto-generated columns after its 7 audit rows arrived. No failed repository load was reproduced for these screens. Products was not failing with seeded data, but shared the same empty-before-first-row header gap.

`Bootstrapper` creates a new `MasterDataViewModel` per navigation, and `MainViewModel.NavigateToMasterData` replaces `CurrentViewModel`; the ContentControl can reuse the same DataTemplate root when content types match, so Loaded alone is not a reliable re-entry trigger. The smoke checked the same view through hide/show and a new DataContext. It passed without Refresh: Customers 2 rows/9 columns; Suppliers 2/7; Products 2/13; Categories 1/1; User Accounts 1/6; Activity Log 7/7. The QA session was signed in before the views were instantiated, matching app order (`InitializeSecurityAsync` runs before MainWindow is shown; sign-in sets session before navigation).

Existing Add actions stage an unsaved row in memory; Master Data persists it through Save Changes, then reloads. Deactivation is also staged until Save Changes. Lookup Add is staged until Save; Lookup Delete and persisted Administration account actions already reload their lists. Those command semantics were preserved. Receipts/Payments use `FinancialEntryView`; Ledgers and Reports now reload their existing customer/supplier/product lists on visible activation or DataContext replacement. Return Entry has no customer/supplier/product selector; it loads original invoice lines when the user requests Load Invoice.

No view-model files were modified. No business logic, data access, calculations, database/schema, permissions, or new NuGet packages were changed. Release build succeeded with 0 errors and 4 NU1900 vulnerability-feed warnings (`api.nuget.org` unreachable). QA launcher smoke and startup probe passed with the QA database directory redirected inside `qa-artifacts` because this sandbox grants read-only access to `%USERPROFILE%\Documents\SOFT2027-QA`; the first probe correctly surfaced SQLite Error 14 (`unable to open database file`) for that restricted folder. With the writable QA override, the login shell title appeared and the probe closed its temporary QA app process. The old `errors.log` entry was a separate Phase 1 issue: `#FFF5F6FA` failed parsing as `SolidColorBrush.Color` during `MainWindow.InitializeComponent`; current tokens use `#F5F6FA`, and the QA launcher startup probe now passes.

## Phase 2 — App Shell (2026-10-13)

| File | Reason |
|---|---|
| `src/SOFT2027.UI/MainWindow.xaml` | Set custom window controls to 46×32 Segoe MDL2 glyphs with keyboard-only focus visuals and red close hover; add a compact two-button sidebar footer, user-name tooltip, and a pinned Sales/Purchase total-and-save bar. |
| `src/SOFT2027.UI/MainWindow.xaml.cs` | Let Esc dismiss the profile and notification popups without changing the active page or business data. |
| `src/SOFT2027.UI/Views/AppTourWindow.xaml` | Replace the native purple title bar with themed draggable app chrome and an accessible close control. |
| `src/SOFT2027.UI/Views/AppTourWindow.xaml.cs` | Connect the Tour close control to the existing dialog-close result and support dragging the title area. |
| `src/SOFT2027.UI/Views/SalesEntryView.xaml` | Move the visible Save Invoice action to the shell's pinned bottom action bar while keeping the existing command. |
| `src/SOFT2027.UI/Views/PurchaseEntryView.xaml` | Move the visible Save Purchase action to the shell's pinned bottom action bar while keeping the existing command. |
| `src/SOFT2027.UI/SOFT2027.UI.csproj` | Embed the ComsTech logo-derived multi-size icon as the app/taskbar icon. |
| `Assets/SOFT2027.ico` | Provide valid 16, 32, 48, and 256 pixel app icon images rendered from the existing ComsTech logo asset. |
| `qa-artifacts/Run-SOFT2027-QA-Dummy.cmd` | Prefer the isolated Phase 2 Release build for QA smoke/startup and fall back to the standard Release output. |
| `docs/CHANGES_SO_FAR.md` | Record Phase 2 changes, build outcome, and visual verification limits. |

Release solution build succeeded using an isolated Release output directory because the standard `SOFT2027.UI.exe` was held open by an existing SOFT2027 process (PID 12620); the process was not stopped. Exact command: `dotnet build SOFT2027.sln --configuration Release --no-restore -p:BaseOutputPath='<Phase Copy>\qa-artifacts\phase2-solution-build\bin\'`; result: 0 errors, 4 NU1900 warnings because NuGet's vulnerability feed was unreachable. The QA launcher now selects this isolated app and smoke executable when present. The XAML smoke executable was run; the QA launcher and full app were not run in this phase, so desktop interaction is not verified.

No business logic, calculations, data access, database/schema, permissions, or view models were changed; the sole code-behind edit closes two shell popups on Esc and handles title-area dragging in Help & Tour. The inner Save buttons on Sales and Purchase are collapsed; the pinned shell buttons bind to the same existing save commands. Remaining Windows visual checks: verify all three window buttons, drag/double-click maximize, snap/resize/minimum size, close confirmation with a draft, Tour chrome, sidebar footer fit, truncated-user tooltip, and that the Sales/Purchase action bar remains visible while scrolling at normal and reduced window sizes. The user should close the already running older app instance and then launch only `qa-artifacts\Run-SOFT2027-QA-Dummy.cmd` to inspect this build.

## Phase 2 smoke failure — icon decoder compatibility (2026-10-13)

The supplied smoke output reproduced a `FileFormatException` while loading `MainWindow`. The new `SOFT2027.ico` used PNG-compressed payloads inside its ICO directory entries; WPF's `BitmapDecoder` rejected those frames when `MainWindow.xaml` assigned the file to `Window.Icon`. The existing ComsTech JPEG decoded successfully. Regenerated `Assets/SOFT2027.ico` as a classic 32-bit DIB ICO with 16, 32, 48, and 256 pixel frames. Direct decoder inspection passed (`IconBitmapDecoder`, four frames), and the XAML smoke executable then passed `rendered MainWindow / Login shell`, every registered view, seeded loading checks, and all 74 rendered DataGrid header contrast checks.

Rebuilt the Release solution into the isolated QA output: 0 errors and 4 NU1900 vulnerability-feed warnings. The full app was not launched; only the XAML smoke executable was run. The older `LowStockCount` and `#FFF5F6FA` entries in the second attached log are historical and unrelated to this image-decoder failure.


## Phase 2 shell legibility correction (2026-10-13)

| File | Reason |
|---|---|
| `src/SOFT2027.UI/MainWindow.xaml` | Replace private-use title-bar glyphs with standard symbols in main and signed-out controls so minimize, maximize/restore, and close have visible glyphs. |
| `src/SOFT2027.UI/ThemeResources.xaml` | Reduce shared sidebar footer button font and horizontal padding so both action labels fit within the sidebar. |
| `src/SOFT2027.UI/Views/AppTourWindow.xaml` | Use a standard visible close glyph in the themed Tour chrome. |
| `src/SOFT2027.UI/Views/BugReportWindow.xaml` | Use a standard visible close glyph in the bug-report dialog chrome. |

Release rebuild: `dotnet build SOFT2027.sln --configuration Release --no-restore -p:BaseOutputPath='qa-artifacts\phase2-solution-build\bin\'` succeeded with 0 errors and 5 NU1900 warnings because `https://api.nuget.org/v3/index.json` could not be reached for vulnerability data. The XAML smoke executable passed, including rendering the MainWindow/Login shell, all registered view roots, and Bug Report / Tour dialogs. It did not sample title-bar icon pixels or test hover/keyboard/window behavior. The full application was not launched; continue to use only `qa-artifacts\Run-SOFT2027-QA-Dummy.cmd` for manual visual review.

The sidebar navigation is a scrollable list; its wheel is routed to the scroll viewer beneath the pointer and the footer remains pinned. There is no separate sidebar collapse/show control in the current XAML, so no new behavior or permission change was made. No business logic, calculation, data access, database/schema, permission, or view-model files were changed.

## Phase 2 shell correction follow-up — real launcher output (2026-10-13)

| File | Reason |
|---|---|
| `src/SOFT2027.UI/MainWindow.xaml` | Draw the three window-control symbols with vector Paths, including distinct maximize and restore shapes; reserve two 106-DIP footer actions for complete labels. |
| `src/SOFT2027.UI/ThemeResources.xaml` | Tighten footer button text and padding to ensure labels fit within the 250-DIP sidebar. |
| `docs/CHANGES_SO_FAR.md` | Record the launcher/build output diagnosis and verification results. |

Root cause of the screenshots persisting: the earlier relative `BaseOutputPath` build wrote separate outputs under each project, but `Run-SOFT2027-QA-Dummy.cmd` reads the centralized `qa-artifacts\phase2-solution-build\bin\Release\net8.0-windows` folder. That left an older executable in the launcher's folder. Rebuilt with an absolute centralized BaseOutputPath so the launcher now selects the updated executable. Release build succeeded, 0 errors and 5 NU1900 warnings (NuGet vulnerability endpoint unavailable). The launcher's full XAML smoke suite and startup probe passed when directed to `qa-artifacts\qa-run` for an isolated QA DB; using the default Documents QA path first failed with SQLite error 14 (`unable to open database file`).

The smoke utility rendered all registered views and passed the 74-header contrast audit. It does not sample the title-control pixels or verify footer label clipping. I did not open the interactive app window for manual visual confirmation; launch only through `qa-artifacts\Run-SOFT2027-QA-Dummy.cmd`. No business logic, calculation, data access, database/schema, permission, or view-model code was changed.

## Maximize/restore icon correction (2026-10-13)

| File | Reason |
|---|---|
| `src/SOFT2027.UI/MainWindow.xaml` | Put maximize and restore vector icons inside the shared button ControlTemplate so their strokes use TemplateBinding to the button foreground; show one icon at a time based on WindowState. |
| `tests/SOFT2027.XamlSmoke/Program.cs` | Exercise normal and maximized window states and fail if the icon geometry, visibility switch, or stroke is missing. |
| `docs/CHANGES_SO_FAR.md` | Record the screenshot diagnosis and smoke evidence. |

Screenshot diagnosis: the 46-DIP maximize button remained in the title bar, but its restore icon was blank in the maximized screenshot. The prior DataTrigger replaced Content with a Path whose `Stroke` binding to an ancestor Button did not resolve; the rendered-state probe confirmed the restore Path had no visible stroke. The new template binds both shapes directly to `Foreground` with `TemplateBinding`, and the restore state draws two complete overlapping square outlines. Release solution build succeeded with 0 errors and 5 NU1900 warnings due unavailable NuGet vulnerability metadata. Smoke output: `PASS maximize/restore button renders distinct visible vector icons in both window states`; all existing rendered-view and DataGrid contrast checks also passed. The interactive app was not opened for a visual screenshot; verify by launching only through `qa-artifacts\Run-SOFT2027-QA-Dummy.cmd`.

No business logic, calculations, data access, database/schema, permission, or view-model code changed.

## GitHub/source handoff update (2026-10-13)

| File | Reason |
|---|---|
| `.gitignore` | Exclude the isolated phase copy and local QA databases/build outputs from GitHub. |
| `SOFT2027.sln` | Add the XAML smoke project to normal solution builds. |
| `qa-artifacts/Run-SOFT2027-QA-Fresh.cmd` | Replace the dummy-data launcher with a launcher that starts against an empty, separate QA database. |
| `README.md` | Document empty first-run data and the fresh QA launcher. |
| `CHANGELOG.md` | Record the completed UI/QA work as unreleased changes. |
| `docs/INSTALLATION_AND_HANDOVER.md` | Correct icon approval notes and document the empty QA startup workflow. |
| `docs/PROJECT_UPDATE_REPORT.md` | Update review date and distinguish previous full-suite results from current verification. |
| `docs/FINAL_TEST_REPORT.md` | Replace stale summary with the current build/smoke results, fresh database behavior, known defects, manual acceptance gates, and GitHub handoff status. |
| `docs/CHANGES_SO_FAR.md` | Maintain the merge/push file inventory. |

The source Release build succeeded after restoring the newly added smoke project. QA startup passed using a fresh empty database. Seeded rows used by master-data smoke checks remain only in ignored QA artifacts and are not part of application startup or the GitHub source. No `.db`, `.db-wal`, `.db-shm`, customer data, credentials, or database backup is tracked. Changes remain local and uncommitted/unpushed pending review.


Repository hygiene: removed generated local QA database files (including the old dummy backup and disposable seeded/fresh test databases) from `qa-artifacts`; only the fresh QA launcher remains as a tracked source artifact. Automated checks can recreate isolated QA databases when run.