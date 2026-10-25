# HelpdeskOps

[![CI](https://github.com/kerembolek/HelpdeskOps/actions/workflows/ci.yml/badge.svg)](https://github.com/kerembolek/HelpdeskOps/actions/workflows/ci.yml)

[Türkçe sürüm](README.tr.md)

An enterprise-style IT Inventory and Helpdesk Ticketing System, built with ASP.NET Core MVC (.NET 10).

This project simulates a real-world internal IT operations platform: tracking hardware inventory, managing support tickets, and providing role-based dashboards for employees, IT specialists, and managers.

> Built as a portfolio project, inspired by hands-on experience in a corporate IT department during an internship.

**Live demo:** [helpdeskops.runasp.net](https://helpdeskops.runasp.net), sign in with one of the [demo accounts](#demo-accounts).

---

## Tech Stack

- **Framework:** ASP.NET Core MVC (.NET 10)
- **Database:** Entity Framework Core (Code-First)
- **Authentication:** ASP.NET Core Identity with role-based authorization (`Employee`, `ITAdmin`)
- **Frontend:** Razor Views, Bootstrap, Bootstrap Icons
- **Testing:** xUnit unit tests, run by GitHub Actions on every push and pull request
- **Version Control:** Git & GitHub

---

## Features

### Hardware Inventory & Network Management
- [x] Core device model (hostname, IPv4, serial number, brand/model, device type, status)
- [x] IPv4 format validation
- [x] Device management screens (list, create, edit, details, delete)
- [x] Device assignment to users (zimmet) from the device create/edit screens
- [x] Live network status indicator (ping-based, green/red dot) on the device list and details, with a refresh button
- [x] Remote access shortcut: a downloadable `.rdp` file for laptops and desktops that opens Remote Desktop Connection
- [x] Cisco switch port mapping: connect a device to a switch port (e.g. `Gi1/0/12`) and VLAN, one device per port, a port table on each switch's details page, and port changes recorded in the device history
- [x] Device lifecycle history: format, upgrade, part replacement, and repair entries, plus automatic assignment and status changes
- [x] Warranty tracking: purchase date, warranty end with active / expiring soon (30 days) / expired badges, service/vendor contact (name, phone, email), and warranty filters on the device list
- [x] CSV device import and export: add up to 500 devices from a CSV file (all or nothing, with every problem listed by line), download a sample template, and export the device list in the same format

### Helpdesk & Ticket System
- [x] Core ticket model (category, priority, status, requester, assignee)
- [x] Ticket management screens (list, create, edit, details, delete)
- [x] Automatic resolution date when a ticket is resolved or closed
- [x] Requester set automatically from the signed-in user
- [x] Ticket assignment to IT staff from the edit screen
- [x] IT staff can open tickets on behalf of an employee (phone and walk-in requests)
- [x] One-click "Assign to me" for unassigned active tickets
- [x] Ticket list search and filters (status, priority, active, resolved/closed, assigned to me, unassigned)
- [x] SLA targets per priority (Critical 4, High 8, Medium 24, Low 72 hours) with on track / due soon / overdue / met / missed badges on the ticket list and details, and an "overdue" ticket view
- [x] Ticket comments between the requester and IT staff
- [x] Ticket attachments: screenshots, PDF, text/log, Word, and Excel files (up to 5 files of 5 MB) when opening a ticket or later, stored outside the web root and downloadable only by people who can see the ticket
- [x] Satisfaction ratings: the requester rates a resolved ticket from 1 to 5 with an optional comment, once; the rating appears on the ticket and in its history
- [x] In-app notifications: requesters hear about comments, status changes, and tickets opened for them; IT staff hear about assignments, requester comments, and ratings; a navbar link shows the unread count
- [x] Ticket history: creation and every status, priority, and assignee change with who made it and when
- [x] Turkish user interface
- [x] Tier 0 self-service solution bank: employees pick a topic and try step-by-step solutions before opening a ticket; "not solved" opens a pre-filled ticket form, and IT staff manage the articles and see each one's self-service rate
- [x] Tiered category structure: each category has its own subcategories, and "Other" asks for a short free-text description; the ticket list can be filtered by category
- [x] Digital zimmet (asset assignment) forms: assigning a device opens a form that keeps a copy of the device and employee details, the employee confirms receipt in "My Devices", reassigning or deleting the device closes it, and each form downloads as a PDF
- [x] Signed handover and return: the employee draws their signature to confirm receipt and to request a return (with a reason such as leaving or a replacement), IT checks the returned device, records its condition, and signs to check it in, which unassigns the device and logs it in the device history; the PDF shows every signature and the return details

### Role-Based Access Control (RBAC)
- [x] Core user model with department field
- [x] Login and registration (name and department captured at sign-up)
- [x] Employee role: create tickets and see only their own tickets (assigned by default on registration)
- [x] ITAdmin role: full access to devices and ticket management (edit, assign, delete)
- [x] "My Devices" page: every user sees the devices assigned to them; employees can only attach their own devices to tickets
- [x] Manager role: read-only access to every ticket with its history and comments, plus the dashboard and reports; no device management or ticket changes
- [x] User management for IT admins: a searchable user list with roles, assigned devices, and active tickets, and a role change per user (nobody can change their own role; a former IT admin's active tickets go back to the unassigned pool)

### Quality
- [x] xUnit test project covering SLA rules, Turkey time, CSV import, signature validation, and the read-only demo middleware
- [x] GitHub Actions CI that builds the solution and runs the tests on every push to main and every pull request

### Interface
- [x] Navy navbar with icons, the current section highlighted, and a user menu
- [x] Role-based home page: shortcut tiles with live counts (my tickets, my devices, forms to sign; for IT: assigned to me, unassigned, past target time, pending returns, expiring warranties) and, on the demo site, the demo accounts for visitors
- [x] Color-coded ticket status and priority badges, dashboard cards with icons, and tables that scroll sideways on phones

### Admin Dashboard & Reporting
- [x] Ticket summary cards (open, in progress, resolved/closed, unassigned active tickets) that open the matching filtered ticket list
- [x] Average ticket resolution time, overall and by priority
- [x] Chronic problem devices (most frequent failures by brand/model)
- [x] Department workload distribution
- [x] Warranty cards (expiring within 30 days, expired) that open the matching filtered device list
- [x] SLA cards: overdue active tickets (opens the filtered list) and the share of resolved tickets that met their target
- [x] Reports page for a chosen period (7, 30, 90 days, 1 year): opened/resolved counts, tickets by category and subcategory, IT staff performance, average satisfaction (overall and per IT staff member), and a CSV export of the period's tickets

---

## Roadmap: Toward a Sellable Product

The original roadmap is complete. The next phases target what a company asks before buying, and what sets HelpdeskOps apart for Turkish SMBs (20-200 employees) that want a Turkish interface, zimmet and KVKK-ready records, and a self-hosted install.

### Phase 1: Must-haves for a company
- [x] E-mail: every in-app notification is also e-mailed, password reset and e-mail confirmation work, all in Turkish (SMTP, or `.eml` files locally)
- [x] Account security: 15-minute lockout after 5 wrong passwords (IT can unlock from the user list), two-factor sign-in with an authenticator app (QR code and recovery codes), passwords of at least 10 characters, and a Turkish "Hesabım" page
- [ ] Audit log: who changed what and when, across devices, tickets, zimmet forms, and roles
- [ ] KVKK: privacy notice, retention periods, and export or deletion of a person's data
- [ ] Paging and faster lists for thousands of devices and tickets
- [ ] Self-hosted install guide (IIS and Docker) with a backup plan

### Phase 2: Differentiators
- [ ] QR labels: scanning a device's sticker opens a ticket for it (employee) or its device card (IT); printable label sheets
- [ ] Sign-in with Active Directory / Microsoft Entra ID and user sync
- [ ] Automatic inventory: a small PowerShell agent reports serial number, RAM, disk, and Windows version
- [ ] Onboarding and offboarding checklists that tie accounts, devices, and signed returns together
- [ ] Business-hours SLA with Turkish public holidays
- [ ] Software license and consumables (toner, cartridges) tracking

### Phase 3: Extras
- [ ] Open tickets by e-mail
- [ ] Microsoft Teams notifications
- [ ] AI suggestions for category, priority, and matching self-service articles

## Project Status

**Actively in development.** Core data models (`Device`, `Ticket`, `ApplicationUser`) with relationships and validation are complete, and device and ticket management screens are working with a Turkish user interface. Users sign in with ASP.NET Core Identity. Employees first try the self-service solution bank, then create and track their own tickets, while IT admins manage devices, assign them to users, keep a lifecycle history for each device, track warranties and service contacts, see whether each device answers ping, open an RDP connection to computers, map devices to Cisco switch ports, open tickets on behalf of employees, take tickets with one click, and assign, edit, and close them. Tickets are filed under a category and subcategory (with a free-text "Other"), and the ticket list can be searched and filtered. Requesters and IT staff talk through ticket comments, and every ticket keeps a history of its status, priority, and assignee changes. IT admins also have a dashboard with resolution times, chronic problem devices, department workload, and expiring warranties, and a reports page with period-based ticket figures, IT staff performance, and a CSV export. Managers can read every ticket and see the dashboard and reports without changing anything.

Assigning a device opens a digital zimmet form that the employee signs in "My Devices". Employees request returns with a signature, and IT checks the device and signs to receive it. Every form, with its signatures, can be downloaded as a PDF. IT admins manage user roles from the user list. Every item on the current roadmap is implemented.

---

## Documentation

- [User Guide (Turkish)](docs/USER_GUIDE_TR.md): walkthrough of the current UI with screenshots.

---

## Getting Started

Prerequisites: the .NET 10 SDK and the EF Core command-line tools (`dotnet tool install --global dotnet-ef`).

```bash
git clone https://github.com/kerembolek/HelpdeskOps.git
cd HelpdeskOps
dotnet restore
dotnet ef database update --project HelpdeskOps
dotnet run --project HelpdeskOps
```

Run the unit tests with `dotnet test` from the repository root.

The default connection string uses SQL Server LocalDB (`(localdb)\mssqllocaldb`). Update `appsettings.json` if you use a different SQL Server instance.

Pending migrations are also applied automatically when the app starts. Dates are shown in Turkey time (`Europe/Istanbul`) whatever time zone the server uses.

### Deployment

The live demo runs on MonsterASP.NET and is published from Visual Studio with Web Deploy. Its settings live in an `appsettings.Production.json` file next to `appsettings.json`, which is git-ignored because it holds the hosted database connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "SeedDemoData": true,
  "DemoReadOnly": true
}
```

`SeedDemoData` turns on the demo data below outside the `Development` environment. `DemoReadOnly` makes the public demo read-only: every form post except sign-in and sign-out is refused with a notice, registration and account management pages are closed, opening a notification does not mark it read, and a banner tells visitors that changes are not saved. The publish profile excludes `App_Data`, so local attachments and keys never reach the server.

### E-mail

The `Email` settings section picks how mail goes out. `Mode` is `None` (default, nothing is sent; the live demo runs like this), `Pickup` (each mail is written as an `.eml` file to `App_Data/mail-outbox`, which `appsettings.Development.json` turns on), or `Smtp`. For real delivery, put the SMTP settings in the git-ignored `appsettings.Production.json` or in user secrets, never in `appsettings.json`:

```json
{
  "Email": {
    "Mode": "Smtp",
    "Host": "smtp.example.com",
    "Port": 587,
    "UseStartTls": true,
    "UserName": "...",
    "Password": "...",
    "FromAddress": "helpdesk@example.com",
    "FromName": "HelpdeskOps"
  }
}
```

Mail is queued and sent in the background, so a slow or failing mail server never slows down a page. Every in-app notification is also mailed to its recipient. With `Smtp`, new accounts must confirm their e-mail address before signing in, and the sign-in page offers password reset.

### Demo Accounts

When the app runs in the `Development` environment, it creates these accounts, a few sample devices assigned to them with maintenance history and a mix of active, expiring, and expired warranties, a Cisco switch with those devices patched into its ports, signed zimmet forms for the assigned devices (one still awaiting a signature, one with a return request for IT to check in), self-service solution articles, and sample tickets with comments and history for the dashboard automatically. Other environments get them only when `SeedDemoData` is `true`:

| Role     | Email                  | Password        |
|----------|------------------------|-----------------|
| Employee | `test@helpdeskops.com` | `Helpdesk2026!` |
| ITAdmin  | `it@helpdeskops.com`   | `Helpdesk2026!` |
| Manager  | `mudur@helpdeskops.com` | `Helpdesk2026!` |

The demo devices use made-up `10.10.x.x` addresses, so their network status dots show red unless such hosts exist on your network.

Two more employee accounts (`ik@helpdeskops.com`, `satis@helpdeskops.com`, same password) are created so the dashboard can compare departments.

New users who register through the sign-up page get the `Employee` role.

---

## Author

**Kerem Bölek**
Computer Programming graduate | IT Hardware & Support background
[GitHub](https://github.com/kerembolek)