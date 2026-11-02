# Changelog

## 0.9.0 - 2026-11-01

First numbered release, ahead of the first pilot install.

### Check before updating
- The sign-up page is now closed by default. To let employees sign up, set `Registration:AllowedDomains` to the company's e-mail domains (`REGISTRATION_DOMAIN` in `.env`). The `InitialAdminEmail` address can still register, and installs with a company directory are not affected.

### Security
- Sign-up accepts only the configured company domains, checked on the server.
- Thirty posts a minute per address across the sign-in, sign-up, two-factor, and password pages.
- Content-Security-Policy with per-request script nonces, plus X-Frame-Options, X-Content-Type-Options, Referrer-Policy, and Permissions-Policy.
- Form validation scripts on the account pages load from this site instead of a CDN.

### Added
- `/health` for monitoring tools, which checks the database and shares no details.
- Tests on the running app for role access, ticket ownership, security headers, and the sign-in limit.
- The version number in the footer for signed-in users.

### Changed
- New versions use a source-available license; see [LICENSE](LICENSE).
