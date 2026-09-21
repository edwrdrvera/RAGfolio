# JobLedger

A personal job-search tracker API. Records companies, applications, resume versions, and job postings — a structured log of job-search activity, not a tool for finding jobs.

## Stack

- ASP.NET Core Minimal APIs (no controllers)
- EF Core + PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- JWT auth with role-based access — `Owner` (full read/write) and `Viewer` (read-only)
- xUnit + `WebApplicationFactory` integration tests
- GitHub Actions CI
- Docker + Docker Compose

## Project layout

```
JobLedger.slnx          solution
JobLedger.csproj        ASP.NET Core web app (root)
JobLedger.Tests/        xUnit integration tests
Migrations/             EF Core migrations
Models/                 entities and DTOs
Data/                   DbContext
```

## Running locally

**Prerequisites:** .NET 10 SDK, PostgreSQL running locally.

```bash
# Apply migrations (first time only)
dotnet ef database update

# Run
dotnet run
```

The API starts at `http://localhost:5047`. Open `http://localhost:5047/scalar/v1` for the interactive docs.

### Environment / config

Secrets are read from `appsettings.Development.json` (not committed). Create one at the project root:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=jobledger;Username=<user>;Password=<password>"
  },
  "Jwt": {
    "Key": "<random-base64-string>",
    "Issuer": "jobledger",
    "Audience": "jobledger"
  }
}
```

## Running with Docker Compose

```bash
# Create a .env file at the repo root
cp .env.example .env   # then fill in values
docker compose up --build
```

The API is available at `http://localhost:8080` and the Scalar docs at `http://localhost:8080/scalar/v1`.

`.env` values:

| Variable | Description |
|---|---|
| `POSTGRES_USER` | Database username |
| `POSTGRES_PASSWORD` | Database password |
| `POSTGRES_DB` | Database name |
| `JWT_KEY` | Random base64 string (32+ bytes) |
| `JWT_ISSUER` | Token issuer (e.g. `jobledger`) |
| `JWT_AUDIENCE` | Token audience (e.g. `jobledger`) |

## API usage

All write endpoints require an `Owner` JWT. The flow:

```bash
# 1. Register
curl -X POST http://localhost:5047/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username": "ed", "password": "hunter2"}'

# 2. Login — copy the token from the response
curl -X POST http://localhost:5047/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "ed", "password": "hunter2"}'

# 3. Use the token on protected routes
curl -X POST http://localhost:5047/applications \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{"companyName": "Acme", "role": "Software Engineer", "status": "Applied"}'
```

Or paste the token into the **Authentication** panel in Scalar and use the built-in request runner.

## Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/applications` | — | List all applications |
| GET | `/applications/{id}` | — | Get one application |
| POST | `/applications` | Owner | Create an application |
| PUT | `/applications/{id}` | Owner | Update an application |
| DELETE | `/applications/{id}` | Owner | Delete an application |
| GET | `/applications/stale?days=N` | — | Applications not updated in N days |
| GET | `/resumeversions/{id}/usage-count` | — | How many applications used a resume version |
| POST | `/auth/register` | — | Register a new user |
| POST | `/auth/login` | — | Login; returns a JWT |

## Tests

```bash
dotnet test
```

Integration tests use `WebApplicationFactory` with EF Core InMemory — no real database needed.

## Future directions

Resume versions and job postings store their full text rather than just filenames or labels. That's intentional: a future v2 RAG layer (given a job posting, retrieve the most relevant past resume bullets from your own history) can be added without a schema rebuild.
