# Printavo Shipper: UPS and FedEx Shipping Label Integration for Printavo

**Printavo Shipper** is a free, open-source Windows app that connects [Printavo](https://www.printavo.com/)
to **UPS** and **FedEx**. Open a Printavo order, compare carrier rates, buy and print the shipping
label, and the tracking number and shipping cost are written back to the Printavo order on their own.
It was built at a screen-printing shop for print and apparel shops that use Printavo and ship orders
on their own UPS or FedEx accounts.

**[Download the latest release (Windows .exe)](https://github.com/BenExtreme/printavo-shipper/releases/latest)**:
one file, no installer, nothing else to install.

> Printavo Shipper is an independent project. It is not made, endorsed, or supported by Printavo,
> UPS, or FedEx. It uses their public APIs with your own account credentials.

## Features

- **Printavo integration**: look up any Printavo order and ship to its address or to any of its
  additional addresses. Works with the Printavo GraphQL API.
- **UPS shipping labels**: rate and buy labels through the official UPS API. Services include UPS
  Ground, 3 Day Select, 2nd Day Air, Next Day Air, and Next Day Air Saver. Optional UPS email
  tracking notifications (Quantum View) go to your customer.
- **FedEx shipping labels**: rate and buy labels through the FedEx Ship API. Services include FedEx
  Ground, Home Delivery, Express Saver, 2Day, and Standard and Priority Overnight.
- **Address validation** through UPS and FedEx before you buy a label.
- **Multi-box shipments**, with box-size presets for your common carton sizes.
- **Third-party billing**: bill your own carrier account or the customer's UPS or FedEx account.
  Useful for contract printing, where each contract customer pays its own shipping.
- **Tracking written back to Printavo**: the tracking number goes in the customer note, the shipping
  cost is recorded as a task, an optional Shipping line (with markup) can be added to the invoice,
  and the order status is updated.
- **Label printing**: 4x6 thermal label printers (Zebra, Rollo, and others with a Windows driver) or
  plain letter paper on an office printer.
- **Pick-up box labels**: numbered box labels for local pick-up orders, with your shop logo or the
  contractor's logo.
- **Shipment history**: reprint, void, and retry the Printavo sync on past shipments.

Everything specific to a shop, such as Printavo statuses and staff, delivery method names, box sizes,
ship-from address, and carrier accounts, is set up on the app's Settings screen. See
[docs/StaffRunbook.pdf](docs/StaffRunbook.pdf) for setup and day-to-day use.

## FAQ

**Does Printavo integrate with UPS or FedEx?**
Printavo Shipper adds that link. It reads orders from your Printavo account, buys UPS or FedEx labels
on your own carrier account, and writes the tracking number back to the order. You don't need a
third-party shipping platform or a monthly subscription.

**Does it cost anything?**
The app is free and open source (GPL-3.0). You pay UPS or FedEx for the labels you buy, at your own
account's rates.

**Which printers does it support?**
Any printer with a Windows driver, including 4x6 thermal label printers. See
[Labels and printers](#labels-and-printers).

**Does it work on Mac?**
No. It's a Windows app (Windows 11, x64).

## Requirements

> **Bring your own carrier API keys.** The app doesn't include any UPS or FedEx credentials. Each
> shop needs its own free developer account with UPS (and with FedEx, if it ships FedEx), creates an
> app or project there to get its own Client ID and Client Secret, and links its own shipping account
> number to it. New keys start in the carriers' test environments (UPS *CIE*, FedEx *Sandbox*);
> moving to live shipping may require extra approval steps on the carrier's side.
>
> **FedEx label certification:** to get a FedEx developer project approved for live shipping, you
> submit scans of printed sample labels. This app produces FedEx labels as PDF images, and FedEx only
> accepts thermal-printed labels in ZPL or EPL format — so print the certification labels on a
> **laser printer** (Settings > Label Printer > Label paper: `Letter8x11Centered`), not a thermal printer.

- Windows 11 (x64). Windows 10 may work but is untested.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build. Running a published exe
  needs nothing installed.
- To actually ship:
  - a Printavo account with API access (login email + API token);
  - an app on the [UPS Developer Portal](https://developer.ups.com/) (Client ID/Secret) and your own
    UPS account number;
  - optionally, a project on the [FedEx Developer Portal](https://developer.fedex.com/) and your own
    FedEx account number.

## Build from source

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
