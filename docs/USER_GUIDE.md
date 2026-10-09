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

## 3. Purchase bill

1. Open **Purchase Entry** and choose a supplier and product.
2. Enter quantity, purchase rate, and discount. The same Up/Down quantity keys work here.
3. Add each product, review the bill total, amount paid, and payment source.
4. Select **Save Purchase** to post the bill and increase stock.

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
- **Reports** currently provides profit/loss, stock valuation, and a customer statement.

## 7. Administration and data safety

- Use **Administration** to manage administrator accounts and review recorded activity.
- Use **Backup & Restore** to create a database backup before major changes or updates. Restore is restricted to authorized administrators and replaces current business data.
- Keep a copy of the database backup somewhere other than the computer running SOFT2027.
- Never share passwords, database files, backup files, or unredacted screenshots containing customer or supplier data.

## 8. Updating the application

1. Close SOFT2027 before replacing its executable.
2. Keep a current backup of `%LOCALAPPDATA%\SOFT2027\soft2027.db`.
3. Replace the application executable with the new build and launch it.
4. Confirm the sign-in screen and verify the main workflow using test records before resuming live work.

The database is stored separately from the executable, so replacing the application file does not normally remove business data. Do not delete the SOFT2027 folder under Local AppData when updating.
