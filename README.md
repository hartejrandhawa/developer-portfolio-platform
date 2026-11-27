# Dev Portfolio

Sign in with GitHub, pick the repositories worth showing, and get one shareable
page at `/u/{username}` with your projects (in your order) and a 13-week
activity grid built from your public GitHub events.

## Features

- **GitHub login.** OAuth 2.0 with no separate password. The API then issues its own JWT.
- **Projects.** Add, edit, delete, and reorder them, or import public repos in one step
  (description, homepage, language, and topics are filled in for you).
- **Public profile.** No login needed to view. Shows your bio, the activity heatmap, your
  top repos, and your projects.
- **Safe by default.** Project links must be `http(s)` (blocking stored XSS via
  `javascript:` URLs), every edit is scoped to its owner, and inputs are length-checked.

## Stack

| Layer    | Tech                                                         |
| -------- | ------------------------------------------------------------ |
| API      | C#, ASP.NET Core 8, Entity Framework Core, SQL Server        |
| Auth     | GitHub OAuth (`AspNet.Security.OAuth.GitHub`) + JWT bearer   |
| Frontend | React 19, React Router, Vite (no UI library, plain CSS)      |
| Tests    | xUnit + `WebApplicationFactory` + SQLite, Vitest + Testing Library |
| Deploy   | GitHub Actions → AWS EC2 (API serves the built frontend)     |

## Setup

You need the .NET 8 SDK, Node 20.19+ (22 recommended), and Docker (for SQL Server).

**1. Start SQL Server**

```bash
docker compose up -d
```

On Apple Silicon this runs `azure-sql-edge`. Without the compose plugin, run the
same container directly:

```bash
docker run -d --name devportfolio-sql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='YourPassword123!' \
  -p 1433:1433 mcr.microsoft.com/azure-sql-edge:latest
```

**2. Create a GitHub OAuth app.** Go to github.com/settings/developers → *New OAuth App*:

- Homepage URL: `http://localhost:5173`
- Authorization callback URL: `http://localhost:5000/api/auth/github/callback`

**3. Configure the API**

```bash
cd backend/DevPortfolio
cp appsettings.Development.example.json appsettings.Development.json
# fill in Jwt:Secret and your GitHub client id/secret (this file is gitignored)
```

**4. Run the API** (port 5000; applies database migrations automatically in Development)

```bash
cd backend/DevPortfolio
dotnet run --launch-profile http
```

**5. Run the frontend** (in a second terminal)

```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:5173 and log in with GitHub.

## Usage

```bash
# Backend: integration + unit tests (no SQL Server or network needed)
dotnet test backend/DevPortfolio.sln

# Frontend
cd frontend
npm test          # Vitest
npm run lint      # oxlint
npm run build     # production bundle in frontend/dist
```

API docs (Swagger UI) are at http://localhost:5000/swagger while the API runs in Development.

## API

| Method | Path                         | Auth | Purpose                                   |
| ------ | ---------------------------- | ---- | ----------------------------------------- |
| GET    | `/api/auth/github`           |      | Start GitHub login                        |
| GET    | `/api/profile/{username}`    |      | Public profile, projects, activity        |
| GET    | `/api/me`                    | JWT  | Signed-in user's profile                  |
| PUT    | `/api/profile`               | JWT  | Update display name and bio               |
| GET/POST | `/api/projects`            | JWT  | List or create your projects              |
| PUT/DELETE | `/api/projects/{id}`     | JWT  | Update or delete a project                |
| PUT    | `/api/projects/reorder`      | JWT  | Set order from a full list of project ids |
| GET    | `/api/github/repos`          | JWT  | Your public repos, flagged if imported    |
| POST   | `/api/github/import`         | JWT  | Create projects from repo names           |
| POST   | `/api/github/activity/sync`  | JWT  | Refresh activity (at most every 10 min)   |

## Project structure

```
backend/
  DevPortfolio/          ASP.NET Core API
    Controllers/         Auth, Profile, Projects, GitHub endpoints
    Services/            JWT, GitHub API client, activity sync
    Migrations/          EF Core migrations
  DevPortfolio.Tests/    xUnit integration + unit tests
  schema.sql             Hand-written schema, mirrors the migrations
frontend/
  src/pages/             Home, Dashboard, PublicProfile, AuthCallback
  src/components/        Heatmap, project form, repo import panel, layout
  src/lib/               Auth token store, formatting and activity helpers
.github/workflows/       ci.yml (PRs) and deploy.yml (main → EC2)
```

## Configuration

ASP.NET Core reads these from `appsettings*.json` or from environment variables
(use `__` for the `:` separator, for example `GitHub__ClientId`). See
`backend/DevPortfolio/appsettings.Development.example.json`.

| Key                                   | Purpose                                                    |
| ------------------------------------- | ---------------------------------------------------------- |
| `ConnectionStrings:DefaultConnection` | SQL Server connection string                               |
| `Jwt:Secret`                          | HMAC key for issued tokens (32+ characters)                |
| `Jwt:ExpiryHours`                     | Token lifetime (default 24)                                |
| `GitHub:ClientId`, `GitHub:ClientSecret` | OAuth app credentials                                   |
| `GitHub:ApiToken`                     | Optional. Raises the GitHub API limit from 60 to 5,000 requests/hour |
| `Frontend:Origin`                     | Allowed CORS origin, and where login errors redirect       |
| `Frontend:CallbackUrl`                | Where the API sends the browser after login                |

Frontend: `VITE_API_BASE` overrides the API URL (defaults to
`http://localhost:5000/api` in dev and same-origin `/api` in production builds).

## Deployment

Every PR runs `ci.yml`. Pushes to `main` run `deploy.yml`, which reruns CI,
builds the frontend into the API's `wwwroot`, and ships a single release to EC2
by SSH: the release is copied to a new folder, the `current` symlink is
switched, and the service restarts. Required repo secrets: `EC2_HOST`,
`EC2_USER`, `EC2_SSH_KEY`.

Production notes:

- Serve over HTTPS. The OAuth correlation cookie is only relaxed for plain HTTP in Development.
- Set `Frontend:Origin` and `Frontend:CallbackUrl` to the public URL, and update the
  OAuth app's callback to `https://<host>/api/auth/github/callback`.
- Migrations don't auto-apply outside Development. Run `dotnet ef database update`
  (or apply `backend/schema.sql` to a fresh database) as a deliberate step.
- This is a single instance: the symlink swap shortens the restart window but
  isn't zero-downtime. That would need a load balancer across two or more instances.
