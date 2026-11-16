# AGENTS.md — Global AI Agent System & Guidelines

> **Repository:** Directorio Geo de Negocios de Bolivia  
> **Status:** Versión 1.0 | Fecha: 2026-11-16  
> **Authority:** This document is the primary instruction set and operational contract for AI coding agents (Antigravity, Cursor, Claude, Copilot, etc.) working across this monorepo.

---

## 1. Contexto de Negocio y Alcance Funcional (Español)

### 1.1 Objetivo del Producto
Directorio Geo es una plataforma web para encontrar negocios y sucursales en Bolivia, verificar si están abiertos, consultar canales de contacto, rangos de precios y fotos, y abrir indicaciones de navegación en mapas.
- **Entidad principal de búsqueda:** La **SUCURSAL** (`branch`). Un negocio (`business`) puede tener múltiples sucursales geolocalizadas individualmente.
- **Ámbito geográfico:** Exclusivamente **Bolivia**. Coordenadas y geometrías espaciales en **PostGIS Geography SRID 4326** (`[longitud, latitud]`).
- **Público objetivo:** Visitantes sin necesidad de registro ni conocimientos GIS («Cerca de mí», «Buscar en esta zona», «Abierto ahora»).

### 1.2 Entregas y Fases del Proyecto
- **P0:** Base técnica, integridad de base de datos, seguridad Identity JWT, operaciones de diagnóstico y despliegue Docker.
- **P1 (MVP Público y Administración):** Catálogo administrable de categorías, búsqueda pública de sucursales, visualización en mapa, administración por rol `Admin`.
- **P2 (Autogestión por Propietarios):** Registro público y autogestión de sucursales por propietarios verificados (`Owner`). No bloquea el MVP.
- **Fuera de alcance del MVP:** Reseñas de usuarios, favoritos, publicidad paga, autocompletado predictivo, importaciones masivas y motor propio de rutas (las tablas existentes en BD se conservan, pero no se deben desarrollar endpoints ni vistas no autorizadas).

### 1.3 Jerarquía Territorial
- Departamento > Provincia > Municipio > Zona (opcional) > Barrio (opcional).
- No inventar subdivisiones cuando no existan en la base de datos oficial.

---

## 2. Monorepo Architecture & Technology Stack (English)

The repository is organized as a lightweight monorepo with three core domains:

```text
directorio-geo/
├── AGENTS.md                                # This authoritative agent guide
├── README.md                                # Root technical overview & orchestration
├── REQUERIMIENTOS_API.md                    # Normative API functional contract
├── Funcionalidades_Directorio_Geo_SQL_v1_3.md # Functional requirements & acceptance criteria
├── docker-compose.yml                       # Production multi-container orchestration
├── docker-compose.dev.yml                   # Local development container overrides
├── .env.example                             # Environment variable template
├── backend/                                 # ASP.NET Core 10 Web API
│   ├── README.md                            # Backend specific documentation
│   ├── backend.slnx / backend.csproj        # Solution and project files
│   ├── Program.cs                           # App bootstrapping & middleware pipeline
│   ├── Controllers/                         # HTTP Controllers (/api/v1/*)
│   ├── Services/                            # Business & domain services
│   ├── DTOs/                                # Strongly typed requests/responses
│   ├── Data/                                # AppDbContext (EF Core Database-First)
│   ├── Models/                              # Scaffolding & spatial entities
│   ├── Middleware/                          # Global exception & problem details handling
│   └── Dockerfile                           # Multi-stage .NET 10 runtime
├── frontend/                                # Next.js 16 Web Application
│   ├── README.md                            # Frontend documentation & conventions
│   ├── package.json / pnpm-lock.yaml        # Dependencies (pnpm)
│   ├── next.config.ts                       # Next.js standalone output & Turbopack
│   ├── src/app/                             # Next.js App Router (React 19)
│   ├── public/                              # Static assets (including robots.txt)
│   ├── AGENTS.md                            # Next.js agent rules (managed by next dev)
│   └── Dockerfile                           # Multi-stage Node 22 Alpine standalone runner
└── db/                                      # PostgreSQL + PostGIS Database
    ├── Dockerfile                           # Custom image with PostGIS & GIS tools
    ├── DB_DECISIONS.md                      # Normative database decisions
    └── init/                                # SQL seed and initialization scripts
```

### 2.1 Fixed Technical Stack
Do not replace, migrate, or introduce conflicting frameworks:
- **Backend:** C# 13, .NET 10 (`net10.0`), ASP.NET Core Minimal/Controllers, Entity Framework Core 10 (`Microsoft.EntityFrameworkCore`), Npgsql 10 with NetTopologySuite.
- **Frontend:** Next.js 16 (App Router), React 19, TypeScript 5, Tailwind CSS 4, Turbopack, pnpm.
- **Database:** PostgreSQL 18 with PostGIS 3.6. Schema: `catalog`, `app`, `geo`, `public`.
- **Maps:** Mapbox GL JS (`react-map-gl`) with environment-configured public token.
- **Photos / Media:** `BYTEA` storage in PostgreSQL; binary delivery streamed through the backend API.
- **Authentication:** ASP.NET Core Identity with Guid keys, JWT Bearer tokens, HTTP-only refresh cookies.

---

## 3. Strict Rules for AI Coding Agents (English)

### 3.1 Contract-First & Route Integrity
1. **API Route Canon:** All backend routes MUST use the `/api/v1` prefix (e.g. `/api/v1/categories`, `/api/v1/branches`). Never expose naked `/api/[controller]` endpoints.
2. **Authority of `REQUERIMIENTOS_API.md`:** This file is the single source of truth for request/response payloads, HTTP status codes, validation rules, and error formatting (`application/problem+json`). Never create ad-hoc endpoints outside the documented inventory.
3. **Consistent Envelope:** All success responses must wrap data in `ApiResponse<T>` or `ApiResponse<PagedResult<T>>` when returning collections.

### 3.2 Database-First & PostGIS Rules
1. **Source of Truth:** PostgreSQL DDL scripts in `db/init/` and database migrations are the primary source of truth. Never generate Entity Framework code-first migrations that overwrite database architecture.
2. **Spatial Data Handling:** Use NetTopologySuite types (`Point`, `Polygon`) with SRID 4326. Remember coordinates in GeoJSON and PostGIS are `[longitude, latitude]`. Do not invert coordinates.
3. **Soft Deletes:** Respect audit columns: `created_at`, `updated_at`, `deleted_at`, `created_by_user_id`, `updated_by_user_id`, `deleted_by_user_id`. Never perform hard physical deletes on catalog items.

### 3.3 Backend Coding Standards
1. **Language Features:** C# nullable reference types are enabled (`<Nullable>enable</Nullable>`). Implicit usings are disabled (`<ImplicitUsings>disable</ImplicitUsings>`). Always include explicit `using` statements.
2. **Dependency Injection:** Use primary constructors in C# classes (e.g., `public sealed class CategoriesController(...) : ControllerBase`).
3. **Cancellation Tokens:** All async controller actions and service methods MUST accept and propagate `CancellationToken ct`.
4. **Exception Handling:** Do not wrap controllers in broad `try-catch` blocks returning generic 500s. Let exceptions bubble up to `ApiExceptionHandler`, which maps domain exceptions to RFC 7807 `ProblemDetails`.

### 3.4 Frontend Coding Standards
1. **App Router & Server Components:** By default, React components are Server Components. Only add `"use client"` when state, effects, browser APIs, or map interactions are strictly necessary.
2. **Next.js Breaking Changes & Agent File:** Respect `frontend/AGENTS.md`. It is auto-generated by Next.js (`generate-agent-files.js`) to alert agents of Next.js 16 conventions. Do not delete or commit modifications that break it.
3. **Tailwind CSS 4:** Use modern Tailwind v4 utilities and CSS variable themes. Avoid legacy Tailwind config abstractions.
4. **Map Isolation:** Any component rendering Mapbox must be a client component and dynamically imported with `ssr: false` if needed to prevent server-side evaluation errors.

---

## 4. Verification & Command Workflows (English)

Agents MUST verify that code compiles and builds before reporting completion.

### 4.1 Backend Verification
From repo root or `backend/`:
```powershell
# Restore & build backend solution
dotnet restore backend/backend.slnx
dotnet build backend/backend.slnx -c Release

# Run backend locally (requires running PostgreSQL)
dotnet run --project backend/backend.csproj --launch-profile http
```

### 4.2 Frontend Verification
From repo root or `frontend/`:
```powershell
# Install dependencies & lint
cd frontend
pnpm install --frozen-lockfile
pnpm lint

# Production build test
pnpm build
```

### 4.3 Container & Orchestration
```powershell
# Start database and local dev stack
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d db --wait

# Build full production containers
docker compose build
```

---

## 5. Documentation Synchronization Protocol (Bilingual)

Whenever an agent alters code, adds an endpoint, modifies environment variables, or updates dependencies:
1. **Update `README.md` (root):** Keep stack descriptions, project file trees, and deployment steps synchronized.
2. **Update `backend/README.md` / `frontend/README.md`:** Ensure status tables, prerequisites, and instructions match actual source code.
3. **Update `REQUERIMIENTOS_API.md`:** If new endpoints are approved, document route, authorization, DTO schemas, and status codes.
4. **Keep `robots.txt` updated:** Ensure search engine crawlers are directed appropriately in `frontend/public/robots.txt`.
5. **No Ghost Changes:** Do not leave unreferenced files, broken references, or commented-out obsolete documentation blocks.
