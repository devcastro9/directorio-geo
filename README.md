# Directorio Geo de negocios de Bolivia

> **Documentación oficial:** Versión 1.0, 2026-11-16.

El producto permite encontrar comercios y sucursales en Bolivia, consultar horarios, canales de contacto, rangos de precios orientativos y fotos, y abrir indicaciones de navegación en mapas interactivos. El visitante no necesita registro ni conocimientos geográficos («Cerca de mí», «Buscar en esta zona», «Abierto ahora»).

---

## 1. Documentos y Alcance

- [Reglas de Agentes de IA](AGENTS.md): Guía autoritativa, arquitectura monorepo, directrices técnicas bilingües y restricciones para agentes de codificación.
- [Funcionalidades](Funcionalidades_Directorio_Geo_SQL_v1_3.md): Alcance de producto, valor para el usuario, entregas (P0, P1, P2) y criterios de aceptación.
- [Contrato API](REQUERIMIENTOS_API.md): Fuente normativa de rutas (`/api/v1/*`), parámetros, DTOs JSON/GeoJSON, permisos y reglas de validación.
- [Requerimientos Frontend](requirements-frontend.md): Especificación integral de UI/UX (Penpot), portal público y portal administrativo Next.js 16 / React 19.
- [README del backend](backend/README.md): Estructura ASP.NET Core 10, configuración, estado de endpoints implementados y comandos de ejecución.
- [README del frontend](frontend/README.md): Estructura Next.js 16, React 19, Tailwind CSS 4, visor de mapas Mapbox y scripts de desarrollo.
- **Este README:** Entorno compartido, base de datos PostgreSQL/PostGIS, orquestación Docker y orden de implementación.

La planificación se divide en:
- **P0:** Base técnica, integridad de base de datos, seguridad Identity y operación.
- **P1:** MVP público administrable (búsqueda de sucursales, fichas de información, categorías y mapa).
- **P2:** Autogestión por propietarios asociados (`Owner`). No bloquea el MVP.

Quedan fuera del alcance del MVP: favoritos, reseñas de usuarios, publicidad paga, analítica de visitantes, importaciones masivas, autocompletado predictivo y motor propio de rutas. Las tablas correspondientes se conservan en la base de datos sin obligar a implementar endpoints ni interfaces prematuras.

---

## 2. Estado Real y Stack Tecnológico

| Área | Disponible en el código | Pendiente |
| --- | --- | --- |
| **Backend** | Consulta de prueba (`/api/v1/Prueba`), API de Categorías (`/api/v1/categories`), `ApiExceptionHandler` (RFC 7807), contexto EF Core y modelos SQL/Identity. Compilación limpia (`dotnet build` exitoso). | Completar servicio JWT y endpoints de autenticación, sucursales y negocios. |
| **Frontend** | Base Next.js 16 (App Router), React 19, Tailwind CSS 4, Turbopack, salida `standalone`, `robots.txt` y Dockerfile multi-stage. Build verificado. | Buscador, visor interactivo Mapbox, fichas de detalle y consumo de API. |
| **Base de datos** | PostgreSQL 18 + PostGIS 3.6, esquemas `app`, `catalog`, `geo` y `public`, línea base SQL y pruebas transaccionales. | Incrementos de datos territoriales comprobados. |
| **Operación** | Docker Compose multi-contenedor, healthchecks, perfiles de desarrollo y pgAdmin opcional. | Proxy inverso HTTPS para producción. |

### Stack inamovible

| Componente | Tecnología y versión |
| --- | --- |
| **Backend** | ASP.NET Core 10, C# 13, `net10.0`, EF Core 10.0.14 |
| **Datos y Espacial** | PostgreSQL 18, PostGIS 3.6, Npgsql 10.0.3, NetTopologySuite (SRID 4326), Database-First |
| **Autenticación** | ASP.NET Core Identity con claves Guid y tokens JWT Bearer |
| **Frontend** | Next.js 16 (App Router), React 19, TypeScript 5, Tailwind CSS 4, pnpm 12.16.1 |
| **Mapas** | Mapbox GL JS (`react-map-gl`) con token público |
| **Fotografías** | Almacenamiento en columnas `BYTEA` en PostgreSQL; entrega binaria mediante API |
| **Ejecución** | Docker Compose, Alpine Linux, salida Next.js standalone |

---

## 3. Estructura del Monorepo

```text
directorio-geo/
├── AGENTS.md                                # Directrices autoritativas para agentes de IA
├── README.md                                # Este documento
├── REQUERIMIENTOS_API.md                    # Contrato normativo HTTP
├── Funcionalidades_Directorio_Geo_SQL_v1_3.md # Especificación funcional
├── docker-compose.yml                       # Orquestación de producción
├── docker-compose.dev.yml                   # Modificaciones para desarrollo
├── .env.example                             # Plantilla de variables de entorno
├── backend/
│   ├── README.md                            # Documentación del backend
│   ├── backend.csproj / backend.slnx        # Proyecto y solución .NET 10
│   ├── Program.cs                           # Pipeline y registro de servicios
│   ├── Controllers/                         # Categories (/api/v1/categories), Prueba, Auth
│   ├── DTOs/                                # DTOs de categorías, ApiResponse, PagedResult
│   ├── Services/                            # ICategoryService, CategoryService
│   ├── Data/                                # AppDbContext con soporte spatial
│   ├── Identity/                            # ApplicationUser y ApplicationRole
│   ├── Models/                              # Modelos de base de datos
│   ├── Middleware/                          # ApiExceptionHandler (RFC 7807)
│   └── Dockerfile                           # Contenedor .NET 10 runtime
├── frontend/
│   ├── README.md                            # Documentación del frontend
│   ├── Dockerfile                           # Contenedor multi-stage Next.js standalone
│   ├── .dockerignore
│   ├── AGENTS.md                            # Reglas de Next.js (manejado por next dev)
│   ├── package.json / pnpm-lock.yaml
│   ├── next.config.ts                       # Configuración standalone y Turbopack
│   ├── public/                              # Archivos públicos y robots.txt
│   └── src/app/                             # Next.js App Router (React 19)
└── db/
    ├── Dockerfile                           # PostgreSQL 18 + PostGIS 3.6
    ├── AUDIT_DECISIONS.md                   # Registro de decisiones del esquema
    └── init/                                # 01_schema.sql y 02_test.sql
```

---

## 4. Instalación y Ejecución con Docker

Requisitos: Docker Engine con contenedores Linux y Compose v2. Desde la raíz del repositorio, preparar el archivo `.env`:

```powershell
if (-not (Test-Path -LiteralPath .env)) {
    Copy-Item -LiteralPath .env.example -Destination .env
}
```

Configurar las contraseñas y puertos en `.env`.

### Variables Principales en `.env`

| Variable | Descripción | Valor por defecto |
| --- | --- | --- |
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | Credenciales de base de datos | Obligatorias |
| `BACKEND_PORT` | Puerto público del backend en host | `8000` |
| `FRONTEND_PORT` | Puerto público del frontend en host | `3000` |
| `POSTGRES_PORT` | Puerto publicado de PostgreSQL (solo con override dev) | `5432` |
| `PGADMIN_PORT` | Puerto de pgAdmin4 (con perfil tools) | `5050` |

### Arranque del Entorno Completo

```powershell
# Validar configuración
docker compose config --quiet

# Construir imágenes de backend y frontend
docker compose build

# Levantar servicios en segundo plano
docker compose up -d --wait
```

- **Frontend:** http://localhost:3000
- **Backend API (Categorías):** http://localhost:8000/api/v1/categories
- **Backend API (Prueba):** http://localhost:8000/api/v1/Prueba

### Entorno de Desarrollo (con exposición de PostgreSQL y Scalar)

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build --wait
```

- **PostgreSQL accesible en:** `localhost:5432`
- **Documentación OpenAPI:** http://localhost:8000/openapi/v1.json
- **Scalar UI:** http://localhost:8000/scalar/v1

### Herramienta opcional: pgAdmin4

```powershell
docker compose --profile tools up -d pgadmin
```
Acceder a http://localhost:5050 e iniciar sesión con las credenciales configuradas en `.env`.

---

## 5. Desarrollo Local sin Contenedores para la Aplicación

Para desarrollar con recarga rápida manteniendo la base de datos en Docker:

```powershell
# 1. Levantar solo la base de datos exponiendo el puerto 5432
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d db --wait

# 2. Configurar la cadena de conexión local en User Secrets y compilar el backend
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Host=localhost;Port=5432;Database=directorio;Username=directorio;Password=<contraseña-local>' --project backend/backend.csproj
dotnet restore backend/backend.slnx
dotnet build backend/backend.slnx
dotnet run --project backend/backend.csproj --launch-profile http
```
El backend estará disponible en `http://localhost:5089`.

En otra terminal, para el frontend:

```powershell
cd frontend
pnpm install
pnpm dev
```
El frontend estará disponible en `http://localhost:3000` con hot reload de Turbopack.

---

## 6. Comprobaciones de Integridad

```powershell
# Verificar diferencias sin cambios no deseados
git diff --check

# Compilar backend .NET 10 (0 errores esperados)
dotnet build backend/backend.slnx --configuration Release

# Compilar frontend Next.js 16 (0 errores esperados)
pnpm --dir frontend build

# Validar sintaxis de Docker Compose
docker compose config --quiet
```
