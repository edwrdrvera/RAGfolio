# RAGfolio

A minimal API that tracks companies, applications, resume versions, and job postings/cover letters — a personal record of job-search activity, not a tool for finding jobs.

## Stack

- ASP.NET Core Minimal APIs (no controllers)
- EF Core + PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- JWT auth with role-based access — `Owner` (full read/write) and `Viewer` (read-only)
- xUnit + `WebApplicationFactory` integration tests
- Docker + GitHub Actions — planned

## Current state

A database-backed API with authentication, authorization, and integration tests:

- Full CRUD for `Application` over EF Core + PostgreSQL, using async queries and an injected `DbContext`
- `ResumeVersion` (many-to-many with `Application`) and `JobPosting`/`CoverLetter` (one-to-many from `Application`), both storing full text
- Relationship-driven queries: `GET /applications/stale?days=X` and `GET /resumeversions/{id}/usage-count`
- Registration/login endpoints with password hashing and JWT issuance
- `Owner`/`Viewer` roles with policy-protected write endpoints
- `WebApplicationFactory` integration tests hitting real endpoints, running against EF Core InMemory

## Project layout

- `JobLedger.csproj` — the ASP.NET Core web app
- `JobLedger.Tests/` — xUnit integration tests
- `JobLedger.slnx` — solution tying both together

```bash
dotnet test
```

## Destination

Resume versions and job posting/cover letter text are stored in full, not just as filenames or labels. That's intentional groundwork: the goal is to eventually turn this into a RAG (Retrieval-Augmented Generation) layer — given a job posting, retrieve the most relevant past resume bullets from your own history — without needing to rebuild the database schema to get there.
