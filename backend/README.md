# Backend — Directorio Geo

> **Versión:** 1.0  
> **Fecha:** 2026-11-16  
> **Framework:** .NET 10 (`net10.0`), C# 13, ASP.NET Core  
> **Documentación del proyecto:** [README principal](../README.md) | [Contrato API](../REQUERIMIENTOS_API.md) | [Reglas de Agentes](../AGENTS.md)

API RESTful del directorio de negocios de Bolivia construida con ASP.NET Core 10, Entity Framework Core 10 y PostGIS. Este documento describe el estado del código del backend, su arquitectura y los procedimientos para compilarlo, ejecutarlo y extenderlo.

---

## 1. Estado Actual

El backend compila limpiamente (`dotnet build` exitoso con 0 errores y 0 advertencias). Las siguientes operaciones se encuentran implementadas y activas:

| Endpoint | Método | Estado | Descripción |
| --- | --- | --- | --- |
| `/api/v1/Prueba` | `GET` | Implementado | Prueba de acceso a datos contra `catalog.parameter_definition`. |
| `/api/v1/categories` | `GET` | Implementado | Listado paginado de categorías activas con soporte para búsqueda textual (`search`). |
| `/api/v1/categories/{id}` | `GET` | Implementado | Consulta de categoría individual por identificador UUID. |
| `/api/v1/categories` | `POST` | Implementado | Creación de nueva categoría con validación de código único (requiere rol `Admin`). |
| `/api/v1/categories/{id}` | `PUT` | Implementado | Actualización completa de datos editables de categoría (requiere rol `Admin`). |
| `/api/v1/categories/{id}` | `DELETE` | Implementado | Baja lógica de categoría devolviendo 204 No Content (requiere rol `Admin`). |
| `/api/v1/branches` | `GET` | Implementado | Búsqueda pública espacial y filtrada de sucursales (formatos JSON y GeoJSON). |
| `/api/v1/branches/{id}` | `GET` | Implementado | Consulta pública de ficha de sucursal detallada por UUID. |
| `/api/v1/businesses/{slug}` | `GET` | Implementado | Consulta pública de negocio y sus sucursales por slug. |
| `/api/v1/management/businesses` | `GET` / `POST` | Implementado | Listado administrativo y alta de negocio con primera sucursal (Admin). |
| `/api/v1/management/businesses/{id}` | `GET` / `PUT` / `DELETE` | Implementado | Detalle, actualización y baja lógica de negocio (Admin). |
| `/api/v1/management/businesses/{id}/publication` | `PUT` | Implementado | Publicación o retorno a borrador de negocio con validación exhaustiva (Admin). |
| `/api/v1/management/businesses/{id}/restore` | `POST` | Implementado | Restauración de negocio archivado y sucursales seleccionadas (Admin). |
| `/api/v1/management/businesses/{id}/branches` | `POST` | Implementado | Creación de nueva sucursal dentro del negocio (Admin). |
| `/api/v1/management/branches/{id}` | `PUT` / `DELETE` | Implementado | Actualización completa y baja lógica de sucursal (Admin). |
| `/api/v1/management/branches/{id}/restore` | `POST` | Implementado | Restauración de sucursal dada de baja (Admin). |
| `/api/v1/management/branches/{id}/photos` | `POST` | Implementado | Subida de foto binaria multipart JPEG/PNG/WebP con límites validados (Admin). |
| `/api/v1/management/photos/{id}` | `PUT` / `DELETE` | Implementado | Actualización de metadatos/portada y baja lógica de foto (Admin). |
| `/api/v1/photos/{id}` | `GET` | Implementado | Entrega binaria de foto en streaming con cabeceras nosniff y no-store. |
| `/api/v1/auth/login` | `POST` | Implementado | Autenticación con credenciales, emisión de JWT y cookie HttpOnly `dg_refresh` (P1 Admin, P2 activa). |
| `/api/v1/auth/refresh` | `POST` | Implementado | Rotación atómica de token de refresco con protección anti-replay (revocación de familia). |
| `/api/v1/auth/logout` | `POST` | Implementado | Cierre de sesión y revocación de familia de tokens con eliminación de cookie. |
| `/api/v1/auth/me` | `GET` | Implementado | Consulta de datos y roles del usuario autenticado con sesión activa vigente. |
| `/api/v1/auth/register` | `POST` | Implementado | Alta de nuevo usuario con rol User (activo a partir de P2, responde 404 en P1). |

### Componentes y Servicios Clave

| Componente | Estado en el código |
| --- | --- |
| **PostgreSQL y EF Core** | `AppDbContext` registrado con Npgsql y NetTopologySuite; timeout de comandos de 30 segundos. |
| **Manejo de Errores** | `ApiExceptionHandler` registrado con `IExceptionHandler`, devolviendo RFC 7807 (`ProblemDetails`) y mapeo a 503 para fallos transitorios de PostgreSQL. |
| **Servicio de Categorías** | `ICategoryService` y `CategoryService` implementados y registrados en inyección de dependencias. |
| **Servicio de Búsqueda Espacial** | `IBranchSearchService` y `BranchSearchService` implementados con NetTopologySuite, vistas espaciales y soporte JSON/GeoJSON. |
| **Servicio de Negocios y Sucursales** | `IBusinessService` y `BusinessService` implementados cubriendo todo el agregado de administración P1 (negocios, sucursales y fotos). |
| **DTOs Base y Dominio** | `ApiResponse<T>`, `PagedResult<T>`, `ListRequest` y DTOs canónicos en subdirectorios `Categories/`, `Branches/`, `Businesses/` y `Management/`. |
| **Identity y JWT** | Modelos `ApplicationUser` y `ApplicationRole` con claves Guid; servicio `IAuthService`/`AuthService`, rotación de refresh tokens en `AspNetUserTokens` y validación de `sid` activo en `OnTokenValidated`. |
| **OpenAPI y Scalar** | Habilitados en entorno `Development` (`/openapi/v1.json`, `/scalar/v1`). |

---

## 2. Stack Tecnológico

| Elemento | Configuración del proyecto |
| --- | --- |
| **Framework** | C#, ASP.NET Core 10, `net10.0` |
| **EF Core, Identity, JWT y OpenAPI** | Paquetes Microsoft 10.0.14 |
| **Soporte Espacial y PostgreSQL** | Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 y NetTopologySuite |
| **Documentación Interactiva** | Scalar.AspNetCore 2.19.0 |
| **Persistencia** | PostgreSQL 18 y PostGIS 3.6 (Database-First) |
| **Fotografías** | Almacenamiento en columnas `BYTEA` en PostgreSQL; entrega binaria vía API |
| **Contenedores** | Multi-stage Dockerfile sobre `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` |

---

## 3. Estructura del Código

```text
backend/
├── backend.csproj           # Configuración del proyecto C# (Nullable habilitado)
├── backend.slnx             # Solución de .NET
├── Program.cs               # Configuración de servicios, middleware y pipeline HTTP
├── appsettings.json         # Configuración base
├── appsettings.Development.json # Configuración de diagnóstico local
├── Properties/
│   └── launchSettings.json  # Perfiles de ejecución (http: 5089, https: 7273)
├── Controllers/
│   ├── ApiControllerBase.cs    # Clase base canónica con enrutamiento api/v1/[controller] y ProblemDetails
│   ├── CategoriesController.cs # CRUD y listado de categorías (/api/v1/categories)
│   ├── BranchesController.cs   # Búsqueda pública JSON/GeoJSON y ficha de sucursales (/api/v1/branches)
│   ├── BusinessesController.cs # Ficha pública de negocio por slug (/api/v1/businesses)
│   ├── ManagementController.cs # Administración integral P1 de negocios, sucursales y fotos (/api/v1/management)
│   ├── PhotosController.cs     # Entrega binaria de fotos (/api/v1/photos)
│   ├── PruebaController.cs     # Endpoint de prueba (/api/v1/Prueba)
│   └── AuthController.cs       # Endpoints de autenticación (en desarrollo)
├── DTOs/
│   ├── ApiResponse.cs          # Envoltorio canónico de respuestas API
│   ├── PagedResult.cs          # Estructura de paginación
│   ├── ListRequest.cs          # Petición base con página, tamaño y búsqueda
│   ├── Categories/             # CategoryDto, CreateCategoryRequest, UpdateCategoryRequest
│   ├── Branches/               # BranchSearchRequest, BranchSummaryDto, PointDto, GeoJson*
│   ├── Businesses/             # PublicBusinessDto, PublicBranchDto, CatalogRefDto, PhotoDto, etc.
│   └── Management/             # ManagedBusinessDto, BranchInputDto, BusinessInputDto, etc.
├── Services/
│   ├── ICategoryService.cs     # Contrato del servicio de categorías
│   ├── CategoryService.cs      # Implementación del servicio de categorías
│   ├── IBranchSearchService.cs # Contrato de búsqueda pública y fichas
│   ├── BranchSearchService.cs  # Implementación espacial con NetTopologySuite
│   ├── IBusinessService.cs     # Contrato de administración de agregados
│   └── BusinessService.cs      # Implementación de negocio, sucursales y fotos
├── Data/
│   ├── AppDbContext.cs         # DbContext principal derivado de IdentityDbContext
│   └── Migrations/             # Scripts SQL de soporte
├── Identity/
│   ├── ApplicationUser.cs      # Usuario Identity con clave Guid
│   └── ApplicationRole.cs      # Rol Identity con clave Guid
├── Models/                     # Entidades del catálogo, negocio y esquema espacial
├── Middleware/
│   └── ApiExceptionHandler.cs  # Manejador global de excepciones (ProblemDetails)
├── Dockerfile                  # Imagen Docker optimizada
└── .dockerignore
```

---

## 4. Configuración y Secretos

| Clave | Descripción |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Cadena de conexión Npgsql a PostgreSQL. |
| `Jwt:Issuer` | Emisor del token JWT (`directorio-geo-api`). |
| `Jwt:Audience` | Audiencia del token JWT (`directorio-geo-client`). |
| `Jwt:AccessTokenMinutes` | 15 minutos de validez para el token de acceso. |
| `Jwt:RefreshTokenDays` | 30 días de validez para el token de refresco. |
| `ASPNETCORE_HTTP_PORTS` | Puerto expuesto en contenedor (`8080`). |

Para configurar la base de datos localmente mediante User Secrets:

```powershell
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Host=localhost;Port=5432;Database=directorio;Username=directorio;Password=<contraseña-local>' --project backend/backend.csproj
```

---

## 5. Compilación y Ejecución Local

Desde la **raíz del repositorio**:

```powershell
# Restaurar paquetes NuGet y compilar
dotnet restore backend/backend.slnx
dotnet build backend/backend.slnx --configuration Release

# Ejecutar el backend con perfil HTTP (puerto local 5089)
dotnet run --project backend/backend.csproj --launch-profile http
```

### URLs de Desarrollo (Perfil HTTP)
- **Categorías:** http://localhost:5089/api/v1/categories
- **Prueba:** http://localhost:5089/api/v1/Prueba
- **OpenAPI JSON:** http://localhost:5089/openapi/v1.json
- **Scalar UI:** http://localhost:5089/scalar/v1

---

## 6. Ejecución con Docker Compose

```powershell
# Levantar base de datos y backend
docker compose up -d --build db backend

# Ver registros en tiempo real
docker compose logs -f backend
```
El contenedor publica el backend en el puerto configurado en `.env` (por defecto `http://localhost:8000`).
