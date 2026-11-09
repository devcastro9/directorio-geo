# Backend — Directorio Geo

API ASP.NET Core 10 del directorio de negocios de Bolivia. Este README describe el estado del código del backend y cómo trabajar con él. El alcance y los contratos que deben implementarse están en [REQUERIMIENTOS_API.md](../REQUERIMIENTOS_API.md); las funciones del producto, en [Funcionalidades](../Funcionalidades_Directorio_Geo_SQL_v1_3.md). Para la instalación completa, consultar el [README principal](../README.md); la integración de la interfaz se documenta en el [README del frontend](../frontend/README.md).

## Estado actual

La única operación con implementación es `GET /api/v1/Prueba`: consulta `catalog.parameter_definition` y devuelve una lista de entidades `ParameterDefinition`. Es una prueba de acceso a datos, no el contrato público definitivo.

| Componente | Estado comprobado en el código |
| --- | --- |
| PostgreSQL y EF Core | `AppDbContext` registrado con Npgsql y NetTopologySuite; timeout de comandos de 30 segundos |
| Identity | Modelos `ApplicationUser` y `ApplicationRole` con claves Guid; contexto derivado de `IdentityDbContext` |
| Autenticación | `IAuthService` en desarrollo y `AuthService` vacío; sin configuración de Identity/JWT en el arranque |
| Controladores | `AuthController`, `BusinessesController` y `BranchesController` sin acciones HTTP |
| DTO | Clases de negocio y errores vacías; registro/login con definiciones provisionales |
| Errores | `ExceptionHandlingMiddleware` vacío y sin registrar |
| Trabajos programados | Tres clases vacías en `BackgroundJobs`; sin ejecución ni planificador |
| CORS | Permite cualquier origen, cabecera y método; sin credenciales habilitadas |
| OpenAPI y Scalar | Se habilitan exclusivamente en `Development` |
| Salud y pruebas .NET | Sin endpoints de salud ni proyectos de pruebas |

Los atributos de ruta de un controlador vacío no implementan endpoints. Los controladores de negocio y sucursales declaran actualmente `api/[controller]`, mientras que el contrato objetivo exige `/api/v1`. Todavía no existen las 27 operaciones del MVP ni las cuatro adicionales de propietarios.

Hay bloqueos de compilación visibles por inspección: `AuthService` no implementa los métodos de `IAuthService`, y no se encuentran definiciones de `UserResponse` ni `AuthSession`. Además, la interfaz importa `Microsoft.AspNetCore.Identity.Data`, por lo que sus nombres `RegisterRequest` y `LoginRequest` corresponden a los tipos del framework y no a los DTO propios de `backend.DTOs`. Completar y alinear esta parte antes de esperar un arranque correcto. Esta sincronización documental no ejecuta compilación ni corrige código.

## Stack que se conserva

| Elemento | Configuración del proyecto |
| --- | --- |
| Framework | C#, ASP.NET Core 10, `net10.0` |
| EF Core, Identity, JWT y OpenAPI | Paquetes Microsoft 10.0.14 |
| Proveedor PostgreSQL y soporte espacial | Npgsql EF Core 10.0.3 y NetTopologySuite |
| Documentación interactiva | Scalar.AspNetCore 2.19.0 |
| Persistencia | PostgreSQL 18 y PostGIS 3.6; Database First |
| Fotografías | BYTEA en PostgreSQL; entrega binaria por la API a implementar |
| Contenedores | Imágenes SDK/ASP.NET 10.0; ejecución Linux como usuario de aplicación |

El proyecto tiene nullable habilitado e implicit usings deshabilitado. [backend.slnx](backend.slnx) contiene únicamente [backend.csproj](backend.csproj). No sustituir el stack ni agregar módulos por la sola existencia de tablas SQL.

## Estructura existente

```text
backend/
  backend.csproj
  backend.slnx
  Program.cs
  appsettings.json
  appsettings.Development.json
  Properties/launchSettings.json
  Controllers/             # Prueba y controladores vacíos de Auth/Businesses/Branches
  DTOs/                    # Contratos provisionales y clases vacías
  Services/                # IAuthService y AuthService incompleto
  Data/
    AppDbContext.cs        # Contexto Database First con Identity Guid
    Migrations/
      IdentityMigration.sql # Script existente; no aplicar sobre la línea base
  Identity/                # ApplicationUser y ApplicationRole
  Models/                  # Entidades y vistas del esquema existente
  Middleware/              # ExceptionHandlingMiddleware vacío
  BackgroundJobs/          # ArchiveEvents/ConsolidateMetrics/ExpireAds vacíos
  Dockerfile
  .dockerignore
```

`Program.cs` registra contexto, servicio de autenticación, controladores, CORS y OpenAPI. No configura `AddAuthentication`, servicios Identity, `UseAuthentication` ni autorización. La redirección HTTPS y `UseAuthorization` están comentadas. Heredar de `IdentityDbContext` y tener paquetes instalados no activa la seguridad HTTP.

En `Development` están habilitados `EnableSensitiveDataLogging` y errores detallados de EF. Mantener ese entorno restringido al desarrollo.

## Configuración

| Clave | Estado y uso |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Vacía en appsettings; obligatoria para consultar PostgreSQL |
| `Jwt:Issuer` | `directorio-geo-api`; declarada, aún sin consumo por autenticación |
| `Jwt:Audience` | `directorio-geo-client`; declarada, aún sin consumo por autenticación |
| `Jwt:AccessTokenMinutes` | 15; declarada, pendiente de implementación |
| `Jwt:RefreshTokenDays` | 30; declarada, pendiente de implementación |
| `ASPNETCORE_ENVIRONMENT` | Determina OpenAPI/Scalar y diagnóstico de desarrollo |
| `ASPNETCORE_HTTP_PORTS` | 8080 en contenedor |

El backend nativo no carga el `.env` de Docker Compose. Configurar la conexión mediante User Secrets o `ConnectionStrings__DefaultConnection`. La futura clave `Jwt:SigningKey` y los orígenes permitidos deberán incorporarse siguiendo el contrato API; no existe aún su configuración operativa.

## Ejecución local

Todos los comandos de este documento se ejecutan desde la **raíz del repositorio**, salvo indicación expresa. Requisitos: SDK .NET 10 y PostgreSQL 18/PostGIS con el esquema inicializado. Para mantener la base en Docker, preparar `.env` según el README principal y ejecutar:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d db --wait
```

Configurar la conexión local, sustituyendo los datos por los de `.env` y el puerto publicado:

```powershell
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Host=localhost;Port=5432;Database=directorio;Username=directorio;Password=<contraseña-local>' --project backend/backend.csproj
dotnet restore backend/backend.slnx
dotnet build backend/backend.slnx --configuration Release
dotnet run --project backend/backend.csproj --launch-profile http
```

Estos comandos de compilación y arranque serán utilizables al resolver los bloqueos del servicio de autenticación descritos arriba. No representan una ejecución validada del estado actual.

| Perfil | Dirección configurada |
| --- | --- |
| `http` | http://localhost:5089 |
| `https` | https://localhost:7273 y http://localhost:5089 |

Ambos perfiles locales usan `Development`. El perfil HTTPS requiere certificado de desarrollo local. Tras un arranque correcto con perfil HTTP:

- Prueba de datos: http://localhost:5089/api/v1/Prueba.
- OpenAPI: http://localhost:5089/openapi/v1.json.
- Scalar: http://localhost:5089/scalar/v1.

## Ejecución con Docker Compose

```powershell
docker compose config --quiet
docker compose up -d --build db backend
docker compose logs --tail 100 backend
```

El archivo base usa `Production`, conecta a `db:5432` y publica la API en loopback, puerto 8000 por defecto. OpenAPI/Scalar no se habilitan en ese entorno. La construcción del backend también depende de resolver los errores de compilación actuales.

Para habilitar documentación de desarrollo y publicar PostgreSQL localmente:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build db backend
```

Con el puerto predeterminado, API en http://localhost:8000/api/v1/Prueba, OpenAPI en http://localhost:8000/openapi/v1.json y Scalar en http://localhost:8000/scalar/v1. El override usa imágenes compiladas y no ofrece hot reload. Usar los mismos archivos `-f` en operaciones posteriores de ese entorno.

[Dockerfile](Dockerfile) usa contexto `backend/`, restauración, compilación y publicación en etapas separadas. La imagen final ejecuta `dotnet backend.dll` en el puerto 8080 como usuario de aplicación y zona `America/La_Paz`. [.dockerignore](.dockerignore) excluye artefactos y archivos locales de secretos.

Compose espera `pg_isready` de PostgreSQL; eso no verifica esquema ni credenciales de la API. No existe healthcheck del backend configurado. El despliegue actual no incorpora proxy HTTPS.

## SQL, modelos y migraciones

La fuente de verdad es [db/init/01_schema.sql](../db/init/01_schema.sql). Contiene esquemas de dominio, funciones, triggers, índices y tablas Identity. Se ejecuta automáticamente solo al inicializar un volumen vacío. [db/init/02_test.sql](../db/init/02_test.sql) contiene pruebas SQL con rollback.

**No aplicar [IdentityMigration.sql](Data/Migrations/IdentityMigration.sql) sobre esa línea base:** también crea tablas del dominio y produciría colisiones. No hay migraciones C# ni snapshot versionados que hagan reproducible `dotnet ef database update`. El arranque actual no ejecuta `EnsureCreated` ni `Migrate`.

La política acordada para continuar es SQL incremental numerado en `db/migrations/`, con versión y checksum; esa infraestructura sigue pendiente. Los scripts se aplicarán durante mantenimiento, no al arrancar la API. No eliminar volúmenes ni volver a ejecutar la línea base para actualizar una instalación existente.

`AppDbContext` llama a `base.OnModelCreating` e incluye `OnModelCreatingPartial`. Preservar Identity y personalizaciones al regenerar modelos. La sección 12 del [contrato API](../REQUERIMIENTOS_API.md) define las correcciones pendientes de revisión de agregado, principales únicos, horarios, refresh tokens y consultas espaciales.

## Continuación del desarrollo

1. Completar autenticación y DTO propios. Alinear login por email, registro, salidas `User`/`AuthSession`, refresh y errores con el contrato; los DTO provisionales actuales no lo cumplen.
2. Implementar la base P0: permisos Identity/JWT, sesiones revocables, CORS explícito, ProblemDetails, límites, auditoría, concurrencia y salud; aplicar las correcciones SQL necesarias.
3. Implementar las 27 operaciones MVP del contrato: catálogo público, búsqueda JSON/GeoJSON, fichas y administración de negocios, sucursales y fotos. Unificar rutas bajo `/api/v1` y retirar `/Prueba` cuando existan las operaciones reales.
4. Añadir pruebas de integración con PostgreSQL/PostGIS real y los criterios de aceptación del contrato. No hay proyectos de pruebas automatizadas versionados para este backend; esta actualización no ejecuta compilación ni pruebas de aplicación.
5. Incorporar las cuatro operaciones P2 de registro/asociación de propietarios una vez estable el MVP, reutilizando las rutas de gestión con autorización por negocio.

Los modelos de reseñas, favoritos, publicidad, importaciones y métricas reflejan el esquema existente; no implican funcionalidades comprometidas. Las clases vacías de `BackgroundJobs` permanecen fuera de P0/P1/P2: no agregar planificadores para completarlas sin una ampliación explícita del alcance.

## Integración con Nuxt

El frontend actual muestra la bienvenida y no realiza solicitudes a la API. No hay que configurar variables de API para ejecutarlo en ese estado. La URL local del backend nativo es `http://localhost:5089`; dentro de Compose el backend escucha en `http://backend:8080`, mientras que el navegador accede al puerto publicado del host. La red predeterminada permite la comunicación entre frontend y backend; el frontend no tiene acceso directo a la red interna de PostgreSQL.

La integración prevista usa `/api/v1` como base pública bajo un origen HTTPS y una base privada para SSR. Esas opciones aún no están implementadas en Nuxt, y Compose no configura el proxy. Al conectar sesiones de navegador, reemplazar CORS abierto por orígenes explícitos con credenciales y mantener refresh en cookie HttpOnly; la cookie no sustituye Bearer en rutas privadas. Seguir las convenciones de JSON/GeoJSON, ETag y ProblemDetails del contrato.

## Comprobaciones de desarrollo

Desde la raíz del repositorio:

```powershell
dotnet build backend/backend.slnx --configuration Release
git diff --check
```

La compilación permite identificar los bloqueos actuales; no se presenta como una validación aprobada. Las pruebas SQL existentes pueden ejecutarse contra una base de desarrollo con el esquema inicializado:

```powershell
Get-Content -LiteralPath db/init/02_test.sql -Raw -Encoding UTF8 | docker compose exec -T db sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

Usar la misma combinación de archivos Compose con que se inició el entorno. La prueba SQL termina con rollback y no sustituye pruebas HTTP de permisos, sesiones, contratos o concurrencia. Un estado saludable de PostgreSQL tampoco demuestra que `/api/v1/Prueba` pueda ejecutarse con las credenciales de la aplicación.
