# Printavo Shipper

A Windows desktop app that turns a completed [Printavo](https://www.printavo.com/) order into a
printed UPS or FedEx shipping label, and writes the tracking number and cost back to Printavo.

- Opens Printavo orders and ships to the order's address (or any of its additional addresses).
- Rates and buys labels through the UPS and FedEx APIs, billing your own account or a customer's
  third-party account.
- Prints labels to a 4x6 label printer or plain letter paper, and prints numbered box labels for
  pick-up orders.
- Writes back to Printavo: tracking in the customer note, a task recording the shipping cost, an
  optional Shipping line on the invoice, and the order's status.
- Keeps a local shipment history with reprint, void, and retry-sync.

Everything specific to a shop — Printavo statuses and staff, delivery method names, box sizes,
ship-from address, carrier accounts — is configured on the app's Settings screen. See
[docs/StaffRunbook.pdf](docs/StaffRunbook.pdf) for setup and day-to-day use.

## Requirements

> **Bring your own carrier API keys.** The app doesn't include any UPS or FedEx credentials. Each
> shop needs its own free developer account with UPS (and with FedEx, if it ships FedEx), creates an
> app or project there to get its own Client ID and Client Secret, and links its own shipping account
> number to it. New keys start in the carriers' test environments (UPS *CIE*, FedEx *Sandbox*);
> moving to live shipping may require extra approval steps on the carrier's side.

- Windows 11 (x64). Windows 10 may work but is untested.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build. Running a published exe
  needs nothing installed.
- To actually ship:
  - a Printavo account with API access (login email + API token);
  - an app on the [UPS Developer Portal](https://developer.ups.com/) (Client ID/Secret) and your own
    UPS account number;
  - optionally, a project on the [FedEx Developer Portal](https://developer.fedex.com/) and your own
    FedEx account number.

## Build and run

```powershell
git clone https://github.com/BenExtreme/printavo-shipper.git
cd printavo-shipper
dotnet build -c Release
.\src\PrintavoShipper.App\bin\Release\net8.0-windows\PrintavoShipper.App.exe
```

Close any running copy of the app before rebuilding — Windows locks the exe while it's open, and the
build fails.

## Publish a standalone exe

```powershell
.\publish.ps1
```

This produces `publish\Printavo Shipper.exe` (~83 MB): a single self-contained file that runs on any
Windows 11 x64 PC with nothing else installed. Copy it to the target PC and run it.

## First run

On first launch the app creates its database and starts with empty settings. Open **Settings** and
fill in, at minimum:

1. **Printavo** — login email and API token, then *Test Printavo Connection*.
2. **UPS** (and/or **FedEx**) — API credentials, with the environment on *Cie* / *Sandbox* while
   testing; then at least one of your own accounts under **Our UPS Accounts** / **Our FedEx
   Accounts**. The carriers require your own account as the shipper even for third-party billing.
3. **Printavo Workflow** — which of your Printavo statuses to use, your delivery method names, and
   your staff.
4. **Default Ship-From Address** and **Label Printer**.

Use the carriers' test environments (UPS *Cie*, FedEx *Sandbox*) until everything works — Production
labels cost real money. Printavo has no sandbox, so test against an order you don't mind writing
tracking notes and tasks to.

## Labels and printers

| Label | Format received from the carrier | Saved for reprints as |
|---|---|---|
| UPS shipping label | GIF image | `.gif` |
| FedEx shipping label | PDF, 4x6 | `.pdf` |
| Pick-up box label | Drawn by the app | Not saved (reprint from the order) |

Labels print through the regular Windows printer driver as an image — at 4x6 on a label printer, or
at true size centered on 8.5x11 paper on an office printer.

**The app does not produce or send ZPL or EPL.** Any printer with a Windows driver works, including
thermal label printers that install one. A printer that only accepts raw ZPL/EPL commands, with no
Windows driver, isn't supported.

## Where data lives

Everything is stored per Windows user under `%LocalAppData%\PrintavoShipper\`:

- `printavoshipper.db` — SQLite database: settings, contractors, shipment history. API secrets are
  encrypted with Windows DPAPI, so they only decrypt for the same Windows user on the same PC.
- `Labels\` — saved label files (`.gif` for UPS, `.pdf` for FedEx), for reprints.
- `Archive\` — older shipments exported from history.
- `logs\` — daily log files.

The database schema is updated automatically on startup, so a new build can be run over an existing
install.

## Project layout

| Project | What it contains |
|---|---|
| `src/PrintavoShipper.App` | WPF UI (MVVM with CommunityToolkit.Mvvm, styled with WPF-UI) |
| `src/PrintavoShipper.Core` | Data models, EF Core/SQLite database and migrations, label printing |
| `src/PrintavoShipper.Printavo` | Printavo GraphQL API client |
| `src/PrintavoShipper.Ups` | UPS REST API client |
| `src/PrintavoShipper.FedEx` | FedEx REST API client |
| `tests/PrintavoShipper.Tests` | Test project scaffold (no tests yet) |

## Database migrations

Migrations use the EF Core CLI, run against the Core project:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> --project src/PrintavoShipper.Core --startup-project src/PrintavoShipper.Core
```

You don't need to apply migrations by hand — the app applies any pending ones when it starts. Never
put shop-specific data (IDs, emails, addresses) in a migration: it ships inside the published exe.

## License

Printavo Shipper is free software, licensed under the
[GNU General Public License v3.0](LICENSE). You may use, modify, and redistribute it; if you
distribute a modified version, you must make its source code available under the same license.
