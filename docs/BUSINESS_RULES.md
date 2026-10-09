# SOFT2027: Business Rules & Domain Formulas Specification
## FMCG Wholesale & Distribution Management System

**Version:** 1.0 (Phase 1 Baseline)  
**Author:** COMSTECH.Solutions  
**Target Environment:** Windows Desktop (C# / .NET WPF, SQLite)  
**Scope:** 100% Offline-First Wholesale & Distribution Management

---

## 1. The Three-Tier "Business Memory" System

The system operates as an intelligent business assistant to minimize repetitive manual lookup of prices, percentages, and prior customer/supplier arrangements.

```
+-------------------------------------------------------------------------+
|                               TIER 1                                    |
|                        Product Default Pricing                          |
|         Stored in `Products` (BasePurchasePrice, DefaultSalePrice)       |
+-------------------------------------------------------------------------+
                                    |
             +----------------------+----------------------+
             |                                             |
             v                                             v
+---------------------------+                +---------------------------+
|          TIER 2           |                |          TIER 3           |
|  Supplier Pricing Memory  |                |  Customer Pricing Memory  |
| Stored in `Supplier-      |                | Stored in `Customer-      |
| ProductPricingHistory`    |                | ProductPricingHistory`    |
+---------------------------+                +---------------------------+
```

### 1.1 Tier 1: Product Default Pricing
- Configured in the `Products` master table.
- Stores baseline cost and selling references:
  * `BasePurchasePrice`: Standard purchase cost.
  * `PurchasePercentage`: Standard expected purchase discount or margin.
  * `DefaultSalePrice`: Standard wholesale selling price.
  * `SellingPercentage`: Standard default markup or margin percentage.
- **Trigger:** Used whenever a customer or supplier transacts an item for the very first time (no prior transactional history exists).

### 1.2 Tier 2: Supplier/Product Pricing Memory
- Tracks all purchase transactions between a specific Supplier and Product.
- Stored in `SupplierProductPricingHistory`.
- Captured fields:
  * `PurchasePrice`: Exact purchase price on last bill.
  * `PurchasePercentage`: Last purchase percentage.
  * `DiscountPercentage`: Additional line discount.
  * `Quantity`: Quantity purchased last time.
  * `TransactionDate`: Date of last purchase.
  * `InvoiceNumber`: Last purchase bill reference.
- **Workflow:** When an administrator creates a Purchase Bill and selects Supplier $S$ and Product $P$, the system displays the Tier 2 memory panel immediately.

### 1.3 Tier 3: Customer/Product Pricing Memory
- Tracks negotiated sales terms between a specific Customer and Product.
- Stored in `CustomerProductPricingHistory`.
- Captured fields:
  * `SalePrice`: Negotiated selling price on last invoice.
  * `SellingPercentage`: Applied percentage rule.
  * `DiscountPercentage`: Negotiated line discount.
  * `Quantity`: Quantity sold last time.
  * `TransactionDate`: Date of last invoice.
  * `InvoiceNumber`: Prior invoice number.
- **Workflow:** When an administrator creates a Sales Invoice and selects Customer $C$ and Product $P$, the system queries the latest record for $(C, P)$ and populates the inline Pricing Memory panel.

---

## 2. Price Selection Resolution Matrix

During line-item data entry, the operator has complete autonomy:

| Scenario | Lookup Result | Default UI Value | Options Available |
| :--- | :--- | :--- | :--- |
| **Sales (Existing Pair)** | Found in Tier 3 | Last Negotiated Sale Price | `[Alt+P] Use Previous`, `[Alt+D] Use Default`, `[Alt+N] Enter New` |
| **Sales (New Pair)** | None in Tier 3 | Master Default Sale Price | `[Alt+D] Use Default`, `[Alt+N] Enter New` |
| **Purchase (Existing Pair)** | Found in Tier 2 | Last Purchase Rate | `[Alt+P] Use Previous`, `[Alt+D] Use Default`, `[Alt+N] Enter New` |
| **Purchase (New Pair)** | None in Tier 2 | Master Default Purchase Price | `[Alt+D] Use Default`, `[Alt+N] Enter New` |

### New Price Capture Rule
When the operator selects `[Enter New]` and overrides the rate, saving the invoice commits this new rate to `CustomerProductPricingHistory` or `SupplierProductPricingHistory`. On the next transaction with that party, this new rate automatically becomes the "Previous Price".

---

## 3. Mathematical Formulas & Immutability Rules

### 3.1 Line Item Total Calculation
$$\text{GrossLineTotal} = \text{Quantity} \times \text{SelectedPrice}$$
$$\text{DiscountAmount} = \text{GrossLineTotal} \times \left(\frac{\text{DiscountPercentage}}{100}\right)$$
$$\text{NetLineTotal} = \text{GrossLineTotal} - \text{DiscountAmount}$$

### 3.2 Invoice Subtotal & Net Total
$$\text{SubTotal} = \sum_{i=1}^{n} \text{NetLineTotal}_i$$
$$\text{InvoiceNetTotal} = \text{SubTotal} - \text{InvoiceHeaderDiscount}$$
$$\text{RemainingBalance} = \text{InvoiceNetTotal} - \text{PaidAmount}$$

### 3.3 Historical Cost & Profit Immutability
To guarantee that past profit figures remain permanent and tamper-proof:
1. Every line item in `SaleItems` captures two price snapshots at the exact moment of sale:
   * `HistoricalSalePrice`: The price charged to the customer.
   * `HistoricalCostPrice`: The inventory cost of the product at that time (from the batch or product master).
2. Profitability is strictly evaluated as:
   $$\text{GrossProfit}_{\text{Item}} = (\text{HistoricalSalePrice} - \text{HistoricalCostPrice}) \times \text{Quantity} - \text{DiscountAmount}$$
3. **Immutability Invariant:** If `Products.DefaultSalePrice` or `Products.BasePurchasePrice` is subsequently modified in the Master catalog, existing rows in `SaleItems` and `Sales` are **never updated**. Past financial reports remain mathematically unchanged.

---

## 4. Multi-Table Atomic Transaction Boundaries

All transactional operations must execute within an isolated SQLite ACID transaction scope (`BEGIN TRANSACTION ... COMMIT`). If any constraint or write fails, the entire transaction rolls back (`ROLLBACK`).

### 4.1 Sales Invoice Posting Boundary
1. Insert `Sales` header record.
2. Insert all `SaleItems` records (stamping historical snapshot rates).
3. Decrement `Products.CurrentStock` by item quantities.
4. Insert corresponding `StockMovements` records with `MovementType = 'SALE'`.
5. Update `Customers.OutstandingBalance += RemainingBalance`.
6. Insert `CustomerLedger` debit entry (`Debit = NetTotal`, `RunningBalance = NewBalance`).
7. If `PaidAmount > 0`, insert `CustomerLedger` credit entry (`Credit = PaidAmount`).
8. Update/Insert `CustomerProductPricingHistory` for each product.
9. Insert `AuditLogs` entry.

### 4.2 Purchase Bill Posting Boundary
1. Insert `Purchases` header record.
2. Insert all `PurchaseItems` records.
3. Increment `Products.CurrentStock` by item quantities.
4. Insert `StockMovements` records with `MovementType = 'PURCHASE'`.
5. Update `Suppliers.OutstandingBalance += RemainingBalance`.
6. Insert `SupplierLedger` credit entry (`Credit = NetTotal`, `RunningBalance = NewBalance`).
7. If `PaidAmount > 0`, insert `SupplierLedger` debit entry (`Debit = PaidAmount`).
8. Update/Insert `SupplierProductPricingHistory` for each product.
9. Insert `AuditLogs` entry.

---

## 5. Scope Boundaries

The following features are strictly **EXCLUDED** from design and implementation:
* ❌ Cloud synchronization or remote server polling
* ❌ Multi-company legal accounting consolidations
* ❌ Online payment gateways (Stripe, PayPal, etc.)
* ❌ Public customer web portals
* ❌ AI chatbots or external cloud LLM dependencies
