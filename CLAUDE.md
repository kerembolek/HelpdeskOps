# HelpdeskOps – Working Notes for Claude

IT inventory and helpdesk ticketing system: ASP.NET Core MVC (.NET 10), EF Core Code-First on SQL Server LocalDB, ASP.NET Core Identity with `Employee` and `ITAdmin` roles, Razor views with Bootstrap. Solo portfolio project by Kerem Bölek; see `README.md` for the feature roadmap.

## Communication
- Reply to the user in Turkish. They are early in their career: explain Git/GitHub/Visual Studio steps briefly when they come up.
- Code, identifiers, comments, and commit messages are in English. All UI text is in Turkish.

## Git workflow
- Big or important changes (new features, auth/roles, migrations, multi-commit work) go on their own branch (`feature/...`, `fix/...`, `docs/...`), then a GitHub PR, then a merge into `master`.
- Small fixes, typos, and purely visual tweaks are committed straight to `master`. When unsure, use a branch.
- The user pushes, opens PRs, and merges from Visual Studio and GitHub themselves. Don't push. Ask before committing.
- `git` may not be on PATH. Visual Studio's bundled Git lives at:
  `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe`

## Commit messages
- An imperative English subject line, then a bulleted body covering every meaningful change, grouped under short headings when the change is large.
- Call out security or data-integrity fixes explicitly in the body.
- Never propose a one-line message for a multi-file change.

## Code conventions
- Controllers: role checks use `[Authorize(Roles = "ITAdmin")]`; ownership checks compare against `User.FindFirstValue(ClaimTypes.NameIdentifier)`.
- Edit actions load the stored entity and copy form fields onto it. Never `_context.Update()` a form-bound object, and never bind server-owned fields such as `CreatedAt`, `RequesterId`, or timestamps.
- Validate user-supplied foreign keys (assigned user, device) on the server, not only through the dropdowns.
- Store timestamps in UTC (`DateTime.UtcNow`) and show them with `.ToLocalTime().ToString("dd.MM.yyyy HH:mm")`.
- List views: `table table-hover`, plus an `alert alert-info` message when the list is empty.
- Demo data lives in `Data/DataDemoSeeder.cs`, runs only in Development, and is idempotent (match on email or serial number).

## Build and verify
- Build: `dotnet build` in `HelpdeskOps/`. Run: `dotnet run --launch-profile http` (http://localhost:5161).
- Before calling a feature done, build it and check it in the browser with both demo roles (accounts are listed in `README.md`).

## Docs
- Update `README.md` and `README.tr.md` in the same branch as any feature that changes behavior (roadmap checkboxes, project status).
- `docs/USER_GUIDE_TR.md` has screenshots. Update it when the UI changes visibly; batching at milestones is fine.

## Open items
- `docs/USER_GUIDE_TR.md` doesn't cover login, roles, ticket assignment, My Devices, or the Dashboard yet.
