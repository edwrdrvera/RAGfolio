# JobLedger — Codex Project Context

## Communication and teaching style

Talk to Edward like a junior developer who already understands general programming concepts, but is new to C# and .NET. Do not over-explain cross-language fundamentals; do explain .NET and ASP.NET Core idioms, conventions, and terminology when they matter. Use plain language, keep jargon and filler low, and say why a recommendation fits this project.

This is a learning project. Treat it as a teaching-assistant relationship, not an autopilot coding task:

- Default to explaining concepts, reviewing Edward's code, debugging alongside him, and answering why something fails.
- Do not write complete implementations or solve exercises unless Edward explicitly asks for a working reference to compare against.
- When shown broken code, help identify the cause and explain why it failed before offering a correction. Keep any example correction focused on the issue.
- It is useful to flag unusually steep learning curves and common gotchas in advance, but do not preemptively build solutions for problems Edward has not encountered.
- If Edward explicitly asks to implement, modify, or generate code, do that work and explain the relevant .NET-specific decisions concisely.

## Project

**JobLedger** is a job-search-tracker API replacing a manual spreadsheet. It tracks companies, applications, resume versions, and job postings/cover letters. Resume versions and job postings/cover letters store their full text, rather than only labels or filenames.

Edward is moving from a FreeCodeCamp foundational C# course toward building a working, tested, CI-enabled, containerized ASP.NET Core API over a self-paced ~10-day schedule. The same repository is used every day rather than a disposable tutorial project.

## Current status

**Active day: Day 4 — Relationships and real queries**

- Day 1 (Minimal APIs and dependency injection) — ✅ Complete
- Day 2 (EF Core setup) — ✅ Complete
- Day 3 (Database-backed CRUD) — ✅ Complete
- Day 4 (Relationships and real queries) — 🔄 In progress

## Stack

- ASP.NET Core Minimal APIs (no controllers)
- EF Core with PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`
- JWT or ASP.NET Identity authentication; roles are `Owner` (full read/write) and `Viewer` (read-only)
- xUnit with `WebApplicationFactory` integration tests
- GitHub Actions CI
- Docker and Docker Compose; deployment as a Portainer stack on a home server, with Pi-hole for local DNS

## Repository layout

The project deliberately uses a single-project layout for now rather than a solution with separate `src` and `tests` projects. Do not recommend or perform a restructuring unless Edward asks. The project can be split after the API outgrows v1 scope.

## v1 scope

Do not add or recommend:

- A frontend; Swagger is the UI for v1
- RAG, vector search, embeddings, or `pgvector`
- Semantic Kernel
- Microservices

The full-text fields on `ResumeVersion` and `JobPosting`/`CoverLetter` are the sole forward-looking v1 design choice. They make a future RAG layer possible without a schema rebuild; they do not authorize retrieval logic in v1.

## Roadmap context only

- **v2:** A hosted `BackgroundService` that checks stale applications daily and sends email or Slack notifications.
- **v3:** Chunk stored resume/posting text, generate embeddings, add a `pgvector` column, and create a retrieval endpoint for finding relevant prior resume bullets for a posting.
- **Optional:** A Grafana dashboard on the home server.

Do not fold roadmap items into current work. Revisit them only once ASP.NET Core and EF Core are second nature.

## Learning schedule

### Before Day 1 — Install .NET 8 SDK

Use the .NET 8 SDK (LTS and close to what most employers run in production).

### ✅ Day 1 — Minimal APIs and dependency injection

Read:

- Microsoft Learn, "Tutorial: Create a Minimal API with ASP.NET Core"
- Microsoft Learn, "Dependency injection in ASP.NET Core" — service lifetimes only: Transient, Scoped, and Singleton

Build:

- A new Minimal API project without controllers
- Two or three hardcoded GET/POST `Application` routes for company, role title, and status; no database yet
- Understand `Program.cs` top to bottom before continuing

### ✅ Day 2 — EF Core setup

Read:

- EF Core Getting Started tutorial
- `Npgsql.EntityFrameworkCore.PostgreSQL` setup documentation

Build:

- `Company` and `Application` entities plus a `DbContext`; `ResumeVersion` and `JobPosting`/`CoverLetter` wait until Day 4
- A local PostgreSQL connection
- The first migration and a few seeded real applications

### ✅ Day 3 — Database-backed CRUD

Read the CRUD part of the EF Core tutorial and apply it to this schema.

Build full CRUD endpoints for `Application` using asynchronous EF Core queries and an injected `DbContext`.

### 🔄 Day 4 — Relationships and real queries

Read an EF Core relationships tutorial covering one-to-many and many-to-many relationships with migrations.

Build:

- A `ResumeVersion` entity that stores full text and has a many-to-many relationship with `Application`
- A full-text `JobPosting`/`CoverLetter` entity with a one-to-many relationship from `Application`
- `GET /applications/stale?days=X` and `GET /resumeversions/{id}/usage-count`, using `.Include()`

### Day 5 — Authentication

Read Microsoft Learn's authentication and authorization guidance for Minimal APIs, plus JWT documentation if issuing tokens rather than using full Identity.

Build registration/login endpoints, password hashing, token issuance, and manual Swagger verification.

### Day 6 — Authorization

Continue the Day 5 material, focusing on roles and policies.

Build `Owner` and `Viewer` seed roles; protect POST/PATCH endpoints for `Owner` and verify with real tokens.

### Day 7 — Testing

Read Microsoft Learn's ASP.NET Core integration-testing guidance, especially `WebApplicationFactory`.

Build integration tests that hit real endpoints rather than only isolated unit tests.

### Day 8 — CI

Build a GitHub Actions workflow that runs the test suite on every push. This should be broadly similar to the setup in PokeLogAPI.

### Day 9 — Dockerize and deploy

Build a Dockerfile and Docker Compose configuration with PostgreSQL. Deploy it as a Portainer stack on the home server and configure reverse proxy and Pi-hole local DNS for a real hostname.

### Day 10 — Buffer and polish

- Resolve issues found along the way.
- Clean up Swagger/OpenAPI documentation.
- Write the README.
- Draft resume bullets while project details are fresh.
