# RAGfolio 

A minimal API that tracks companies, applications, and resume versions — a personal record of job-search activity (applications, statuses, resume versions), not a tool for finding jobs.

## Stack

- ASP.NET Core Minimal APIs
- EF Core + PostgreSQL
- JWT auth with role-based access (Owner/Viewer) — planned
- xUnit + WebApplicationFactory — planned
- Docker + GitHub Actions — planned

## Current state

Early scaffold, not yet backed by a real database:

- In-memory `GET`/`POST /applications` routes (data lives in a list in memory, resets on restart)
- `Company` and `Application` model classes defined
- `AppDbContext` (the EF Core class that would connect these models to Postgres) exists but is empty — no database connection yet

## Destination

Resume versions and job posting/cover letter text are stored in full, not just as filenames or labels. That's intentional groundwork: the goal is to eventually turn this into a RAG (Retrieval-Augmented Generation) layer — given a job posting, retrieve the most relevant past resume bullets from your own history — without needing to rebuild the database schema to get there.
