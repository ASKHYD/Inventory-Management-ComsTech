# Installation and Handover

**Package:** SOFT2027 — Ali Traders FMCG Wholesale & Distribution  
**Candidate version:** 1.0.0-rc.2  
**Delivery:** portable Windows x64 folder/ZIP; no installer was requested.  
**Production status:** candidate only; see [Final Test Report](FINAL_TEST_REPORT.md).

## Requirements

- 64-bit Windows 10 version 21H2 or Windows 11 on a supported edition. Check Microsoft's current [.NET on Windows support matrix](https://learn.microsoft.com/dotnet/core/install/windows) and Windows lifecycle before deployment.
- A printer is needed for paper output; the app's normal database and reporting workflows are local.
- No separate .NET runtime is required for the self-contained package.
- The package has no customer-approved application icon yet; the executable uses the default Windows icon until Ali Traders supplies or approves one.

## Install the portable package

1. Extract `SOFT2027-1.0.0-rc.2-win-x64-portable.zip` to a stable location, for example `%LOCALAPPDATA%\Programs\SOFT2027`.
2. Run `SOFT2027.UI.exe`.
3. On first launch, create the primary administrator account with a password of at least 10 characters, then sign in.
4. Optional Start Menu shortcut: run `Create-Shortcuts.ps1` from the extracted folder. Add `-DesktopShortcut` for a desktop shortcut. If local PowerShell policy blocks unsigned scripts, create a shortcut through File Explorer instead; do not weaken the computer's execution policy for this package.
5. Verify the sign-in screen, database creation, and the approved acceptance scenarios on a disposable installation before entering business data.

## Application data and backups

- Default database: `%LOCALAPPDATA%\SOFT2027\soft2027.db`.
- Restore safety copies: `%LOCALAPPDATA%\SOFT2027\Backups\before-restore-*.db`.
- The database is outside the portable app folder. Copy backups to a separate storage device or approved location. The in-app Backup and Restore screen asks for confirmation before restore and checks the selected SQLite backup before replacing live data.
- Test a restore to a disposable database first. Never use the customer's only live database for acceptance testing.
- For a local isolated test instance, set `SOFT2027_DATABASE_PATH` to a full `.db` file path before starting the app.

## Update and uninstall

- Before updating, close SOFT2027 and create a verified backup.
- Extract the newer portable build and replace only the application folder. Keep the `%LOCALAPPDATA%\SOFT2027` data folder intact. Startup applies idempotent schema initialization/migrations.
- To uninstall, close the application, remove its shortcut (optionally run `Remove-Shortcuts.ps1`), then remove only the extracted application folder. Do not remove `%LOCALAPPDATA%\SOFT2027` unless the owner explicitly chooses data removal after a verified separate backup.
- The portable package does not register a Windows installer or automatic uninstaller. No MSI installer is included.

## Build and publish

Use PowerShell from the repository root. `publish.ps1` resolves the SDK in the repository or its parent and creates `publish\win-x64-rc.2` plus the portable ZIP. Manual equivalent:

```powershell
dotnet publish src/SOFT2027.UI/SOFT2027.UI.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:Version=1.0.0-rc.2 `
  -o publish/win-x64-rc.2
```

Build a new candidate version only after updating the project version, release notes, test report, and package name. The x64 .NET runtime pack and NuGet dependencies must be available to restore; do not bypass local security policy to run scripts.

## Release handover checklist

- [ ] Ali Traders approves the application icon and displayed business name.
- [ ] Run install, first-run setup, update, persistence, and uninstall on a clean supported Windows x64 machine.
- [ ] Complete all pending manual scenarios in the final test report; record tester, machine/OS, date, actual result, and defects.
- [ ] Verify printer output and the final cash-memo/bill layouts on target hardware.
- [ ] Verify restore against a disposable copy of the previous-version database.
- [ ] Customer approves reports, bank/advance workflow, pricing, returns, and production readiness.
- [ ] Store the released ZIP, its SHA-256 hash, approved release notes, and a separate verified data backup in the approved handover location.
