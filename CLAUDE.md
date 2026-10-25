# HelpdeskOps – Working Notes for Claude

IT inventory and helpdesk ticketing system: ASP.NET Core MVC (.NET 10), EF Core Code-First on SQL Server LocalDB, ASP.NET Core Identity with `Employee` and `ITAdmin` roles, Razor views with Bootstrap. Solo portfolio project by Kerem Bölek; see `README.md` for the feature roadmap.

## Communication
- Reply to the user in Turkish. They are early in their career: explain Git/GitHub/Visual Studio steps briefly when they come up.
- Code, identifiers, comments, and commit messages are in English. All UI text is in Turkish.

## Git workflow
- Big or important changes (new features, auth/roles, migrations, multi-commit work) go on their own branch (`feature/...`, `fix/...`, `docs/...`), then a GitHub PR, then a merge into `main`.
- Small fixes, typos, and purely visual tweaks are committed straight to `main`. When unsure, use a branch.
- Claude builds and checks a branch in the browser; only when it works, Claude pushes it, opens the PR, and merges it.
- PRs are merged with **Rebase and merge** (linear history, no merge commits).
- The user sets a daily cap on GitHub contributions. Every commit and every opened PR counts; commits count on their author date. Plan the list up front and ask before anything that could go over.
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
- Build: `dotnet build` in `HelpdeskOps/`. Tests: `dotnet test` from the repository root (xUnit project `HelpdeskOps.Tests`, also run by GitHub Actions CI). Run: `dotnet run --launch-profile http` (http://localhost:5161).
- Before calling a feature done, build it and check it in the browser with both demo roles (accounts are listed in `README.md`).

## Docs
- Update `README.md` and `README.tr.md` in the same branch as any feature that changes behavior (roadmap checkboxes, project status).
- `docs/USER_GUIDE_TR.md` has screenshots. Update it when the UI changes visibly; batching at milestones is fine.

## Open items
- None. `docs/USER_GUIDE_TR.md` covers every screen as of 2026-10-23; screenshots are 800px wide PNGs in `docs/images`.
