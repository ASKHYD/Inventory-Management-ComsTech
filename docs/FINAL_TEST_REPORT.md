# SOFT2027 — Software and Final QA Report

**Report date:** 2026-10-13
**Application:** SOFT2027 — Ali Traders Wholesale & Distribution ERP
**Publisher:** ComsTech Solutions Private Limited
**Candidate version:** 1.0.0-rc.2
**Target:** WPF desktop, .NET 8, Windows x64
**Recommendation:** **Retest required before production use.** The source is buildable and the XAML/startup checks pass, but manual business workflow acceptance and the open ledger/report findings remain.

## Product overview

SOFT2027 is an offline-first wholesale and distribution application. Its modules cover sales and purchases, returns, products and parties, inventory and movements, customer and supplier ledgers, receipts/payments, expenses, cash and bank, reports, user administration, audit history, and database backup/restore. The application uses a local SQLite database.

## Clean start and user data

- No business database or dummy database is part of the source release. Database and SQLite sidecar extensions (`.db`, `-wal`, `-shm`) are ignored by Git.
- On first launch, the app initializes an empty database at `%LOCALAPPDATA%\SOFT2027\soft2027.db` and presents first-use administrator setup. There is no default username or password.
- The QA launcher is `qa-artifacts\Run-SOFT2027-QA-Fresh.cmd`. It uses a separate empty QA database; it does not copy or seed the old dummy backup. Automated data-loading checks used a disposable seeded copy; generated QA database files were removed after verification.
- Existing local business data is stored outside the repository and is not changed by this report or build.

## Current verification

| Check | Result | Evidence / limitation |
|---|---|---|
| Release solution build | **Pass** | `dotnet build SOFT2027.sln --configuration Release --no-restore`; 0 errors, 3 NU1900 vulnerability-feed warnings. |
| NuGet restore | **Pass with warning** | New smoke project restored; packages were available locally. `api.nuget.org` vulnerability metadata was unreachable (NU1900). No new packages were added. |
| XAML smoke suite | **Pass** | All registered view roots rendered; 74 DataGrid header contrast checks passed at 16.48:1; other rendered-state checks passed. |
| Maximize/restore state | **Pass in smoke** | Normal and maximized states show distinct vector geometry with visible stroke. Native manual interaction still needs user confirmation. |
| QA first-start database | **Pass** | QA launcher startup probe opened the app with a fresh, empty database in an isolated folder. |
| Master-data loading smoke | **Pass on seeded QA copy** | Customers, Suppliers, Products, Lookups, Users and Activity Log loaded rows/headers without Refresh; navigate-away-and-back check passed. This seeded copy is test-only and excluded from Git. |
| Preview smoke | **Pass in code-built UI check** | Single and four-up preview rendered; zoom changed rendered page bounds from 397×561 at 50% to 794×1122 at 100%. Printer output and native dialogs were not tested. |
| Full coordinator/unit suite | **Historical result** | The prior report records 129 passed, 0 failed, 0 skipped on 2026-10-10. This suite was not rerun after the current UI merge; do not treat that count as a fresh run. |
| Full customer end-to-end acceptance | **Needs manual test** | No complete manual sales/purchase/finance/restore/printing pass was performed for this handoff. |
| Publish package | **Not rebuilt for this handoff** | Source Release build verified; make a new portable package only after manual acceptance and release-version review. |

Build warning detail: NU1900 reports that the NuGet vulnerability service index at `https://api.nuget.org/v3/index.json` could not be loaded. This did not block restore or compilation.

## Functional acceptance still required

Run these on a clean supported Windows machine using a disposable QA database, then record tester, build, Windows version, resolution/scaling, expected and actual results, and evidence.

1. Complete first administrator setup; create and save a QA product, customer, and supplier.
2. Create a sale with full, partial, and no payment; reconcile stock, customer balance, ledger, and cash/bank.
3. Create a purchase with full and partial payment; reconcile stock, supplier balance, ledger, and cash/bank.
4. Verify partial and repeated sales/purchase returns, rejected over-return, duplicate/retry, and cancel paths.
5. Verify receipts, payments, expenses, ledgers, dashboard/report filters, and price-memory selection against saved transactions.
6. Preview, reprint, and four-up preview without creating a second transaction or stock movement; print on the target printer.
7. Test backup, restore, invalid-backup rejection, safety copy, and restart persistence using disposable copies.
8. Test admin/employee navigation, audit records, minimize/maximize/restore, drag/snap/resize, close confirmation, keyboard navigation, 125% scaling, and smaller window size.
9. Test Report a Bug with no email handler, Gmail logged out, WhatsApp unavailable, and offline. Confirm no information is sent automatically and report content contains no secrets or database contents.

## Open findings

### CALC-04 — Customer ledger running balance (High; confirmed on the earlier QA copy)

The statement displays ledger rows by `TransactionDate ASC, LedgerId ASC` (`CustomerLedgerRepository.GetByCustomerIdAsync`), but `RunningBalance` is the value stored at posting time by `TransactionCoordinator`. A backdated transaction therefore appears earlier in the display with a balance calculated after later-posted rows.

| Display order | Reference | Expected calculation | Expected balance | Stored balance |
|---:|---|---:|---:|---:|
| 1 | QA-SAL-001 | 0 + 1,140 − 500 | 640.00 | 640.00 |
| 2 | INV-20261013022010865 | 640 + 540 | 1,180.00 | 745.00 |
| 3 | QA-SRET-001 | 1,180 − 67.50 | 1,112.50 | 572.50 |
| 4 | QA-SRET-002 | 1,112.50 − 67.50 | 1,045.00 | 505.00 |
| 5 | QA-REC-001 | 1,045 − 200 | 845.00 | 305.00 |
| 6 | QA-REC-002 | 845 − 100 | 745.00 | 205.00 |

The displayed customer closing balance is 745.00, but the final row says 205.00. Severity is High because the statement gives misleading intermediate and final row balances. Supplier ledgers checked on the same copy matched their displayed ordering. **This is report-only; the calculation and query have not been changed.**

### Dashboard / account reconciliation — unresolved candidate

The prior test request flagged Dashboard Total Revenue **1,770** versus Period Sales **1,905** (difference **135**) and asked to reconcile Receivables **970**, Payables **2,700**, Inventory at cost **3,550**, COGS **1,250**, and Net profit **445**. A verified transaction-by-transaction reconciliation is not present in the evidence available for this handoff. Treat the variance as unresolved; do not mark the figures reconciled or accepted. Reproduce with an approved QA dataset and attach the source rows before assigning a final defect cause/severity.

### Business rules requiring owner confirmation

- Inter-account cash/bank transfers and customer/supplier advance payments need client/CTO confirmation.
- Damaged-return disposition is not separately modeled; confirm whether returned goods should always re-enter stock or be tracked separately.
- Confirm discount order, rounding, tax, and inventory cost/profit basis before production sign-off.

## GitHub handoff status

- The completed UI/data-loading/preview source changes and their report are now in the main repository tree; the XAML smoke project is included in `SOFT2027.sln`.
- `.gitignore` excludes the isolated work copy and local QA data/build outputs. No database, WAL/SHM file, customer record, or dummy backup should be committed.
- Current candidate metadata remains `1.0.0-rc.2`; it was not promoted to a production version because manual acceptance is incomplete.
- The working tree has not been committed or pushed. Review `git status`, stage source/docs only, and publish to GitHub when the final QA gates are accepted.

## Release decision

The source is in a GitHub-reviewable state and builds successfully. The release is **not production-approved**: CALC-04 remains open, the dashboard discrepancy is unreconciled, business rules need confirmation, and the Windows manual acceptance list above is pending. The portable package was not rebuilt for this handoff.
