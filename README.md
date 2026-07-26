# Developer Portfolio Platform

A full-stack platform where developers log in with GitHub, manage their
project list, and get a shareable public profile page.

## What it does

- Log in via GitHub OAuth — no separate password
- Add, edit, and delete your own projects (title, description, repo/live
  links, tech stack)
- Get a public profile at `/u/{username}` anyone can view without logging in

## Honest note on testing — this one is different from the other three

Unlike the previous three projects, almost nothing here could be verified
directly, for two real environment limitations:

- **NuGet is blocked** in the sandbox this was built in (confirmed: 403
  Forbidden from api.nuget.org) — same restriction Maven Central hit
  earlier, so the C# backend has never actually been compiled.
- **SQL Server itself isn't installable** here either — no package
  available via apt, and Microsoft's own package repository is also
  blocked (403).

So the backend and database were **written and carefully reviewed, not
tested.** One thing worth flagging specifically: I used claim names like
`urn:github:avatarurl` in the auth code based on memory of how the GitHub
OAuth package works by default, wasn't fully confident that was exactly
right, and fixed it by explicitly mapping the claims myself in `Program.cs`
using GitHub's own documented API field names (`id`, `login`, `avatar_url`,
`name`) instead of relying on the package's internal defaults — that's a
more verifiable approach regardless of environment, but it's still unbuilt
code.

**What was actually tested:** the React frontend builds clean here (Node
works fine in this sandbox), but it's never been run against a live
backend, so the actual login → dashboard → public profile flow hasn't been
verified end-to-end.

**Recommended next step:** open this in VS Code with Claude Code once you
have real internet access to NuGet — `dotnet build` will surface any real
compile errors immediately, and Claude Code can walk through fixing them
with you far faster than debugging blind.

## Stack

- **Backend:** C#, ASP.NET Core 8 Web API, Entity Framework Core, SQL Server
- **Auth:** GitHub OAuth 2.0 + JWT for API calls
- **Frontend:** React (Vite)
- **Deployment:** AWS EC2, GitHub Actions CI/CD

## Setup

**Database:** Run `backend/schema.sql` against a SQL Server instance, or
let EF Core create it via migrations:
```bash
cd backend/DevPortfolio
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

**GitHub OAuth app:** Register one at
github.com/settings/developers → set the callback URL to
`http://localhost:5000/api/auth/github/callback` for local dev.

**Backend:**
```bash
cd backend/DevPortfolio
# Fill in appsettings.json: connection string, JWT secret, GitHub OAuth credentials
dotnet run
```

**Frontend** (separate terminal):
```bash
cd frontend
npm install
npm run dev
```

## Deployment

`.github/workflows/deploy.yml` builds both projects and deploys to EC2 via
SSH on every push to `main`. Requires these GitHub repo secrets:
`EC2_HOST`, `EC2_USER`, `EC2_SSH_KEY`.

Uses a symlink-swap + service restart pattern to minimize downtime during
deploys. Note this is a single-instance setup — true zero-downtime (no
dropped requests at all) would need a load balancer across 2+ instances;
this minimizes the restart window but isn't a strict zero-downtime
guarantee on its own.
