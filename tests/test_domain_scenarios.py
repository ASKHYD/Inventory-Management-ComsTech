"""
SOFT2027 FMCG Wholesale & Distribution System
Relational & Domain Scenario Verification Test Suite
Verifies Acceptance Test Scenarios 1 through 9 from PRS Section 62.
"""

import sqlite3
import os
import shutil
import unittest

SCHEMA_FILE = os.path.join(os.path.dirname(__file__), '..', 'docs', 'DATABASE_SCHEMA.sql')

class TestSOFT2027DomainScenarios(unittest.TestCase):
    def setUp(self):
        self.conn = sqlite3.connect(':memory:')
        self.conn.row_factory = sqlite3.Row
        with open(SCHEMA_FILE, 'r', encoding='utf-8') as f:
            self.conn.executescript(f.read())
        self.cursor = self.conn.cursor()

        # Create Admin
        self.cursor.execute(
            "INSERT INTO AdminUsers (Username, PasswordHash, FullName) VALUES ('admin', 'hash123', 'Administrator')"
        )
        self.admin_id = self.cursor.lastrowid
        self.conn.commit()

    def tearDown(self):
        self.conn.close()

    def test_scenario_1_new_product(self):
        """Scenario 1: Administrator creates a product with default prices."""
        self.cursor.execute("""
            INSERT INTO Products (ProductCode, ProductName, BasePurchasePrice, PurchasePercentage, DefaultSalePrice, SellingPercentage, MinimumStock)
            VALUES ('PROD-A', 'Product A', 500.0, 5.0, 550.0, 10.0, 20.0)
        """)
        prod_id = self.cursor.lastrowid
        self.conn.commit()

        self.cursor.execute("SELECT * FROM Products WHERE ProductId = ?", (prod_id,))
        prod = self.cursor.fetchone()
        self.assertEqual(prod['ProductCode'], 'PROD-A')
        self.assertEqual(prod['BasePurchasePrice'], 500.0)
        self.assertEqual(prod['DefaultSalePrice'], 550.0)
        self.assertEqual(prod['CurrentStock'], 0.0)

    def test_scenario_2_and_3_purchase_and_supplier_memory(self):
        """
        Scenario 2: First Purchase from Supplier A -> Stock increments, Supplier balance updates, Tier 2 history created.
        Scenario 3: Second Purchase -> System recalls previous purchase rate.
        """
        # Create Product & Supplier
        self.cursor.execute("""
            INSERT INTO Products (ProductCode, ProductName, BasePurchasePrice, PurchasePercentage, DefaultSalePrice, SellingPercentage)
            VALUES ('PROD-A', 'Product A', 500.0, 5.0, 550.0, 10.0)
        """)
        prod_id = self.cursor.lastrowid

        self.cursor.execute("""
            INSERT INTO Suppliers (SupplierCode, SupplierName) VALUES ('SUPP-A', 'Supplier ABC Distributors')
        """)
        supp_id = self.cursor.lastrowid

        # Scenario 2: First Purchase of 100 units @ 500 PKR
        qty = 100.0
        rate = 500.0
        line_total = qty * rate
        inv_no = 'PUR-2026-0001'

        self.cursor.execute("""
            INSERT INTO Purchases (InvoiceNumber, SupplierId, PurchaseDate, SubTotal, NetTotal, PaidAmount, RemainingBalance, AdminId)
            VALUES (?, ?, '2026-10-01 10:00:00', ?, ?, 0.0, ?, ?)
        """, (inv_no, supp_id, line_total, line_total, line_total, self.admin_id))
        pur_id = self.cursor.lastrowid

        self.cursor.execute("""
            INSERT INTO PurchaseItems (PurchaseId, ProductId, Quantity, HistoricalPurchasePrice, LineTotal)
            VALUES (?, ?, ?, ?, ?)
        """, (pur_id, prod_id, qty, rate, line_total))

        # Stock Movement
        self.cursor.execute("""
            UPDATE Products SET CurrentStock = CurrentStock + ? WHERE ProductId = ?
        """, (qty, prod_id))

        self.cursor.execute("""
            INSERT INTO StockMovements (ProductId, MovementDate, MovementType, ReferenceNumber, QuantityIn, BalanceQuantity, AdminId)
            VALUES (?, '2026-10-01 10:00:00', 'PURCHASE', ?, ?, ?, ?)
        """, (prod_id, inv_no, qty, qty, self.admin_id))

        # Supplier Balance & Ledger
        self.cursor.execute("""
            UPDATE Suppliers SET OutstandingBalance = OutstandingBalance + ? WHERE SupplierId = ?
        """, (line_total, supp_id))

        self.cursor.execute("""
            INSERT INTO SupplierLedger (SupplierId, TransactionDate, TransactionType, ReferenceNumber, Description, Credit, RunningBalance)
            VALUES (?, '2026-10-01 10:00:00', 'PURCHASE', ?, 'Purchase Invoice', ?, ?)
        """, (supp_id, inv_no, line_total, line_total))

        # Tier 2 Pricing Memory
        self.cursor.execute("""
            INSERT INTO SupplierProductPricingHistory (SupplierId, ProductId, PurchasePrice, PurchasePercentage, Quantity, InvoiceNumber, TransactionDate, AdminId)
            VALUES (?, ?, ?, 5.0, ?, ?, '2026-10-01 10:00:00', ?)
        """, (supp_id, prod_id, rate, qty, inv_no, self.admin_id))

        self.conn.commit()

        # Assert Scenario 2 outcomes
        self.cursor.execute("SELECT CurrentStock FROM Products WHERE ProductId = ?", (prod_id,))
        self.assertEqual(self.cursor.fetchone()['CurrentStock'], 100.0)

        self.cursor.execute("SELECT OutstandingBalance FROM Suppliers WHERE SupplierId = ?", (supp_id,))
        self.assertEqual(self.cursor.fetchone()['OutstandingBalance'], 50000.0)

        # Scenario 3: Query Tier 2 Pricing Memory for Supplier A + Product A
        self.cursor.execute("""
            SELECT PurchasePrice, PurchasePercentage, Quantity, TransactionDate
            FROM SupplierProductPricingHistory
            WHERE SupplierId = ? AND ProductId = ?
            ORDER BY TransactionDate DESC LIMIT 1
        """, (supp_id, prod_id))
        mem = self.cursor.fetchone()
        self.assertIsNotNone(mem)
        self.assertEqual(mem['PurchasePrice'], 500.0)
        self.assertEqual(mem['PurchasePercentage'], 5.0)

    def test_scenario_4_5_and_6_sales_customer_memory_and_immutability(self):
        """
        Scenario 4: First Sale to Customer A @ negotiated 540 PKR -> Stock decrements, customer balance updates, Tier 3 memory stored.
        Scenario 5: Repeat Sale -> Recalls Previous Sale Price (540 PKR).
        Scenario 6: Master price updated to 580 PKR -> Old invoice strictly preserves 540 PKR and profit calculation remains immutable.
        """
        # Create Product & Customer & initial stock of 100 units
        self.cursor.execute("""
            INSERT INTO Products (ProductCode, ProductName, BasePurchasePrice, PurchasePercentage, DefaultSalePrice, SellingPercentage, CurrentStock)
            VALUES ('PROD-A', 'Product A', 500.0, 5.0, 550.0, 10.0, 100.0)
        """)
        prod_id = self.cursor.lastrowid

        self.cursor.execute("""
            INSERT INTO Customers (CustomerCode, CustomerName) VALUES ('CUST-A', 'Al-Rehman Store')
        """)
        cust_id = self.cursor.lastrowid

        # Scenario 4: First Sale of 10 units @ negotiated 540 PKR (2% discount)
        qty = 10.0
        sale_rate = 540.0
        cost_rate = 500.0 # Captured historical cost
        gross = qty * sale_rate
        disc_amt = gross * 0.02 # 108 PKR
        net_line = gross - disc_amt # 5292 PKR
        inv_no = 'INV-2026-0001'

        self.cursor.execute("""
            INSERT INTO Sales (InvoiceNumber, CustomerId, SaleDate, SubTotal, DiscountAmount, NetTotal, PaidAmount, RemainingBalance, AdminId)
            VALUES (?, ?, '2026-10-05 14:00:00', ?, ?, ?, 0.0, ?, ?)
        """, (inv_no, cust_id, gross, disc_amt, net_line, net_line, self.admin_id))
        sale_id = self.cursor.lastrowid

        self.cursor.execute("""
            INSERT INTO SaleItems (SaleId, ProductId, Quantity, HistoricalSalePrice, HistoricalCostPrice, DiscountPercentage, LineTotal)
            VALUES (?, ?, ?, ?, ?, 2.0, ?)
        """, (sale_id, prod_id, qty, sale_rate, cost_rate, net_line))

        # Decrement Stock
        self.cursor.execute("UPDATE Products SET CurrentStock = CurrentStock - ? WHERE ProductId = ?", (qty, prod_id))
        self.cursor.execute("""
            INSERT INTO StockMovements (ProductId, MovementDate, MovementType, ReferenceNumber, QuantityOut, BalanceQuantity, AdminId)
            VALUES (?, '2026-10-05 14:00:00', 'SALE', ?, ?, 90.0, ?)
        """, (prod_id, inv_no, qty, self.admin_id))

        # Customer Ledger
        self.cursor.execute("UPDATE Customers SET OutstandingBalance = OutstandingBalance + ? WHERE CustomerId = ?", (net_line, cust_id))
        self.cursor.execute("""
            INSERT INTO CustomerLedger (CustomerId, TransactionDate, TransactionType, ReferenceNumber, Description, Debit, RunningBalance)
            VALUES (?, '2026-10-05 14:00:00', 'SALE', ?, 'Sales Invoice', ?, ?)
        """, (cust_id, inv_no, net_line, net_line))

        # Tier 3 Pricing Memory
        self.cursor.execute("""
            INSERT INTO CustomerProductPricingHistory (CustomerId, ProductId, SalePrice, SellingPercentage, DiscountPercentage, Quantity, InvoiceNumber, TransactionDate, AdminId)
            VALUES (?, ?, ?, 8.0, 2.0, ?, ?, '2026-10-05 14:00:00', ?)
        """, (cust_id, prod_id, sale_rate, qty, inv_no, self.admin_id))

        self.conn.commit()

        # Assert Scenario 4
        self.cursor.execute("SELECT CurrentStock FROM Products WHERE ProductId = ?", (prod_id,))
        self.assertEqual(self.cursor.fetchone()['CurrentStock'], 90.0)

        # Scenario 5: Repeat Sale Lookup
        self.cursor.execute("""
            SELECT SalePrice, SellingPercentage, DiscountPercentage, Quantity, TransactionDate
            FROM CustomerProductPricingHistory
            WHERE CustomerId = ? AND ProductId = ?
            ORDER BY TransactionDate DESC LIMIT 1
        """, (cust_id, prod_id))
        mem = self.cursor.fetchone()
        self.assertEqual(mem['SalePrice'], 540.0)
        self.assertEqual(mem['DiscountPercentage'], 2.0)

        # Scenario 6: Modify Product Default Price in Master Table
        self.cursor.execute("UPDATE Products SET DefaultSalePrice = 580.0, BasePurchasePrice = 530.0 WHERE ProductId = ?", (prod_id,))
        self.conn.commit()

        # Historical Immutability Check:
        self.cursor.execute("""
            SELECT si.HistoricalSalePrice, si.HistoricalCostPrice, si.LineTotal, s.NetTotal
            FROM SaleItems si
            JOIN Sales s ON s.SaleId = si.SaleId
            WHERE s.InvoiceNumber = ?
        """, (inv_no,))
        item = self.cursor.fetchone()
        self.assertEqual(item['HistoricalSalePrice'], 540.0)
        self.assertEqual(item['HistoricalCostPrice'], 500.0)

        # Profit calculation using historical snapshots
        historical_profit = (item['HistoricalSalePrice'] - item['HistoricalCostPrice']) * 10.0 - 108.0
        self.assertEqual(historical_profit, 292.0) # (540 - 500)*10 - 108 = 400 - 108 = 292 PKR

    def test_scenario_7_return_workflow(self):
        """Scenario 7: Return adjusts stock, ledger balance, and preserves invoice history."""
        # Setup product and initial sale
        self.cursor.execute("""
            INSERT INTO Products (ProductCode, ProductName, BasePurchasePrice, DefaultSalePrice, CurrentStock)
            VALUES ('PROD-A', 'Product A', 500.0, 550.0, 90.0)
        """)
        prod_id = self.cursor.lastrowid

        self.cursor.execute("""
            INSERT INTO Customers (CustomerCode, CustomerName, OutstandingBalance)
            VALUES ('CUST-A', 'Al-Rehman Store', 5292.0)
        """)
        cust_id = self.cursor.lastrowid

        # Return 2 units @ 540 PKR (with 2% discount refunded = 529.2 per unit -> 1058.4 PKR)
        refund_amt = 1058.4
        ret_no = 'RET-2026-0001'

        self.cursor.execute("""
            INSERT INTO SalesReturns (ReturnNumber, CustomerId, ReturnDate, TotalRefundAmount, AdminId)
            VALUES (?, ?, '2026-10-06 10:00:00', ?, ?)
        """, (ret_no, cust_id, refund_amt, self.admin_id))
        ret_id = self.cursor.lastrowid

        self.cursor.execute("""
            INSERT INTO SalesReturnItems (ReturnId, ProductId, Quantity, RefundRate, LineTotal)
            VALUES (?, ?, 2.0, 529.2, ?)
        """, (ret_id, prod_id, refund_amt))

        # Adjust Stock
        self.cursor.execute("UPDATE Products SET CurrentStock = CurrentStock + 2.0 WHERE ProductId = ?", (prod_id,))
        self.cursor.execute("""
            INSERT INTO StockMovements (ProductId, MovementDate, MovementType, ReferenceNumber, QuantityIn, BalanceQuantity, AdminId)
            VALUES (?, '2026-10-06 10:00:00', 'SALE_RETURN', ?, 2.0, 92.0, ?)
        """, (prod_id, ret_no, self.admin_id))

        # Adjust Customer Ledger
        self.cursor.execute("UPDATE Customers SET OutstandingBalance = OutstandingBalance - ? WHERE CustomerId = ?", (refund_amt, cust_id))
        self.cursor.execute("""
            INSERT INTO CustomerLedger (CustomerId, TransactionDate, TransactionType, ReferenceNumber, Description, Credit, RunningBalance)
            VALUES (?, '2026-10-06 10:00:00', 'SALE_RETURN', ?, 'Sales Return', ?, 4233.6)
        """, (cust_id, ret_no, refund_amt))

        self.conn.commit()

        # Assert stock is back to 92
        self.cursor.execute("SELECT CurrentStock FROM Products WHERE ProductId = ?", (prod_id,))
        self.assertEqual(self.cursor.fetchone()['CurrentStock'], 92.0)

        # Assert customer balance is reduced
        self.cursor.execute("SELECT OutstandingBalance FROM Customers WHERE CustomerId = ?", (cust_id,))
        self.assertAlmostEqual(self.cursor.fetchone()['OutstandingBalance'], 4233.6, places=2)

    def test_scenario_8_and_9_backup_and_restore(self):
        """Scenario 8: Backup database to file. Scenario 9: Restore database and verify pricing memory & transactions."""
        test_db = 'test_live.db'
        backup_db = 'test_backup.bak'

        if os.path.exists(test_db):
            os.remove(test_db)
        if os.path.exists(backup_db):
            os.remove(backup_db)

        # 1. Setup file-based SQLite database with data
        conn_file = sqlite3.connect(test_db)
        with open(SCHEMA_FILE, 'r', encoding='utf-8') as f:
            conn_file.executescript(f.read())
        cur = conn_file.cursor()

        cur.execute("INSERT INTO AdminUsers (Username, PasswordHash, FullName) VALUES ('admin', 'hash', 'Admin')")
        admin_id = cur.lastrowid
        cur.execute("INSERT INTO Products (ProductCode, ProductName, BasePurchasePrice, DefaultSalePrice) VALUES ('P1', 'Test Prod', 100, 120)")
        p_id = cur.lastrowid
        cur.execute("INSERT INTO Customers (CustomerCode, CustomerName) VALUES ('C1', 'Test Cust')")
        c_id = cur.lastrowid
        cur.execute("""
            INSERT INTO CustomerProductPricingHistory (CustomerId, ProductId, SalePrice, SellingPercentage, Quantity, InvoiceNumber, TransactionDate, AdminId)
            VALUES (?, ?, 115.0, 5.0, 10.0, 'INV-TEST-01', '2026-10-08 12:00:00', ?)
        """, (c_id, p_id, admin_id))
        conn_file.commit()

        # Scenario 8: Execute SQLite Online Backup API
        conn_backup = sqlite3.connect(backup_db)
        conn_file.backup(conn_backup)
        conn_backup.close()
        conn_file.close()

        self.assertTrue(os.path.exists(backup_db))
        self.assertGreater(os.path.getsize(backup_db), 0)

        # Scenario 9: Restore to a brand new database connection and verify integrity
        conn_restored = sqlite3.connect(backup_db)
        conn_restored.row_factory = sqlite3.Row
        cur_res = conn_restored.cursor()

        # Check PRAGMA integrity_check
        cur_res.execute("PRAGMA integrity_check;")
        self.assertEqual(cur_res.fetchone()[0], 'ok')

        # Check Pricing Memory survived
        cur_res.execute("SELECT * FROM CustomerProductPricingHistory WHERE CustomerId = ? AND ProductId = ?", (c_id, p_id))
        row = cur_res.fetchone()
        self.assertIsNotNone(row)
        self.assertEqual(row['SalePrice'], 115.0)
        self.assertEqual(row['InvoiceNumber'], 'INV-TEST-01')

        conn_restored.close()

        # Cleanup files
        if os.path.exists(test_db):
            os.remove(test_db)
        if os.path.exists(backup_db):
            os.remove(backup_db)

if __name__ == '__main__':
    unittest.main()
