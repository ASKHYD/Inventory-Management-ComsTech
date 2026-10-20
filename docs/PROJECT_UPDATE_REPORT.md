# SOFT2027 Project Update and Deployment Readiness Report

**Review date:** 2026-10-13
**Repository:** [Inventory-Management-ComsTech](https://github.com/MIbraheemDaudpoto/Inventory-Management-ComsTech)
**Reviewed source commit:** `753c925` — `Add SOFT2027 inventory management application and docs`

## Deployment decision

**Not ready for production deployment.** A prior full test run passed, but it predates the latest UI merge. Windows hardware acceptance, current full-suite rerun, and customer approvals remain open. Do not use this readiness review as approval to install over a live business database.

The latest source and UI verification are summarized in [Final Test Report](FINAL_TEST_REPORT.md). Current source Release build passes; the previous 129-test result is historical and the full suite was not rerun after the latest UI merge. The application starts with an empty user-local database and has no default credentials. No database is included in the GitHub source tree.

## Work included in the uploaded update

- Added the .NET 8 WPF desktop application, SQLite data schema, and business rules to the repository.
- Added sales and purchase flows, return screens, customer/supplier ledgers, inventory adjustment and history, master-data screens, administration/audit history, finance, and backup/restore screens.
- Added numeric quantity input with Up/Down keyboard adjustment for sales, purchases, returns, and stock adjustments.
- Fixed editable dropdown selected-value visibility and the Inventory low-stock binding issue.
- Corrected cash/bank transaction-type values used when sales and purchases are posted.
- Added setup/run instructions, operator guide, changelog, and PRS coverage review.
- Added saved invoice/purchase bill previews, a four-distinct-invoice A4 sales sheet, and immutable sale/purchase line description snapshots with an idempotent upgrade migration.
- Completed the operational reporting catalog and added persisted-data dashboard KPIs, trends, and recent activity.
- Defined price-memory selection and percentage rules with first-time, previous/default/manual selection coverage.
- Added service-layer guards that reject negative-stock sales and receipts/payments above the party's outstanding balance; added saleable stock and party ledger reconciliation coverage for repeated partial returns, including rejected over-returns.
- Added repeated partial cash/bank receipt and payment reconciliation tests and supplier return stock/payable reconciliation.
- Added close confirmation when sales, purchase, or return drafts contain line items; documented default database and safety-backup paths and added a per-process database path override for isolated testing.
- Hardened backup restore with SQLite integrity and foreign-key checks before replacement, and added tests for successful restore, invalid backup rejection, and preservation of live data.
- Added admin repository acceptance tests for password hashing, first-run restriction, login behavior, admin deactivation, password reset, and audit records.
- Produced a self-contained Windows x64 portable release candidate with install/update/uninstall guidance and current test report. Customer selected the portable-folder format; no installer is included.
- Routed mouse-wheel input through the nearest scrollable table or panel and onward to the page at scroll boundaries; removed the nested Reports Dashboard page scroller.

## Verification completed

- Built the full solution in Release with the locally installed .NET 8 SDK: succeeded. NuGet emitted `NU1900` because vulnerability metadata was unavailable.
- Ran the full solution test suite: **129 passed, 0 failed, 0 skipped**.
- Rebuilt after the shared mouse-wheel routing change; Release compilation succeeded and the complete 129-test suite passed.
- Published a self-contained win-x64 single-file executable and assembled `SOFT2027-1.0.0-rc.2-win-x64-portable.zip`. The earlier RC1 executable had a disposable-database startup smoke check; RC2 still needs its own startup check.
- End-to-end SQLite coverage reconciles sales/P&L/dashboard totals, verifies saved invoice values after product edits, and confirms print-data retrieval does not duplicate sales or change stock.
- Automated business scenarios use disposable SQLite test databases. The WPF process starts against a disposable database, but native UI automation was unavailable in this session, so visual/manual window-control acceptance remains outstanding. No live-business acceptance run was performed.

## Open release gates

1. Obtain Ali Traders' final approval for the A4 memo/bill layout, footer/terms and print settings; verify output with the target Windows printer.
2. Complete business acceptance of the report catalog, dashboard and pricing history using the approved known dataset.
4. Confirm the required bank workflow with the business, including whether transfers are needed.
5. Complete end-to-end acceptance for stock adjustment, opening-stock setup, backup/restore, native window controls and print output on a supported Windows machine.
6. Verify database upgrade behavior against a copy of an existing database and document recovery steps before replacing a live installation.
7. Customer selected a portable folder; the package includes optional shortcut scripts. Test installation/update/uninstall on a clean Windows machine. No installer is included.
8. Obtain an approved app icon and distribution-signing decision. Package is a release candidate, not a signed/production distribution.
9. Reassess .NET target framework before production; .NET 8 support ends November 2026.

## Recommended next sequence

1. Approve the invoice/bill layout and confirm the bank workflow.
2. Verify printed documents and upgrade behavior on target Windows hardware and a copy of an existing database.
3. Execute remaining release-gate scenarios against disposable databases, including backup/restore, returns, printing and native UI controls.
4. Complete clean-machine package acceptance, framework lifecycle review, and customer sign-off before production deployment.

## Scope note

The linked PRS coverage document distinguishes features represented in source from acceptance-tested behavior. Passing a compile does not establish accounting correctness, restore safety, or production suitability.
