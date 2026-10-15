# NAVYCommerce Backend

REST API for NAVYCommerce, a multi-vendor e-commerce platform. Built with ASP.NET Core, Entity Framework Core, and PostgreSQL.

## Features

- JWT authentication and role-based authorization.
- Registration, OTP verification, and OTP resend.
- Store management with ownership checks.
- Product categories and seller product management.
- Product options, variants, pricing, and inventory.
- Product galleries with image processing and Azure Blob Storage integration.
- Background jobs for storage file deletion.
- Product publication and a public catalog with search, filters, sorting, and pagination.

## Tech Stack

- .NET 8 / ASP.NET Core Web API
- Entity Framework Core 8 / Npgsql
- PostgreSQL 16
- Azure Blob Storage / Azurite
- Docker Compose
- Swagger / OpenAPI

## Getting Started

### Prerequisites

- .NET 8 SDK
- Docker Desktop running with Docker Compose available
- `dotnet-ef` compatible with the project's EF Core 8 packages

Verify your environment:

```powershell
dotnet --version
dotnet ef --version
docker info
docker compose version
```

If `dotnet-ef` is already installed and compatible, no additional tool installation is needed.

### 1. Configure the Environment

From the repository root, create `.env.docker` using the variables referenced in `compose.yaml`. If an environment example is included, copy it rather than renaming it.

Configure the PostgreSQL credentials, JWT settings, and storage connection required by the API. Keep local configuration and secrets out of Git.

For the local setup:

| Service | Address from the host machine |
| --- | --- |
| API | `http://localhost:5175` |
| PostgreSQL | `127.0.0.1:5433` |
| Azurite Blob service | `http://127.0.0.1:10000` |

Inside Docker, PostgreSQL is reached through `postgres:5432`, not `localhost:5433`. Use the host address when running migrations from PowerShell.

### 2. Start the Containers

```powershell
docker compose --env-file .env.docker up -d --build
docker compose --env-file .env.docker ps
```

Wait until PostgreSQL is healthy before applying migrations. If a service fails to start, inspect its logs:

```powershell
docker compose --env-file .env.docker logs --tail 100
```

### 3. Apply Database Migrations

Restore the project dependencies:

```powershell
dotnet restore .\src\NAVYCommerce.API\NAVYCommerce.API.csproj
```

In the same PowerShell session, set the host connection string using the database name, username, and password configured for PostgreSQL:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5433;Database=YOUR_DATABASE;Username=YOUR_USERNAME;Password=YOUR_PASSWORD"

dotnet ef database update `
  --project .\src\NAVYCommerce.Infrastructure `
  --startup-project .\src\NAVYCommerce.API `
  --context ApplicationDbContext `
  --connection "$env:ConnectionStrings__DefaultConnection"
```

Replace the placeholder values before running the command. Running EF tooling on the host also requires any startup configuration validated by the API; `.env.docker` is not automatically loaded by `dotnet` commands.

Apply the existing migrations after pulling changes. Do not create a new migration just to set up your local database.

### 4. Test the API

Open [Swagger](http://localhost:5175/swagger) when the API is running in the Development environment.

For protected endpoints, log in and enter `Bearer YOUR_ACCESS_TOKEN` in Swagger's **Authorize** dialog. Logging in alone does not automatically authorize subsequent Swagger requests.

To test product creation, use a verified seller account, create a store owned by that account, and select an existing category.

## Project Structure

| Project | Responsibility |
| --- | --- |
| `NAVYCommerce.API` | HTTP endpoints, request/response contracts, API services, and application configuration |
| `NAVYCommerce.Application` | Application abstractions, including storage contracts |
| `NAVYCommerce.Domain` | Domain entities and enums |
| `NAVYCommerce.Infrastructure` | Database context, entity configurations, migrations, and infrastructure implementations |

## Updating Your Local Copy

1. Pull the latest changes with `git pull`.
2. Check for new environment variables.
3. Run `docker compose --env-file .env.docker up -d --build`.
4. If migrations were added, run the migration command above after PostgreSQL is healthy.
5. Test the updated endpoints in Swagger.

Database migrations update the schema. They do not copy another developer's local data.

## Stopping the Environment

```powershell
docker compose --env-file .env.docker down
```

This removes the containers and Compose network while retaining named volumes. Avoid `down -v` unless you intend to delete the local database and storage data held in those volumes.

## Project Status

Active development. Authentication, stores, product management, image storage, and catalog browsing are implemented. Checkout and payment processing are not included yet.
