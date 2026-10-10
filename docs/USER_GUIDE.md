# SOFT2027 User Guide

This guide is for everyday operators. Button names may vary slightly between builds.

## 1. First launch

1. Open `SOFT2027.UI.exe`.
2. On the first-use setup screen, create the primary administrator username and a strong password. No default credentials are shipped.
3. Sign in. Use **Help & Tour** in the navigation area to open the in-app walkthrough.
4. Open **Categories, Brands & Units** and add the lists your business uses.
5. Add suppliers and customers, then add products with their unit, minimum stock, and default prices.
6. Record opening stock using the inventory tools before entering routine sales.

## 2. Sales invoice

1. Open **Sales Entry** and choose a customer.
2. Choose a product from the product list. Use the search text in the dropdown to narrow the list.
3. Enter quantity, price, and any discount. Use **Use previous**, **Use default**, or **Enter new price** for the price-memory choices.
4. Press **Up** or **Down** while the quantity field is focused to increase or decrease it by one. Numeric quantities may include decimals for fractional units.
5. Select **Add Item** for each product, review totals, enter the amount received, and choose the receiving account.
6. Select **Save Invoice**. The application validates stock and posts the sale with the related stock, balance, payment, and price-history changes.
7. To preview or reprint a posted invoice, choose it from **Saved invoice** history or enter its invoice number, then select **Preview / Print**. **Preview latest saved** opens the last invoice posted in the current Sales Entry session. The preview uses portrait A4 and the supplied cash-memo fields: Ali Traders, Latifabad No. 11, Cell 0336-1841496, customer/code, item/rate/discount/amount, totals, and Customer Signature / Prepared By / Checked By lines. Choose **Print recent 4-up A4** to preview up to four different recent saved invoices on one A4 page, matching the supplied four-cash-memos example. Select **Print…** and choose a printer and supported paper size. Amounts are shown in PKR. Closing the print dialog cancels printing; it does not change the saved sale. Reprints use the line descriptions and rates saved on the original sale.

## 3. Purchase bill

1. Open **Purchase Entry** and choose a supplier and product.
2. Enter quantity, purchase rate, and discount. The same Up/Down quantity keys work here.
3. Add each product, review the bill total, amount paid, and payment source.
4. Select **Save Purchase** to post the bill and increase stock.
5. To preview or reprint a posted bill, choose it from **Saved bill** history or enter its bill number, then select **Preview / Print**. **Preview latest saved** opens the last bill posted in the current Purchase Entry session. The preview uses a portrait A4 business header and signature footer; the supplied sample shows sales cash memos rather than a purchase bill. Select **Print…** and choose a printer and supported paper size. Amounts are shown in PKR. Closing the print dialog cancels printing; it does not change the saved purchase. Reprints use the line descriptions and rates saved on the original bill.

## 4. Returns

Open **Sales Returns** or **Purchase Returns**, enter the original invoice/bill reference, and select **Load Invoice**. Enter the quantities being returned (not more than the original quantities), review the return, and save it. The original transaction remains in history.

## 5. Inventory

Open **Current Stock & Movements** to review stock and recent movements. Select a product to adjust stock. Enter a positive quantity to add stock or a negative quantity to remove it, provide a reason, and save the adjustment. Use Up/Down to change the value by one.

## 6. Ledgers and finance

- **Customer Ledger** and **Supplier Ledger** show the selected party's posted activity and running balance.
- **Receipts** record money received from a customer.
- **Payments** record money paid to a supplier.
- **Expenses** records business expenses against a cash or bank source.
- **Cash & Bank** shows account balances and transaction activity.
- **Reports** includes an operating dashboard with today's and selected-period sales/purchases, daily trends, receivables/payables, stock valuation, low stock and recent activity. Activity rows open their related module. The report catalog includes sales/purchase summaries, returns, discounts and payment status; current stock, valuation, movements, adjustments and low stock; customer and supplier balances, statements, histories and price memory; expenses, cash/bank activity, receipts/payments, product pricing and profitability, and profit/loss. Choose a report, set the date range and any relevant party/product/category filters, then select **Run report**. Use **Export CSV** to save the displayed results. Empty matches are shown explicitly. Profitability uses sale-line historical cost snapshots; stock valuation uses current stock at current default purchase cost.

## 7. Administration and data safety

- Use **Administration** to manage administrator accounts and review recorded activity.
- Use **Backup & Restore** to create a database backup before major changes or updates. Restore asks for confirmation, validates the backup, and creates a safety copy before replacing current business data.
- If a restore fails, keep the current database and backups in place; check the error and select another verified backup. Safety copies are under `%LOCALAPPDATA%\SOFT2027\Backups`.
- Keep a copy of the database backup somewhere other than the computer running SOFT2027.
- Never share passwords, database files, backup files, or unredacted screenshots containing customer or supplier data.

## 8. Updating the application

1. Close SOFT2027 before replacing the portable application folder.
2. Keep a current backup of `%LOCALAPPDATA%\SOFT2027\soft2027.db`.
3. Extract the new portable build and replace only the application folder. Do not remove `%LOCALAPPDATA%\SOFT2027`.
4. Confirm the sign-in screen and verify the main workflow using test records before resuming live work.

The database is stored separately from the executable, so replacing the application folder does not remove business data. To uninstall, remove the shortcut and extracted application folder only; leave the database and backups unless the owner separately requests data removal after checking another backup.
# Storage and backups

The default database is stored at `%LOCALAPPDATA%\SOFT2027\soft2027.db`. Database backups created through the application are saved to the location selected by the operator. A successful restore first creates a safety copy under `%LOCALAPPDATA%\SOFT2027\Backups`. Keep a separate backup copy on removable media or another approved storage location; a backup stored only on the same computer is not protection against device loss.

For an isolated test or alternate local database, set `SOFT2027_DATABASE_PATH` to a full `.db` path before launching the application. This overrides the default path for that process.
