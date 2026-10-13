# HelpdeskOps

[Türkçe sürüm](README.tr.md)

An enterprise-style IT Inventory and Helpdesk Ticketing System, built with ASP.NET Core MVC (.NET 10).

This project simulates a real-world internal IT operations platform: tracking hardware inventory, managing support tickets, and providing role-based dashboards for employees, IT specialists, and managers.

> Built as a portfolio project, inspired by hands-on experience in a corporate IT department during an internship.

---

## Tech Stack

- **Framework:** ASP.NET Core MVC (.NET 10)
- **Database:** Entity Framework Core (Code-First)
- **Authentication:** ASP.NET Core Identity (user model in place; login and role-based access planned)
- **Frontend:** Razor Views, Bootstrap
- **Version Control:** Git & GitHub

---

## Features

### Hardware Inventory & Network Management
- [x] Core device model (hostname, IPv4, serial number, brand/model, device type, status)
- [x] IPv4 format validation
- [x] Device management screens (list, create, edit, details, delete)
- [ ] Live network status indicator (ping-based, green/red dot)
- [ ] Remote access shortcut (RDP launch link)
- [ ] Cisco switch port mapping
- [ ] Device lifecycle / audit trail (timeline of format, upgrades, part replacements)
- [ ] Warranty tracking & service/vendor contact integration

### Helpdesk & Ticket System
- [x] Core ticket model (category, priority, status, requester, assignee)
- [x] Ticket management screens (list, create, edit, details, delete)
- [x] Automatic resolution date when a ticket is resolved or closed
- [x] Turkish user interface
- [ ] Tier 0 self-service solution bank (guided troubleshooting before ticket creation)
- [ ] Tiered category structure with free-text "Other" option
- [ ] Digital asset assignment forms with auto-generated PDF records

### Role-Based Access Control (RBAC)
- [x] Core user model with department field
- [ ] Employee role: view only assigned devices, create tickets
- [ ] IT Specialist role: full access to devices, network info, and ticket management
- [ ] Manager role: ticket history and administrative reports

### Admin Dashboard & Reporting
- [ ] Average ticket resolution time
- [ ] Chronic problem devices (most frequent failures by brand/model)
- [ ] Department workload distribution

---

## Project Status

**Actively in development.** Core data models (`Device`, `Ticket`, `ApplicationUser`) with relationships and validation are complete, the database is configured with EF Core Identity, and the initial migration has been applied. Device and ticket management screens are working with a Turkish user interface.

Not implemented yet: user login and role-based access (tickets are currently created under a temporary development user), ticket assignment from the interface, dashboards, and reports.

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

---

## Author

**Kerem Bölek**
Computer Programming graduate | IT Hardware & Support background
[GitHub](https://github.com/kerembolek)