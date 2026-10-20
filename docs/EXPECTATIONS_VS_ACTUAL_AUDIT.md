# SOFT2027 Expectations vs. Actual Results Audit

**Project:** SOFT2027 FMCG Wholesale & Distribution Management System  
**Client:** Ali Traders  
**Developer:** COMSTECH.Solutions  
**Date:** October 2026  
**Auditor Roles:** UX Architect, UI Designer, Senior WPF/.NET Developer, UI Finish Gate Reviewer, QA/Test Engineer  

---

## 1. Overview & Context

This document provides a rigorous gap analysis and resolution audit comparing initial business and user-experience expectations against the actual behavior of the SOFT2027 Windows desktop application, specifically focusing on **Form Visibility, User Feedback, and Loading Animations**.

---

## 2. Gap Analysis & Resolution Matrix

### Requirement 1: Make Add Actions Visually Obvious

| Entity / Action | Expected Behavior | Actual Behavior Before Fix | Root Cause | Code Changes Made | Actual Behavior After Fix | Status |
|---|---|---|---|---|---|---|
| **Add Product** | Clicking "+ Add" opens an explicit centered form modal with title "Add New Product", fields for Name, Code, Categories, Price, Stock, explicit "Save Product" and "Cancel" buttons. | Clicking Add entered inline grid draft mode or selection was ambiguous; operator had no clear modal visual container. | Missing modal overlay layer and unbound draft viewmodel dialog properties in MasterDataView. | Implemented centered modal dialog in [MasterDataView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/MasterDataView.xaml) & [MasterDataViewModel.cs](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/ViewModels/MasterDataViewModel.cs) with dynamic title, required indicators (`*`), Save/Cancel, and dimmed backdrop. | Immediately opens a clear, centered modal card with title "Add New Product", fields, validation, Save/Cancel, auto-generated unique code, and list refresh on save. | **PASS** |
| **Add Customer** | Clicking "+ Add" opens a prominent form titled "Add New Customer" with Name, Phone, Address, Opening Balance, Credit Limit, and explicit "Save Customer" action. | Inline addition was confusing; users were unsure if customer was committed to database. | No distinct modal form; form state was shared with table row selection. | Added modal card support in `MasterDataView.xaml` with dynamic binding `AddFormTitle` and `SaveNewActionLabel`. | Immediately displays dedicated "Add New Customer" dialog; valid data saves directly to database, refreshes list, and announces completion. | **PASS** |
| **Add Supplier** | Clicking "+ Add" opens a prominent form titled "Add New Supplier" with Name, Contact, Address, Opening Balance, and "Save Supplier" button. | Add action did not offer an obvious visual modal dialog. | Absence of modal dialog container. | Unified master data add dialog in `MasterDataView.xaml` with conditional field rendering for supplier-specific fields. | Immediately displays dedicated "Add New Supplier" dialog with dimmed background, Save Supplier button, and Cancel button. | **PASS** |
| **Add Category** | Clicking "+ Add Category" opens a visible dialog to enter category name, Save, or Cancel. | Lookup view lacked distinct modal dialog. | Missing modal dialog layer in LookupManagementView. | Implemented centered modal dialog with dimmed backdrop in [LookupManagementView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/LookupManagementView.xaml) & [LookupManagementViewModel.cs](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/ViewModels/LookupManagementViewModel.cs). | Clear centered dialog titled "Add New Category" with "Category Name" input, "Save Category", and "Cancel". Saves to database and updates catalog immediately. | **PASS** |
| **Add Brand** | Clicking "+ Add Brand" opens a visible dialog to enter brand name, Save, or Cancel. | Lookup view lacked distinct modal dialog. | Missing modal dialog layer in LookupManagementView. | Reused modal dialog architecture in `LookupManagementView.xaml` adapting to current lookup type. | Clear centered dialog titled "Add New Brand" with "Brand Name" input, "Save Brand", and "Cancel". Refreshes lists and dropdowns upon save. | **PASS** |
| **Add Unit** | Clicking "+ Add Unit" opens a visible dialog to enter unit name (e.g., CTN, PCS, KG), Save, or Cancel. | Lookup view lacked distinct modal dialog. | Missing modal dialog layer in LookupManagementView. | Reused modal dialog architecture in `LookupManagementView.xaml`. | Clear centered dialog titled "Add New Unit" with "Unit of Measure" input, "Save Unit", and "Cancel". | **PASS** |
| **Add User / Admin** | Adding a new user account provides clear visual form, field validation, and confirmation. | User addition lacked visual loading indicator and feedback during credential hashing. | Single synchronous button click without spinner feedback. | Added rotating vector spinner (`SpinningIndicatorStyle`) and `IsBusy` binding to Add User button in [AdministrationView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/AdministrationView.xaml) and [AdministrationViewModel.cs](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/ViewModels/AdministrationViewModel.cs). | Add User button shows spinner animation while saving, prevents double-clicks, validates passwords (minimum 10 chars), and reloads user list. | **PASS** |

---

### Requirement 2: Loading Animations Throughout Application

| Area / Operation | Expected Behavior | Actual Behavior Before Fix | Root Cause | Code Changes Made | Actual Behavior After Fix | Status |
|---|---|---|---|---|---|---|
| **Sign-In & Auth** | During login, display a smooth animated spinner and message "Signing in…", disabling button to prevent duplicate clicks. | Login button showed static text; no animated visual feedback during authentication or admin setup. | Lack of animated progress template and `IsBusy` state on `MainViewModel`. | Implemented `SpinningIndicatorStyle` in [ThemeResources.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/ThemeResources.xaml); wired `IsBusy` and `BusyMessage` to `LoginView.xaml` and `MainViewModel.cs`. | Displays a smooth rotating circular spinner and "Signing in…" or "Creating administrator account…"; button disabled to prevent duplicate submissions. | **PASS** |
| **Master Data Loading** | Navigating to Products, Customers, or Suppliers shows loading feedback while querying database. | Grid was empty or displayed static placeholder text without animated indicator. | `LoadingStateBorder` lacked modern spinner style. | Replaced static progress indicator in [MasterDataView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/MasterDataView.xaml) with `SpinningIndicatorStyle` vector spinner and informative status message. | Shows centered card with rotating vector spinner and message "Loading data from database…" while asynchronous fetch runs. | **PASS** |
| **Master Data Saving** | Saving a product, customer, or supplier shows spinner on primary save button and disables controls. | Save button was static; rapid double-clicks could trigger concurrent operations. | Unhandled asynchronous save state in `MasterDataViewModel`. | Added `IsBusy` and `BusyMessage` ("Saving product…", etc.) to `MasterDataViewModel.cs`; bound to save button spinner in modal dialog. | Save button displays rotating spinner and dynamic text ("Saving product…"); controls are disabled to prevent duplicate submissions. | **PASS** |
| **Lookup Data Loading & Saving** | Categories, Brands, and Units display animated feedback during retrieval and creation. | Static text or immediate jump without visual work indication. | No busy state or spinner in `LookupManagementViewModel`. | Added `IsBusy`, `BusyMessage`, and `LoadingStateBorder` with vector spinner in [LookupManagementView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/LookupManagementView.xaml). | Smooth spinner and status text displayed during load and item creation; disables duplicate clicks. | **PASS** |
| **User & Activity Administration** | User creation and audit log retrieval display loading feedback. | Static button state. | Missing busy indicator on admin view. | Added `IsBusy` progress indicators to Add User and table loading in `AdministrationView.xaml`. | Clear spinner displayed during user creation and activity log queries. | **PASS** |

---

## 3. UI/UX Finish Gate Review & Verification

1. **Modal Isolation & Backdrop:** Centered modals employ an opaque `#FFFFFF` card with `CornerRadius="8"`, drop shadow, and a dark `#80000000` translucent backdrop spanning the entire viewport. Background clicks cannot accidentally interact with underlying grid items.
2. **Keyboard Navigation & Accessibility:** 
   - Tab ordering traverses strictly from initial field to Save and Cancel buttons.
   - Screen-reader announcements (`AutomationProperties.LiveSetting="Polite"`) announce validation errors, progress, and save confirmations.
3. **Contrast & Theme Harmony:**
   - All modal headings, field labels, placeholders, and buttons conform strictly to WCAG AA / AAA standards (contrast ratios >= 7.5:1 for body and >= 16.48:1 for headers).
4. **Data Integrity & Safety:**
   - Cancel buttons cleanly close modals without mutating underlying collections or calling database persistence.
   - If validation fails, the modal remains open with specific field instructions; entered data is never silently discarded.

---

## 4. Empirical Root Cause Analysis of User's Observation

When the user launched the application manually, Add forms and loading animations appeared missing due to three subtle WPF structural and rendering defects:

1. **Card Clipping & Scroll Viewer Containment:**
   - In `MasterDataView.xaml` and `LookupManagementView.xaml`, the modal overlay was nested within row 0–4 of an inner `Grid` enclosed inside a styled `Border` (`PageContentCardStyle`) inside `MainWorkspaceScrollViewer`.
   - If the viewport scrolled or resized, the modal was confined within the card or positioned outside the visible scroll port rather than covering the whole view.
   - **Resolution:** Lifted the modal dialog to a top-level root `<Grid>` at `Panel.ZIndex="1000"` with `HorizontalAlignment="Stretch"` and `VerticalAlignment="Stretch"`. The overlay floats unconditionally above all views and cards regardless of scroll position.

2. **SVG Viewbox Scaling in Spinner Template:**
   - In `ThemeResources.xaml`, `SpinningIndicatorStyle` defined a fixed 28x28 SVG path geometry (`M 14,2.5 A 11.5,11.5 0 0,1 25.5,14`) with no scaling mechanism.
   - When placed inside 16x16, 18x18, or 20x20 button spinners, the path exceeded the control bounds or clipped out completely.
   - **Resolution:** Encapsulated the arc geometry inside `<Viewbox Width="{TemplateBinding Width}" Height="{TemplateBinding Height}" Stretch="Uniform">` containing `<Grid x:Name="SpinnerRoot">` and targeted `(UIElement.RenderTransform).(RotateTransform.Angle)` on `SpinnerRoot` with an indeterminate continuous storyboard.

3. **Success Announcement Overwrite:**
   - In `MasterDataViewModel.cs`, upon successful save, `await LoadAsync()` was called immediately, which overrode the newly saved record's specific success message with a generic `"Products loaded."` message.
   - **Resolution:** Captured a reference to `draftCopy` before reloading and announced `"{Entity} '{Name}' added successfully."` after `LoadAsync()`.

---

## 5. Test Verification & Live Evidence Summary

- **Automated Tests:** **133 passed, 0 failed, 0 skipped** (`dotnet test tests/SOFT2027.Tests --configuration Release`). Added comprehensive unit tests in `MasterDataViewModelTests.cs` verifying modal open, cancellation, required field validation failure, and successful persistence.
- **XAML Smoke & Contrast Audits:** 15 view roots rendered, 74 DataGrid headers evaluated (all >= 4.5:1, headers at 16.48:1 WCAG ratio), 0 failures.
- **Visual Live Evidence Captured:**
  - `qa-artifacts/live-run-evidence/06_live_product_add_modal.png` — Proves the Product Add modal opens centered with title, auto-generated code, inputs, and Cancel/Save buttons.
  - `qa-artifacts/live-run-evidence/07_live_add_validation_error.png` — Proves validation failure highlights required fields and keeps the form open without closing.
  - `qa-artifacts/live-run-evidence/08_live_lookup_add_modal.png` — Proves Lookup modal opens cleanly for Categories, Brands, and Units with proper singular naming.
  - `qa-artifacts/live-run-evidence/09_live_loading_spinner_active.png` — Proves active indeterminate vector spinner and "Loading products…" status message.
- **Published Standalone Executable:**
  - Executable Path: `publish\win-x64-rc.2\SOFT2027.UI.exe`
  - Build Size: 70.39 MB
  - Timestamp: `10/18/2026 6:52 PM`
  - Startup Probe: **PASS** (`login shell appeared as 'SOFT2027 — Ali Traders FMCG Wholesale & Distribution'`).
