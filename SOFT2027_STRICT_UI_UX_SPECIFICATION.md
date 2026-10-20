# SOFT2027 — STRICT UI/UX AUDIT & SPECIFICATION FOR NON-TECHNICAL FMCG OPERATOR

**Client:** Ali Traders  
**Solution:** FMCG Wholesale & Distribution ERP (Windows WPF Desktop / .NET 8 / SQLite)  
**Primary User Persona:** Non-technical wholesale shop owner / inventory clerk / billing operator.  
**Review Standard:** Strict Product Finish-Gate (Ground truth, zero-fluff, actionable technical specifications).  
**Target Audience for this Report:** Implementation Engineer / Next Developer.

---

## 1. Executive Summary & Persona Profile

### The Reality of the Non-Technical Wholesale User:
1. **Speed & Keyboard Muscle Memory:** In a busy FMCG wholesale store, the clerk creates 50–300 invoices a day while customers and truck drivers are waiting. They use the **Numpad and Enter key**, not a mouse.
2. **Low Cognitive Tolerance for Cryptic States:** They do not understand terms like `"Entity foreign key violation"`, `"Unhandled Exception"`, or unformatted numbers like `12450.5`.
3. **High Cost of Mistakes:** If an invoice rate is typed as `1200` instead of `120`, or a cash receipt is credited to the wrong ledger, real money is lost.
4. **Hardware Environment:** Usually run on POS counter desktops or entry-level laptops (1366x768 or 1080p screens, standard membrane keyboards, sometimes barcode guns).

---

## 2. Hard Finish-Gate Findings & Implementation Work Orders

Each item below is a **blocking issue** that an incoming developer must implement and verify.

---

### Work Order 1: Full Numpad/Keyboard Flow in Transaction Entry (Sales & Purchases)

* **Severity:** **BLOCKER (P0)**
* **User Impact:** A wholesale operator cannot use a mouse to click between "Product", "Qty", "Price", "Discount", and "Add Item" for every line item. It slows counter checkout to an unacceptable crawl.
* **Target Files:**
  * [SalesEntryView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/SalesEntryView.xaml)
  * [PurchaseEntryView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/PurchaseEntryView.xaml)
  * Corresponding ViewModels
* **Strict Requirements:**
  1. **Tab/Enter Chaining:** Pressing `Enter` in the `Product Search/Selector` must jump focus to `Quantity`. Pressing `Enter` in `Quantity` must jump to `Unit Price` (pre-filled with default sale price). Pressing `Enter` in `Unit Price` must jump to `Disc %`. Pressing `Enter` in `Disc %` must execute `Add Line` and return cursor focus back to `Product Search`.
  2. **Global Hotkeys:** must be an availble page for "Global Hotkeys"
     * `F1`: Focus Customer Selection
     * `F2`: Focus Product Search Box (For now search is only showing product not cutomers/supplier/brands etc if serached)
     * `F3`: Add New Line / Commit current row
     * `F4` or `Ctrl+S`: Save & Print Invoice (Cash Bill)
     * `F5`: Save Credit Bill
     * `F12`: Clear/New Bill
     * `Esc`: Close open modal / cancel current search popup
  3. **Visual Keyboard Hints:** Add subtle hotkey badges onto buttons (e.g., `Save & Print [F4]`, `Add Line [Enter]`).
* **Verification Acceptance Condition:** An operator must be able to ring up a 5-item bill using only the keyboard in under 15 seconds without touching the mouse once.

---

### Work Order 2: Auto-Formatting & Pakistani Rupee (PKR) Standards on All Numeric Inputs

* **Severity:** **HIGH (P1)**
* **User Impact:** A non-technical user cannot easily tell the difference between `100000` (1 Lakh) and `1000000` (10 Lakh) in plain text boxes, leading to catastrophic billing errors.
* **Target Files:**
  * [MasterDataView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/MasterDataView.xaml)
  * [SalesEntryView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/SalesEntryView.xaml)
  * [PurchaseEntryView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/PurchaseEntryView.xaml)
  * [FinancialEntryView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/FinancialEntryView.xaml)
* **Strict Requirements:**
  1. **Live Digit Grouping (Commas):** Numeric boxes for Currency (`Sale Price`, `Purchase Price`, `Opening Balance`, `Received Amount`, `Total Bill`) must format on blur or keystroke as `1,250,000.00`.
  2. **Non-Numeric Character Rejection:** Input boxes must reject alphabet characters and invalid punctuation via a clean WPF `PreviewTextInput` behavior.
  3. **Auto-Select on Focus:** When tabbing into any numeric or text field, the entire existing content must be automatically selected (`SelectAll()`) so typing a new number replaces the placeholder immediately instead of appending to it (e.g. typing `5` doesn't turn `0` into `05`).
* **Verification Acceptance Condition:** Entering `50000` displays as `PKR 50,000.00`. Tabbing into the box highlights the text for 1-key overwrite. Letters are ignored.

---

### Work Order 3: Destructive Action Protection & Two-Step Safety

* **Severity:** **CRITICAL (P0)**
* **User Impact:** Accidentally clicking "Delete" on a Customer, Product, or Invoice instantly damages ledgers or inventory if not rigorously guarded.
* **Target Files:**
  * [MasterDataView.xaml.cs](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/MasterDataView.xaml.cs)
  * [LookupManagementView.xaml.cs](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/LookupManagementView.xaml.cs)
  * [ReturnEntryView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/ReturnEntryView.xaml)
* **Strict Requirements:**
  1. **No Silent Deletions:** Any delete button must trigger a standardized confirmation modal with explicit details:
     * *Example:* `"Are you sure you want to delete Customer 'Haji Superstore' (Code: CUST-004)? This customer has 12 past transactions. To prevent ledger discrepancies, consider changing status to Inactive instead."*
  2. **Secondary Guard for High-Risk Actions:** For wiping data or clearing transactions, require typing `"CONFIRM"` or pressing a distinct safety button.
  3. **Soft-Delete / Deactivation Default:** Make the primary button in Master Data "Set Inactive" rather than "Delete". Deletion should only be enabled if `TransactionCount == 0`.
* **Verification Acceptance Condition:** Clicking Delete on any entity with related records refuses deletion with an explanation and offers one-click deactivation instead.

---

### Work Order 4: Stock Warning & Out-of-Stock Visual Guard

* **Severity:** **HIGH (P1)**
* **User Impact:** A wholesale distributor cannot sell stock they don't have. Invoicing negative inventory creates chaos during warehouse dispatch.
* **Target Files:**
  * [SalesEntryView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/SalesEntryView.xaml)
  * [SalesEntryViewModel.cs](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/ViewModels/SalesEntryViewModel.cs)
* **Strict Requirements:**
  1. **Stock Badge on Line Selection:** When a product is selected in Sales Entry, prominently show `Available Stock: XX [Unit]` right next to the Quantity box.
  2. **Low-Stock / Zero-Stock Colors:**
     * `Stock > Minimum`: Subtle Gray / Navy.
     * `Stock <= Minimum`: Amber warning badge (`⚠ Low Stock: 4 Bags left`).
     * `Stock == 0`: Crimson badge (`⛔ Out of Stock`).
  3. **Over-Sell Validation:** If an operator attempts to bill `10` units when only `6` exist, pop an immediate warning modal:
     * Option A: *Adjust Quantity to Available (6)*
     * Option B: *Cancel Line*
     * Option C: *Allow Negative (Manager Override Only — if configured in settings)*.
* **Verification Acceptance Condition:** Selecting a zero-stock item immediately marks the line in red and prevents adding the line without intentional manager consent.

---

### Work Order 5: Customer Credit Limit & Overdue Balance Warnings

* **Severity:** **HIGH (P1)**
* **User Impact:** Wholesale operators frequently lose money by inadvertently billing customers who have exceeded their agreed credit limit or have unpaid overdue bills.
* **Target Files:**
  * [SalesEntryView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/SalesEntryView.xaml)
  * [MasterDataView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/MasterDataView.xaml)
* **Strict Requirements:**
  1. **Customer Snapshot Card in Sales Entry:** Selecting a customer must instantly display:
     * Current Balance: `PKR 450,000` (Red if > Credit Limit, Green if 0)
     * Credit Limit: `PKR 300,000`
     * Available Credit: `PKR -150,000 (OVER LIMIT)`
  2. **Credit Bill Intercept:** If `Current Balance + New Invoice Total > Credit Limit`, the Save Credit Bill button must prompt:
     * *"Warning: Customer 'Al-Madina Traders' has exceeded their credit limit by PKR 150,000. Do you want to proceed?"*
* **Verification Acceptance Condition:** An operator selecting an over-limit customer sees an unmistakable alert and cannot accidentally approve credit without explicit confirmation.

---

### Work Order 6: Quick Filter & Category Pills on Master Data & Ledgers

* **Severity:** **MEDIUM (P2)**
* **User Impact:** When Ali Traders has 2,000 products or 800 customers, typing in a single search box is not always enough.
* **Target Files:**
  * [MasterDataView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/MasterDataView.xaml)
  * [LedgerView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/LedgerView.xaml)
* **Strict Requirements:**
  1. **Quick Status Filter Chips:** Provide 3 click chips: `[ All (1,240) ]`, `[ Active (1,180) ]`, `[ Inactive (60) ]`.
  2. **Category / Area Dropdown Filters:**
     * Products: Quick filter by Category and Brand.
     * Customers: Quick filter by Area/Territory (e.g. "Saddar", "Latifabad", "Site Area").
  3. **Auto-Clear Search Button:** Add a visible `×` inside the search textbox when text is present to clear with one click.
* **Verification Acceptance Condition:** Clicking "Latifabad" territory instantly filters the customer list without typing.

---

### Work Order 7: One-Click Thermal Receipt (3-inch / 80mm) & Standard A4 Memos

* **Severity:** **HIGH (P1)**
* **User Impact:** Wholesale shops use thermal roll printers (Epson, Xprinter 80mm) for counter retail and dot-matrix/A4 laser printers for bulk delivery dispatches. Forcing an A4 preview for a 2-item quick sale is impractical.
* **Target Files:**
  * `TransactionDocumentPreviewWindow.xaml`
  * Print document generators
* **Strict Requirements:**
  1. **Print Mode Toggle:** In Sales Entry, provide two distinct print buttons:
     * `Print Thermal Slip (80mm)` — Compact, high-contrast, zero-margin slip for instant customer handoff.
     * `Print Invoice (A4 / 4-Up)` — Full wholesale tax memo with terms, driver signature, and Urdu notes.
  2. **Direct Silent Print Option:** In settings, allow bypassing the preview window so pressing `F4` sends raw bytes directly to the default POS printer.
* **Verification Acceptance Condition:** Clicking Thermal Print renders a clean 80mm slip with shop name, date, invoice number, items, total, and balance.

---

### Work Order 8: Plain-Language Error Messages (No Developer Jargon)

* **Severity:** **CRITICAL (P0)**
* **User Impact:** When an operation fails, displaying technical stack traces or SQLite exceptions (`"Constraint failed: UNIQUE constraint failed: Products.ProductCode"`) panics non-technical operators.
* **Target Files:**
  * [StatusMessageConverter.cs](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Converters/StatusMessageConverter.cs)
  * ViewModels error handling blocks
* **Strict Requirements:**
  1. **Exception Translation Layer:** Intercept known database and validation exceptions into human instructions:
     * `UNIQUE constraint failed: Products.ProductCode` ➔ *"A product with Code '{0}' already exists. Please enter a different code."*
     * `FOREIGN KEY constraint failed` ➔ *"This item is linked to existing invoices or ledger entries and cannot be removed."*
     * `SQLite disk I/O error` ➔ *"Could not write to database. Please check if your hard drive is full or if another program is locking the file."*
  2. **Actionable Suggestions:** Every error message must tell the operator **what to do next** (e.g. `"Check the highlighted box in red"`).
* **Verification Acceptance Condition:** Intentionally creating a duplicate code produces a friendly instruction, never raw SQL text.

---

### Work Order 9: Screen Resolution Adaptability (1366x768 Standard Laptop Scaling)

* **Severity:** **HIGH (P1)**
* **User Impact:** Many shop cash counters use older 18.5" monitors (1366x768 resolution) with 125% Windows DPI scaling. Modals or DataGrids that exceed 600px height get clipped off-screen, hiding the "Save" button.
* **Target Files:**
  * [MasterDataView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/MasterDataView.xaml)
  * [LookupManagementView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/LookupManagementView.xaml)
  * [SalesEntryView.xaml](file:///d:/Ali%20Traders/Inventory-Management-ComsTech/src/SOFT2027.UI/Views/SalesEntryView.xaml)
* **Strict Requirements:**
  1. **Modal MaxHeight with Inner Scroll:** All modal dialogs must use `MaxHeight="80vh"` with internal `ScrollViewer VerticalScrollBarVisibility="Auto"` so the header and footer (Cancel/Save) remain pinned while form fields scroll.
  2. **Sidebar Collapse by Default on Small Screens:** If window width is < 1100px, collapse the sidebar automatically to give full breathing room to the active table.
* **Verification Acceptance Condition:** Running at 1366x768 with 125% DPI displays all modal buttons ("Save", "Cancel") completely inside the visible viewport without cutting off.

---

## 3. Prioritized Implementation Checklist for the Developer

| Priority | Item | Component | Est. Effort |
| :--- | :--- | :--- | :--- |
| **P0** | **Keyboard & Numpad Chaining (Enter to next field, Hotkeys F1-F5)** | Sales / Purchases Entry | 6 hours |
| **P0** | **Plain-Language Error Translation (Zero SQL/code jargon)** | UI Exception Filters | 3 hours |
| **P0** | **Destructive Action Confirmation Guard** | Master Data / Lookups / Ledgers | 3 hours |
| **P1** | **Live Stock Availability & Over-Sell Badges** | Sales Entry Line Editor | 4 hours |
| **P1** | **Customer Balance & Credit Limit Intercept** | Sales Entry Customer Header | 4 hours |
| **P1** | **PKR Currency Live Formatting & SelectAll on Focus** | UI TextBoxes & Behaviors | 4 hours |
| **P1** | **80mm Thermal Receipt Direct Output** | Print Engine / Preview | 5 hours |
| **P2** | **Quick Filter Chips (Active/Inactive, Categories, Areas)** | Master Data / Lookups | 4 hours |
| **P2** | **1366x768 High-DPI Viewport Constraints** | Modal Popups & Layout Grids | 3 hours |

---

## 4. Acceptance Criteria for QA Sign-Off

Before any future developer marks this complete, they must pass these **real-world checks**:
1. Complete a sale of 3 items from start to finish using **only keyboard/numpad** (no mouse touches allowed).
2. Enter a duplicate product code and verify the error message reads like a human spoke it, with zero SQL syntax.
3. Attempt to bill an out-of-stock item and an over-limit customer and verify both trigger blocking visual warnings.
4. Open all forms at 1366x768 with 125% Windows display scaling and verify no "Save" or "Cancel" buttons are cut off.
5. Hide and show the sidebar, verifying table columns automatically stretch cleanly across the entire monitor.
