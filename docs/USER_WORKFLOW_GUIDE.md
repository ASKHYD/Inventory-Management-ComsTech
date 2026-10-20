# SOFT2027 Operator Workflow Guide & Usability Handbook

**Application:** SOFT2027 FMCG Wholesale & Distribution Management System  
**Client:** Ali Traders  
**Developer:** COMSTECH.Solutions  
**Target User:** Wholesale Counter & Back-Office Business Operators

---

## 1. Quick Navigation & Daily Workflow

The sidebar has been reorganized around the daily wholesale business sequence:

1. **OPERATIONS**
   - **Sales Entry:** Create counter cash memos and customer sales invoices. Automatic price and discount recall for returning customers.
   - **Purchase Entry:** Enter supplier bills. Previous rates, negotiated discounts, and historical invoices are recalled automatically.
   - **Sales Returns:** Process customer product returns against previous invoices.
   - **Purchase Returns:** Record goods returned to suppliers.

2. **INVENTORY**
   - **Current Stock & Movements:** Check real-time stock balances, track incoming/outgoing stock movements, and make manual stock adjustments.

3. **PARTIES & MASTER DATA**
   - **Products:** Manage product names, barcodes, purchase prices, default sale prices, minimum stock warning levels, and active status.
   - **Customers:** Manage customer names, phone numbers, delivery addresses, territories, opening balances, and credit limits.
   - **Suppliers:** Manage vendor names, contact numbers, addresses, and opening balances.
   - **Categories, Brands & Units:** Maintain the lookup lists used to categorize products.

4. **ACCOUNTS & FINANCE**
   - **Receipts:** Record payments received from customers (cash, bank transfer, or cheque).
   - **Payments:** Record supplier payments.
   - **Expenses:** Record daily overhead expenses (utilities, fuel, maintenance, transport).
   - **Cash & Bank:** View real-time cash drawer and bank balances.
   - **Customer Ledger:** View running customer statements with debit/credit history.
   - **Supplier Ledger:** View vendor account statements.

5. **REPORTS**
   - **Reports Dashboard:** Access real-time sales summaries, stock valuation, inventory turn, profit & loss, and audit reports.

6. **SYSTEM** (Administrator Only)
   - **Backup & Restore:** Create secure local database backups before day-end or restore previous records.
   - **Users & Activity:** Manage system operators, reset passwords, and inspect security audit logs.

---

## 2. Standard Master Data CRUD Operations

Every master data screen (Products, Customers, Suppliers, Categories, Brands, Units) follows the same interaction pattern:

### A. Finding a Record
1. Click the **Search** box or press `Ctrl + K`.
2. Type any part of the record name, item code, phone number, or territory.
3. The list filters instantly. Clear the search box to view all records again.

### B. Adding a New Record
1. Click the **+ Add [Record]** button in the top toolbar.
2. A new draft row is inserted at the bottom of the table with auto-generated unique codes (e.g. `P-...`, `C-...`, `S-...`).
3. Click into the table cells to enter the name, rate, contact details, or lookup values.
4. Click **Save Changes** in the toolbar. A confirmation announcement appears when the record is saved.

### C. Copying Details (for WhatsApp, Email, or Excel)
1. Select any row in the table.
2. Right-click the row and select **Copy Record Details**, or click **Copy Details** at the bottom of the screen.
3. The complete record summary is copied to the Windows clipboard and can be pasted (`Ctrl + V`) directly into WhatsApp, an email, or a spreadsheet.

### D. Safe Record Deactivation vs. Deletion
- **Unsaved Drafts:** If you added a new draft row and want to remove it before saving, select the row and click **Delete**. The draft is immediately removed.
- **Saved Records (with Financial History):** In wholesale accounting, records referenced by sales invoices, purchase bills, or ledger entries must not be permanently deleted. Selecting **Delete** or clicking **Deactivate** changes the record status to `Inactive`.
  - Inactive records remain safely preserved in transaction history and reports.
  - Inactive records do not appear as choices when creating new invoices or purchase bills.
  - An inactive record can be reactivated at any time by selecting it and clicking **Reactivate Selected**.

---

## 3. Keyboard Shortcuts & Clipboard Operations

- **`Ctrl + C`**: Copies selected text from any field or table cell.
- **`Ctrl + A`**: Selects all text in the active search box or text field.
- **`Ctrl + V`**: Pastes text into an editable field.
- **`Ctrl + K`**: Instantly highlights the global search box.
- **`Escape`**: Closes open menus, popups, or notification trays.
- **`Enter`**: In search boxes, executes the search and jumps to matching records.

---

## 4. Notifications & Stock Warnings

- **Top Bar Notification Bell:** A red dot appears when any active product has fallen to or below its minimum stock level.
- **Quick Review:** Click the notification bell to view the top low-stock items. Click **Review inventory** to open the full inventory management screen.
- **Save Feedback:** Every successful transaction, save, or deactivation produces a green status announcement at the bottom of the screen confirming that data was written to the local disk.
