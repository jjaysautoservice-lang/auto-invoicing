# Auto Invoicing

A Windows desktop app for an automotive repair shop: job tickets, invoices, customer and vehicle
history, parts inventory, and daily / weekly / monthly reports. Data lives in a single SQLite file.

Built with C# / .NET 8 WinForms.

## What it does

| Area | Features |
| --- | --- |
| **Job tickets** | Work orders with concern / cause / correction, technician, promised date, planned labor and parts. Prints a shop copy with checkboxes and sign-off lines. One click turns a ticket into an invoice. |
| **Invoices** | Labor, inventory parts, non-stock parts, and fees / sublet lines. Per-line taxable flag, shop tax rate, payments (cash, card, check...), balance due. Drafts print as an **Estimate**. **Finalize** locks the invoice and deducts its parts from inventory; **Void** puts them back. |
| **Customers & history** | Customers with multiple vehicles (VIN, plate, engine). Search by name, phone, plate or VIN. Service history per customer or per vehicle with mileage and work performed, total spent and amount owed. Printable. |
| **Inventory** | Parts with cost, price, bin location, reorder level. Receive stock, remove stock, or enter a physical count; every change is logged. Low-stock highlighting, reorder list, stock value at cost. |
| **Reports** | Daily, weekly (Mon to Sun) and monthly: labor / parts / fees / tax / total, parts cost and margin, gross profit, average ticket, money collected by payment method, by-day breakdown, top parts, invoice list. Printable. |

## Money and tax rules

- All money is `decimal` in code and stored as exact text in SQLite (never floating point).
- Each line is rounded to the cent (half away from zero), then lines are summed, so printed lines always add up to the subtotal.
- Tax = taxable lines x rate, rounded once to the cent.
- Labor is not taxed and fees are taxed by default; both are shop settings and every line can be overridden.
- Sales in reports come from **finalized** invoices dated in the period; **collected** comes from payments dated in the period.
- Finalizing lets stock go below zero (the part is already on the car) and warns you so you can recount; it also warns when a part reaches its reorder level.

## Running it

Requires Windows and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet run --project src/AutoInvoicing.App
```

The database is created at `%LOCALAPPDATA%\AutoInvoicing\shop.db`. To use a different file (for
example on a shared drive), pass its path: `AutoInvoicing.exe D:\Shop\shop.db`.
**File > Back Up Database** saves a copy while the app is running.

On first run, open **File > Shop Settings** and enter your shop name, address, phone, tax rate and
default labor rate; these appear on every printed invoice.

To publish a standalone build:

```
dotnet publish src/AutoInvoicing.App -c Release -r win-x64 --self-contained
```

## Tests

```
dotnet test
```

Covers invoice totals, tax rounding, payments and balance due, parts deductions on finalize and
restoration on void, stock logging, exact decimal storage, report period boundaries and report totals.

## Layout

```
src/AutoInvoicing.Core    models, InvoiceCalculator, SQLite repositories, invoice/report services
src/AutoInvoicing.App     WinForms screens and print layouts (Printing/Documents.cs)
tests/AutoInvoicing.Tests xUnit tests against the Core library with a throwaway SQLite file
```
