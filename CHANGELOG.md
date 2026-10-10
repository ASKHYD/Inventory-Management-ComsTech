# Changelog

## 1.0.0-rc.2 — 2026-10-10

- Redesigned the existing header as a compact 68px toolbar with a two-line brand, responsive centered search, balanced notification/profile actions, and separated native-style window controls.
- Fixed header icon/avatar clipping by sizing notification hit areas and the profile action for its 34px avatar and padding.
- Kept the notification menu data-driven from current active product stock and preserved explicit navigation through its Review inventory action.
- Styled tooltips consistently and changed the administrator header chip to open a profile menu with an explicit Sign out action.
- Refined top-bar spacing and focus styling; changed the notification bell to open persisted low-stock alerts, with inventory navigation available only through its Review inventory action.
- Fixed mouse-wheel routing across the application so tables scroll internally when possible and the current page continues scrolling at table boundaries.
- Removed the Reports Dashboard's nested page scroller to keep it inside the shared workspace viewport.
- Removed pointer-hover color changes from shared and custom buttons, including primary, dark, danger, success, navigation, login, and window-control buttons. Pressed and keyboard-focus feedback remain.
- Removed hover styling from the ComboBox arrow toggle so dropdown controls behave consistently with the no-button-hover request.
- Rebuilt the self-contained Windows x64 executable in `publish/win-x64-rc.2`.
- Audited shared and custom button styles across the application and corrected unreadable disabled labels, keyboard-focus visibility, sign-in color overrides, and inconsistent hover behavior.
- Bumped the portable candidate to `1.0.0-rc.2` while preserving any already running `rc.1` executable.
- Prepared a portable Windows x64 release candidate and documented install, update, and data-preservation procedures.
- Added Start Menu and optional desktop shortcut helper scripts; shortcut removal leaves application data and backups untouched.
- Added restore validation for SQLite integrity and foreign-key consistency, plus first-run administrator, audit, and restore tests.
- Added confirmation before closing with an unsaved sales, purchase, or return draft.
- Fixed the editable product dropdown arrow hit area and clarified product-selection validation.
- Production sign-off is still pending target-machine installation testing, native desktop acceptance, the approved Ali Traders icon, and customer approval of outstanding workflows.

## Unreleased — 2026-10-09

- Added numeric-only quantity entry with Up/Down keyboard adjustment to sales, purchase, return, and stock adjustment fields, including editable line-item tables.
- Kept stock adjustments signed so operators can add or remove inventory.
- Fixed editable dropdown selected text visibility and the Inventory low-stock count binding that could throw at runtime.
- Corrected cash and bank transaction type values used when posting sales and purchases.
- Added a first-run, operator-focused user guide and a PRS coverage review with known gaps.
- Added a project update and deployment readiness report; production deployment remains gated on PRS completion and full acceptance.

## Initial repository import

- Added the SOFT2027 WPF desktop application, SQLite schema, business rules, and existing automated test sources.
