# Contributing

## Getting started

1. Install the .NET 10 SDK and SQL Server LocalDB.
2. Clone the repository and run `dotnet run --launch-profile http` in `HelpdeskOps/` (http://localhost:5161).
3. In Development the demo seeder creates sample data and the demo accounts listed in `README.md`.

## Before opening a pull request

- `dotnet build` in `HelpdeskOps/` finishes without errors.
- `dotnet test` passes from the repository root.
- The change works in the browser with both the `Employee` and `ITAdmin` demo accounts.

## Conventions

- Code, identifiers, comments and commit messages are in English; all UI text is in Turkish.
- Store timestamps in UTC and show them in Turkey time.
- Edit actions copy form fields onto the stored entity; never bind server-owned fields.
- Validate foreign keys chosen in forms on the server.
- Commit subjects are imperative ("Add ...", "Fix ..."), followed by a bulleted body.
