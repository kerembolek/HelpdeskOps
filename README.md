# HelpdeskOps

[Türkçe sürüm](README.tr.md)

An enterprise-style IT Inventory and Helpdesk Ticketing System, built with ASP.NET Core MVC (.NET 10).

This project simulates a real-world internal IT operations platform: tracking hardware inventory, managing support tickets, and providing role-based dashboards for employees, IT specialists, and managers.

> Built as a portfolio project, inspired by hands-on experience in a corporate IT department during an internship.

---

## Tech Stack

- **Framework:** ASP.NET Core MVC (.NET 10)
- **Database:** Entity Framework Core (Code-First)
- **Authentication:** ASP.NET Core Identity with role-based authorization (`Employee`, `ITAdmin`)
- **Frontend:** Razor Views, Bootstrap
- **Version Control:** Git & GitHub

---

## Features

### Hardware Inventory & Network Management
- [x] Core device model (hostname, IPv4, serial number, brand/model, device type, status)
- [x] IPv4 format validation
- [x] Device management screens (list, create, edit, details, delete)
- [x] Device assignment to users (zimmet) from the device create/edit screens
- [ ] Live network status indicator (ping-based, green/red dot)
- [ ] Remote access shortcut (RDP launch link)
- [ ] Cisco switch port mapping
- [ ] Device lifecycle / audit trail (timeline of format, upgrades, part replacements)
- [ ] Warranty tracking & service/vendor contact integration

### Helpdesk & Ticket System
- [x] Core ticket model (category, priority, status, requester, assignee)
- [x] Ticket management screens (list, create, edit, details, delete)
- [x] Automatic resolution date when a ticket is resolved or closed
- [x] Requester set automatically from the signed-in user
- [x] Ticket assignment to IT staff from the edit screen
- [x] Turkish user interface
- [ ] Tier 0 self-service solution bank (guided troubleshooting before ticket creation)
- [ ] Tiered category structure with free-text "Other" option
- [ ] Digital asset assignment forms with auto-generated PDF records

### Role-Based Access Control (RBAC)
- [x] Core user model with department field
- [x] Login and registration (name and department captured at sign-up)
- [x] Employee role: create tickets and see only their own tickets (assigned by default on registration)
- [x] ITAdmin role: full access to devices and ticket management (edit, assign, delete)
- [x] "My Devices" page: every user sees the devices assigned to them; employees can only attach their own devices to tickets
- [ ] Manager role: ticket history and administrative reports

### Admin Dashboard & Reporting
- [x] Ticket summary cards (open, in progress, resolved/closed, unassigned active tickets)
- [x] Average ticket resolution time, overall and by priority
- [x] Chronic problem devices (most frequent failures by brand/model)
- [x] Department workload distribution

---

## Project Status

**Actively in development.** Core data models (`Device`, `Ticket`, `ApplicationUser`) with relationships and validation are complete, and device and ticket management screens are working with a Turkish user interface. Users sign in with ASP.NET Core Identity. Employees create and track their own tickets, while IT admins manage devices, assign them to users, and assign, edit, and close tickets. IT admins also have a dashboard with resolution times, chronic problem devices, and department workload.

Not implemented yet: the manager role and its reports.

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
dotnet ef database update
dotnet run
```

The default connection string uses SQL Server LocalDB (`(localdb)\mssqllocaldb`). Update `appsettings.json` if you use a different SQL Server instance.

### Demo Accounts

When the app runs in the `Development` environment, it creates these accounts, a few sample devices assigned to them, and sample tickets for the dashboard automatically (never in other environments):

| Role     | Email                  | Password        |
|----------|------------------------|-----------------|
| Employee | `test@helpdeskops.com` | `Helpdesk2026!` |
| ITAdmin  | `it@helpdeskops.com`   | `Helpdesk2026!` |

Two more employee accounts (`ik@helpdeskops.com`, `satis@helpdeskops.com`, same password) are created so the dashboard can compare departments.

New users who register through the sign-up page get the `Employee` role.

---

## Author

**Kerem Bölek**
Computer Programming graduate | IT Hardware & Support background
[GitHub](https://github.com/kerembolek)