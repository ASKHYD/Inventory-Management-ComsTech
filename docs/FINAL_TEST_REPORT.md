# Final Test Report — SOFT2027

**Date:** 2026-10-10  
**Candidate:** 1.0.0-rc.2, Windows x64 portable  
**Environment:** Windows workspace; local .NET SDK 8.0.425; disposable SQLite files for automated tests.  
**Scope:** automated suite and packaging/source verification. This is not customer approval or a clean-VM certification.

## Automated verification

- Full automated suite: **129 passed, 0 failed, 0 skipped**.
- Release WPF solution build succeeded. Restore/build report `NU1900` because NuGet vulnerability metadata was unreachable; dependency restore and x64 runtime restore both completed.
- Self-contained Windows x64 Release publish succeeded. Executable: 73,145,041 bytes; PE machine `0x8664`; FileVersion `1.0.0.0`; SHA-256 `42E58E04E2CD7D15B10CA615473DA7752F5511F58B7D8A8F5B2E3C68121A23FB`.
- The updated portable ZIP is assembled with the rebuilt executable, schema, install guide, test report, and current compliance documents. Record its final delivery checksum separately.
- The standard publish script was blocked by the machine's AllSigned PowerShell policy. The policy was not bypassed; the equivalent `dotnet publish` command succeeded.
- Earlier RC1 startup smoke testing used a disposable database and exposed a top-level window. The current RC2 build has not had a separate startup smoke test or visual acceptance. Native UI automation and visual inspection are unavailable here.
- The updated scroll routing compiles in the Release WPF build and the automated suite passes. Direct mouse-wheel interaction across sections and scroll boundaries still needs a manual Windows UI pass.
- Audited XAML buttons, code-created print-preview buttons, and custom sidebar/login/window-control templates. Removed pointer-hover color triggers from buttons and the ComboBox arrow toggle while preserving pressed and keyboard-focus feedback. Automated compilation and tests pass; actual appearance still needs a manual Windows UI pass.
- Styled tooltips consistently with constrained wrapping. Changed the administrator profile chip to open an account menu; sign-out is performed only by its explicit menu action. Automated build and tests pass; menu placement and tooltip appearance still need a manual Windows UI pass.
- Refined the header into a compact 68px toolbar with a responsive search field, restrained header-only hover/focus feedback, and separated window controls. The notification bell opens current low-stock alerts from saved active-product records; only Review inventory navigates away. Rendered layout, interaction, and narrow-window behavior still need a manual Windows UI pass.
- Corrected the notification button hit area and profile button minimum height so the bell and 34px avatar fit fully inside their click targets. Rendered DPI scaling remains for visual confirmation.

## Manual acceptance scenarios

“Pending” means a person must execute and record the scenario on a clean supported Windows machine. Automated repository/coordinator tests are noted separately and are not substituted for customer workflow acceptance.

| # | Scenario | Expected result | Actual result | Status / defect |
|---:|---|---|---|---|
| 1 | First launch and administrator creation | One administrator can be created; secret is hashed; app signs in | Repository tests cover one-time setup and PBKDF2 storage; UI setup was not submitted | Automated pass; manual UI pending |
| 2 | Create product, customer, supplier | Valid records save and are selectable | CRUD/repository tests exist; not manually exercised in the packaged app | Manual pending |
| 3 | Opening stock | Opening quantity appears in current stock and movement history | No clean-install opening-stock workflow was manually checked | Manual pending |
| 4 | Sale with full payment | Sale, stock, ledger, and selected cash/bank account reconcile | Transaction tests cover atomic posting; exact full-payment UI flow not manually run | Manual pending |
| 5 | Sale with partial payment | Paid, balance, stock, ledger, and cash/bank reconcile | Coordinator and financial tests cover partial posting/settlements | Automated coverage; manual pending |
| 6 | Unpaid sale | Allowed credit sale updates receivable and stock once | End-to-end SQLite scenario covers an unpaid sale and persisted reports | Automated pass; manual pending |
| 7 | Purchase with full payment | Purchase, stock, payable, and selected account reconcile | Not manually exercised as a complete UI scenario | Manual pending |
| 8 | Purchase with partial payment | Paid amount and remaining payable reconcile | Coordinator test covers a partially paid purchase | Automated coverage; manual pending |
| 9 | Customer receipt against balance | Receipt lowers receivable and increases selected account | Cash/bank and repeated partial receipt tests cover this | Automated coverage; manual pending |
| 10 | Supplier payment against balance | Payment lowers payable and decreases selected account | Cash/bank and repeated partial payment tests cover this | Automated coverage; manual pending |
| 11 | Partial sales return | Returned quantity, stock, customer balance, ledger, and history reconcile | Repeated fractional partial returns pass; excess return rejected atomically | Automated pass; manual pending |
| 12 | Partial purchase return | Returned quantity, stock, supplier balance, ledger, and history reconcile | Fractional purchase return passes; excess return rejected | Automated pass; manual pending |
| 13 | Stock adjustment and movement history | Reasoned fractional adjustment logs; negative result rejected | Inventory coordinator regression test passes | Automated pass; manual pending |
| 14 | Previous/default/new customer-product pricing | Each option uses intended terms and saves actual terms | Sales price-memory view-model tests cover options/history | Automated coverage; manual pending |
| 15 | Previous/default/new supplier-product pricing | Each option uses intended terms and saves actual terms | Purchase price-memory view-model tests cover options/history | Automated coverage; manual pending |
| 16 | Edit default after old transaction | Historical invoice and cost remain unchanged | E2E SQLite test confirms saved invoice values survive catalog edits | Automated pass; manual pending |
| 17 | Cash and bank reconciliation | Running account balances match posted flows | Repeated partial cash/bank receipt/payment tests pass | Automated pass; transfer workflow still awaits approval |
| 18 | Reports against underlying records | Totals reconcile to posted transaction data | Known-data report repository reconciliation and E2E report tests pass | Automated pass; customer data-set acceptance pending |
| 19 | Backup and restore | Backup is consistent; restore creates safety copy and restores data | Disposable SQLite backup/restore tests pass | Automated pass; manual UI pending |
| 20 | Invalid backup | Invalid/inconsistent backup is rejected; live data remains intact | Incomplete schema and broken-FK restore tests pass | Automated pass; manual UI pending |
| 21 | Restart and data persistence | Posted data remains after closing/reopening the app | Database persistence is covered at storage level; not manually checked on packaged build | Manual pending |
| 22 | Administrator access restrictions | Protected changes reject unauthenticated/inactive accounts | Admin login/status repository checks pass; UI navigation restrictions need manual confirmation | Partial; manual pending |
| 23 | Minimize/maximize/restore/close/resize | Native controls work; minimum size is respected; drafts prompt | Source defines WindowChrome, min size, and draft close prompt; native interactions not performed | Manual pending; connector unavailable |
| 24 | Invoice/bill preview, print, reprint | Saved figures remain unchanged; printer output legible | Saved-value/reprint safety tests exist; physical printer and dialog not verified | Automated safety pass; manual print pending |
| 25 | Clean install/update | App runs without installed .NET; update preserves data | Self-contained x64 package produced; executable startup smoke check passed against a disposable DB on this host | Partial; clean Windows VM install/update/uninstall remains pending |
| 26 | Mouse wheel in every section | Sidebar and workspace scroll; tables scroll internally, then hand off to the page at their limits | Shared routed-wheel handler added and nested reports scroller removed; Release build and automated suite pass | Implementation complete; manual mouse interaction remains pending |
| 27 | Button hover, pressed, disabled, and keyboard-focus states | Labels remain legible with consistent semantic colors in every application section | Shared base and semantic button variants corrected; app compiles; direct visual state inspection unavailable here | Implementation complete; manual visual acceptance remains pending |

## Release defects and blockers

1. **Blocking:** Native WPF window, dialog, printer, first-run UI, and clean install/update/uninstall behavior still require manual acceptance on supported Windows hardware.
2. **Pending approval:** No customer-approved application icon is present in the repository.
3. **Business decisions:** Confirm any transfer/advance-payment and damaged-return workflows before production sign-off.
4. **Release lifecycle:** This candidate targets .NET 8. Reassess the target framework before production because .NET 8 support ends in November 2026.

The package is a portable release candidate and is not production-approved. Executable SHA-256: `42E58E04E2CD7D15B10CA615473DA7752F5511F58B7D8A8F5B2E3C68121A23FB`. Record the delivered ZIP's checksum separately after distribution.

Do not use this report as production approval. Update each pending row with tester, date, target OS/build, actual result, and defect reference after manual execution.
