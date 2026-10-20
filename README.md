# SOFT2027 Inventory Management

SOFT2027 is an offline-first Windows desktop inventory and wholesale management application for Ali Traders. It is built with WPF on .NET 8 and stores business data in a local SQLite database.

## Features

- Sales invoices and purchase bills with line items, discounts, payments, balances, and saved customer/product or supplier/product price history.
- Numeric quantity entry in transaction, return, and stock adjustment screens. Press **Up** or **Down** in a quantity field to increase or decrease the value by one. Stock adjustments accept negative values.
- Sales and purchase returns linked to the original transaction where available.
- Product, customer, supplier, category, brand, and unit management.
- Inventory levels, stock movement history, low-stock visibility, and stock adjustments.
- Customer and supplier ledgers; receipts, supplier payments, expenses, and cash/bank activity.
- Administrator accounts, role/session controls, activity history, backup/restore, and an in-app first-use tour.
- Local-first operation; an internet connection is not required for normal daily transactions.

## Requirements

- Windows 10 version 21H2 or Windows 11, 64-bit. See [Microsoft's .NET on Windows support matrix](https://learn.microsoft.com/dotnet/core/install/windows) for supported OS editions and servicing status.
- For development: .NET 8 SDK.
- The published self-contained executable does not require a separate .NET installation.

## Run from source

From the repository root in PowerShell:

```powershell
dotnet restore SOFT2027.sln
dotnet run --project src/SOFT2027.UI/SOFT2027.UI.csproj
```

On first launch, create the primary administrator account in the setup screen. There is no default username or password.

The source release does not include a business database or sample customer/product records. The application creates an empty local database on first launch. For a disposable fresh-data startup check after building, run `qa-artifacts\Run-SOFT2027-QA-Fresh.cmd`; this keeps QA data separate from the normal `%LOCALAPPDATA%\SOFT2027\soft2027.db` database.

## Build a Windows x64 portable package

```powershell
dotnet publish src/SOFT2027.UI/SOFT2027.UI.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:Version=1.0.0-rc.2 `
  -o publish/win-x64
```

Run `publish\win-x64-rc.2\SOFT2027.UI.exe`. The release candidate archive is `publish\SOFT2027-1.0.0-rc.2-win-x64-portable.zip`. From the repository root, `publish.ps1` builds the same folder and archive. The app is self-contained and does not require a separately installed .NET runtime.

Extract the portable folder to a stable path. Start Menu and optional desktop shortcuts can be created by running `Create-Shortcuts.ps1`, or by using Windows' **Create shortcut** action on the executable. Updates replace only the extracted application folder; application data is stored separately. This is a portable package, not an MSI installer.

## Data and backups

The application database is stored at `%LOCALAPPDATA%\SOFT2027\soft2027.db`, separate from the executable folder. Replacing or updating the executable therefore does not replace the database. Use the application's Backup screen regularly and store backups on a separate drive or approved network location. Test restore using a separate copy before relying on a backup for recovery.

## Documentation

- [User guide](docs/USER_GUIDE.md): first run and common workflows.
- [PRS coverage and known gaps](docs/PRS_COMPLIANCE.md): implementation coverage review and remaining requirements.
- [Project update and deployment readiness report](docs/PROJECT_UPDATE_REPORT.md): current status, verification, and release gates.
- [Database schema](docs/DATABASE_SCHEMA.sql): SQLite schema reference.
- [Business rules](docs/BUSINESS_RULES.md): transaction and pricing rules.
- [Installation and handover](docs/INSTALLATION_AND_HANDOVER.md): portable setup, updates, uninstall, recovery, and release checklist.
- [Final test report](docs/FINAL_TEST_REPORT.md): automated results and outstanding manual acceptance scenarios.
- [Changes so far](docs/CHANGES_SO_FAR.md): files changed during the UI and QA work.

## Verification

The Windows x64 Release build was verified with .NET SDK 8.0.425. A successful build does not replace end-to-end verification of sales, purchases, returns, reports, backup, and restore against a test database before production use.

## Support and contributions

Please include the application version, Windows version, the steps that led to an issue, and a sanitized error message when reporting a problem. Do not publish business databases, backups, credentials, or customer data in issues.
