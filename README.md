# directorio-geo

Directorio geoespacial de negocios para Bolivia. El repositorio contiene un esquema PostgreSQL con negocios, sucursales, catálogos, territorio, reseñas y métricas; una API ASP.NET Core; y un frontend Nuxt inicial.

La API expone actualmente `GET /api/v1/Prueba`, que consulta `catalog.parameter_definition`. El frontend muestra `NuxtWelcome`: todavía no consume la API. Identity y los paquetes JWT están presentes, pero `Program.cs` no configura autenticación. No se debe interpretar esta base técnica como un producto terminado ni como una API lista para exposición pública.

## Arquitectura y versiones

| Componente | Configuración real |
| --- | --- |
| Backend | .NET 10 (`net10.0`), ASP.NET Core, EF Core 10.0.14 |
| Persistencia | Npgsql 10.0.3 y NetTopologySuite; `AppDbContext` hereda de IdentityDbContext |
| Frontend | Nuxt 4.6.4, Vue 3.5.45, Vue Router 5.4.0 según lockfile |
| Build frontend | pnpm 12.16.1, Nitro `node-server`, Node 22 Alpine en Docker |
| Base | PostgreSQL 18 / PostGIS 3.6 (`postgis/postgis:18-3.6`) |
| Herramientas DB | pgRouting, osm2pgrouting y osmium-tool conservados en `db/Dockerfile` |
| Administración opcional | pgAdmin 9.18.0, perfil Compose `tools` |

Compose usa `db:5432` desde el backend. La red `database` es interna; el frontend no pertenece a ella. Backend y pgAdmin también tienen acceso a la red predeterminada. El frontend no depende del arranque de la API porque actualmente no la utiliza.

El backend espera a `pg_isready`. Este chequeo verifica que PostgreSQL acepte conexiones, no valida el esquema ni las credenciales de la API. El frontend comprueba HTTP `/` con Node. No se declara un healthcheck de backend: no existe un endpoint de salud independiente de los datos y la imagen ASP.NET no necesita herramientas HTTP adicionales.

## Requisitos

- Docker con contenedores Linux y Docker Compose v2 o posterior.
- Para ejecución nativa: SDK .NET 10, Node compatible con `frontend/package.json` y pnpm 12.16.1.
- El lockfile requiere al menos Node 22.22.3 en la rama 22; también admite 24.15+ y 26+. Docker usa la rama 22 mantenida. Git y PowerShell para los ejemplos siguientes.

Las imágenes locales se etiquetan como `geodirectorio-backend:net10`, `geodirectorio-frontend:nuxt4` y `geodirectorio-db:pg18-postgis3.6`. Las etiquetas de imágenes fijan líneas de versión, pero no digests: las reconstrucciones pueden incorporar parches. Para publicaciones inmutables, fijar los digests comprobados en el entorno de entrega y guardar las imágenes construidas.

## Estructura final propuesta

Esta es la estructura objetivo para desarrollar los requisitos de [REQUERIMIENTOS_API.txt](REQUERIMIENTOS_API.txt) y las capacidades descritas en [Funcionalidades_Directorio_Geo_SQL_v1_3.txt](Funcionalidades_Directorio_Geo_SQL_v1_3.txt). Los nombres nuevos son una propuesta de organización; su aparición en este árbol no indica que estén implementados. Esta actualización documenta la estructura y no crea ni mueve los archivos de la aplicación.

Se mantiene un monolito modular ASP.NET Core 10 con un proyecto de aplicación, Nuxt 4 con Vue 3 y TypeScript, y PostgreSQL 18 con PostGIS. Los proyectos de pruebas son independientes y no constituyen servicios adicionales. No se necesita una capa Repository por tabla.

Los documentos difieren en la prioridad de usuarios, propietarios, importaciones y publicidad. Para esta propuesta se adopta el orden final de la sección 13 de `REQUERIMIENTOS_API.txt`: **P0** base técnica y seguridad; **P1** catálogo público, administración, autenticación y propietarios; **P2** favoritos, reseñas, métricas, importaciones, publicidad y autocompletado avanzado. P2 forma parte de la estructura objetivo, pero no bloquea la primera salida. Las capacidades SQL marcadas [S] en el documento funcional requieren API, interfaz y pruebas antes de estar disponibles.

```text
directorio-geo/
├── README.md                          # [E] Qué es, cómo levantarlo, variables, problemas frecuentes
├── .gitignore                         # [E]
├── .env.example                       # [E] Plantilla de Compose; sin secretos reales
├── docker-compose.yml                 # [E] Base Production: db, backend, frontend; solo loopback
├── docker-compose.dev.yml             # [E] Abre puerto de DB y habilita OpenAPI/Scalar
├── docs/
│   └── architecture.md                # [N] Estructura objetivo, decisiones y hoja de ruta (P0/P1/backlog)
├── .github/workflows/
│   └── ci.yml                         # [N] build .NET + build Nuxt + 02_test.sql contra postgis:18-3.6
│
├── backend/
│   ├── backend.slnx                   # [E] Solución (se agrega tests/ cuando existan)
│   ├── backend.csproj                 # [E] net10.0, EF Core, Npgsql, NetTopologySuite, Identity
│   ├── Program.cs                     # [E→N] Hoy solo /Prueba; agregar auth, CORS, rate limiter,
│   │                                  #       OutputCache, health checks y ProblemDetails
│   ├── appsettings.json               # [E] Sin secretos; Jwt:* solo si se elige JWT
│   ├── appsettings.Development.json   # [E]
│   ├── Dockerfile                     # [E] Contexto de build: backend/
│   ├── .dockerignore                  # [E]
│   ├── Properties/launchSettings.json # [E] Perfiles http y https
│   │
│   ├── Controllers/                   # Solo rutas /api/v1; sin reglas de negocio
│   │   ├── PruebaController.cs        # [E] Retirar cuando existan endpoints reales
│   │   ├── AuthController.cs          # [N] Registro, login, logout, me (+ refresh si JWT)
│   │   ├── BranchesController.cs      # [N] Búsqueda por texto, radio y bbox; detalle de sucursal
│   │   ├── BusinessesController.cs    # [N] Detalle por slug; alta y edición (con policy de propietario/admin)
│   │   ├── CatalogsController.cs      # [N] Lectura de categorías, servicios, medios de pago y territorios
│   │   ├── PhotosController.cs        # [N] GET binario (ETag/Cache-Control) y carga validada
│   │   ├── OwnerController.cs         # [N] Solicitar verificación, ver mis negocios y su estado
│   │   └── AdminController.cs         # [N] Aprobar/rechazar verificaciones, baja/restauración,
│   │                                  #     auditoría y parámetros
│   │
│   ├── Data/
│   │   ├── AppDbContext.cs            # [E] Generado del SQL; hereda de IdentityDbContext (un solo contexto)
│   │   ├── AppDbContext.Extensions.cs # [N] Clase partial con personalizaciones (no se pierden al re-scaffold)
│   │   └── Queries/
│   │       ├── BranchSearchQueries.cs # [N] Proyecciones y consultas espaciales (ST_DWithin, bbox, trigram)
│   │       └── BusinessQueries.cs     # [N] Ficha con sucursales, horarios y servicios
│   │
│   ├── Models/                        # [E] Entidades del esquema (ingeniería inversa)
│   │   ├── Business.cs
│   │   ├── Branch.cs
│   │   ├── BranchPhoto.cs
│   │   ├── OwnerIdentity.cs
│   │   ├── BusinessOwner.cs
│   │   ├── Territory.cs
│   │   └── ...                        # Resto de tablas del esquema
│   │
│   ├── DTOs/                          # Records C#; nunca se devuelven entidades EF ni el NIT en público
│   │   ├── CommonDtos.cs              # [N] PagedResult<T>, Coordinates, ErrorResponse
│   │   ├── AuthDtos.cs                # [N] RegisterRequest, LoginRequest, UserResponse
│   │   ├── BranchDtos.cs              # [N] BranchSummaryDto, BranchDetailDto, SearchRequest
│   │   ├── BusinessDtos.cs            # [N] BusinessDetailDto, CreateBusinessRequest, UpdateBusinessRequest
│   │   ├── CatalogDtos.cs             # [N] CategoryDto, TerritoryDto, ServiceDto
│   │   ├── OwnerDtos.cs               # [N] VerificationRequest, OwnedBusinessDto
│   │   └── AdminDtos.cs               # [N] VerificationDecisionRequest, AuditLogDto
│   │
│   ├── Services/                      # Solo donde hay lógica o transacción real
│   │   ├── AuthService.cs             # [N] Alta de usuario, login, sesión (y TokenService si JWT)
│   │   ├── BusinessService.cs         # [N] Alta atómica negocio + primera sucursal; publicar; baja lógica
│   │   ├── SearchService.cs           # [N] Orquesta texto/radio/bbox/territorio y valida parámetros
│   │   ├── OwnershipService.cs        # [N] Verificación, asociación vigente y revocación
│   │   ├── PhotoService.cs            # [N] Valida MIME y tamaño, define foto principal, redimensiona
│   │   └── AuditService.cs            # [N] Registra quién cambió qué (se llama desde los otros servicios)
│   │
│   ├── Identity/
│   │   ├── ApplicationUser.cs         # [E] Mover desde Models/
│   │   ├── ApplicationRole.cs         # [E] Mover desde Models/; roles: Admin, BusinessOwner, User
│   │   ├── Policies.cs                # [N] Constantes de políticas y handler "es propietario vigente"
│   │   └── AdminBootstrapper.cs       # [N] Alta inicial del administrador desde configuración segura
│   │
│   └── Middleware/
│       └── ExceptionHandlingMiddleware.cs  # [N] Errores → ProblemDetails con traceId
│
├── frontend/
│   ├── nuxt.config.ts                 # [E] Agregar runtimeConfig.public.apiBase (sin secretos)
│   ├── tsconfig.json                  # [E]
│   ├── package.json                   # [E]
│   ├── pnpm-lock.yaml                 # [E] No eliminar
│   ├── pnpm-workspace.yaml            # [E]
│   ├── Dockerfile                     # [E] Node 22 Alpine, Nitro node-server
│   ├── .dockerignore                  # [E]
│   ├── public/                        # favicon.ico, robots.txt (no fotos: salen de la API)
│   └── app/
│       ├── app.vue                    # [E→N] Reemplazar NuxtWelcome por NuxtLayout + NuxtPage
│       ├── error.vue                  # [N] Página de error global
│       ├── layouts/
│       │   ├── default.vue            # [N] Cabecera con buscador y acceso/cuenta
│       │   └── admin.vue              # [N] Navegación del panel de administración
│       ├── pages/
│       │   ├── index.vue              # [N] Buscador, filtros, lista y mapa
│       │   ├── businesses/[slug].vue  # [N] Ficha del negocio y sus sucursales
│       │   ├── login.vue              # [N]
│       │   ├── register.vue           # [N]
│       │   ├── owner/
│       │   │   ├── index.vue          # [N] Mis negocios y estado de verificación
│       │   │   ├── verification.vue   # [N] Solicitud de verificación
│       │   │   └── businesses/
│       │   │       ├── new.vue        # [N] Alta de negocio con primera sucursal
│       │   │       └── [id].vue       # [N] Edición, sucursales y fotos
│       │   └── admin/
│       │       ├── index.vue          # [N] Pendientes de revisión
│       │       ├── businesses.vue     # [N] Listado, baja y restauración
│       │       ├── verifications.vue  # [N] Aprobar/rechazar propietarios
│       │       └── audit-logs.vue     # [N] Consulta de auditoría
│       ├── components/
│       │   ├── BusinessCard.vue       # [N] Tarjeta de resultado
│       │   ├── BranchMap.client.vue   # [N] Mapa (solo navegador)
│       │   ├── SearchFilters.vue      # [N] Categoría, territorio, radio, "abierto ahora"
│       │   ├── OpeningHours.vue       # [N] Horarios y estado abierto/cerrado
│       │   └── PhotoGallery.vue       # [N]
│       ├── composables/
│       │   ├── useApi.ts              # [N] Base URL, manejo de errores y credenciales
│       │   ├── useAuth.ts             # [N] Estado de sesión y roles
│       │   ├── useSearch.ts           # [N] Parámetros de búsqueda sincronizados con la URL
│       │   └── useGeolocation.ts      # [N] Permiso explícito del navegador
│       ├── middleware/
│       │   ├── auth.ts                # [N] Redirige a login si no hay sesión
│       │   └── admin.ts               # [N] Bloquea rutas /admin sin rol (la API vuelve a comprobar)
│       └── types/
│           └── api.ts                 # [N] Tipos alineados con el OpenAPI
│
├── db/
│   ├── Dockerfile                     # [E→N] PostGIS 18-3.6; quitar pgRouting/osm2pgrouting/osmium
│   ├── init/                          # Solo se ejecuta en volumen vacío
│   │   ├── 01_schema.sql              # [E] Esquema app/catalog/geo, funciones, triggers, índices
│   │   ├── 02_test.sql                # [E] Pruebas con ROLLBACK (también las usa CI)
│   │   └── 03_roles.sql               # [N] Rol de aplicación con permisos mínimos
│   └── migrations/                    # SQL incremental aplicado con DbUp o Flyway
│       ├── V001__identity_schema.sql  # [N] Tablas de Identity (generadas una vez con dotnet ef script)
│       ├── V002__refresh_tokens.sql   # [N] Solo si se elige JWT
│       ├── V003__owner_verification.sql # [N] Tabla de solicitudes de verificación
│       └── V004__seed_catalogs.sql    # [N] Categorías, servicios y territorios iniciales con procedencia
│
└── tests/
    └── backend.IntegrationTests/
        ├── backend.IntegrationTests.csproj  # [N] xUnit + Testcontainers (postgis:18-3.6)
        ├── Fixtures/
        │   └── PostgisFixture.cs      # [N] Levanta la DB con 01_schema.sql y migraciones
        ├── SearchTests.cs             # [N] Radio en metros, bbox, territorios y horarios
        ├── BusinessCreationTests.cs   # [N] Atomicidad negocio+sucursal, concurrencia, baja lógica
        └── AuthorizationTests.cs      # [N] Roles, propiedad por negocio y endpoints /admin
```

Las carpetas terminadas en `/` sin archivos enumerados agrupan futuras implementaciones; `...` representa las entidades ya existentes que se conservan. No es necesario crear directorios vacíos. `stores/` con Pinia solo se incorporará si aparece una necesidad de estado compartido que los composables no cubran.

La organización dentro de `frontend/app/` adapta la estructura orientativa del documento API a la base Nuxt 4 del repositorio. Los middleware del frontend orientan la navegación; la API debe volver a comprobar permisos en cada operación. La interfaz ofrece filtros comprensibles, ubicación y mapa; las coordenadas y bbox se gestionan internamente. Las indicaciones de llegada se abren en un proveedor externo.

La cobertura funcional queda distribuida así:

| Requisito | Ubicación principal | Prioridad adoptada |
| --- | --- | --- |
| Búsqueda de sucursales, mapa, categorías, territorios y fichas | Controllers/Public, Services/Search, Data/Queries; pages y components públicos | P1 |
| Horarios, festivos, contactos, servicios, pagos y precios | Services/Branches, Models y funciones SQL; OpeningHours y ficha | P1 |
| Alta atómica, publicación, baja lógica, restauración y auditoría | Controllers/Admin, Services/Businesses, Services/Audit y SQL | P0/P1 |
| Identity Guid, JWT, refresh rotativo y administrador inicial | Identity, Services/Auth, Authorization y migraciones Identity | P0/P1 |
| Verificación y asociación vigente de propietarios | Services/Ownership, handlers, paneles owner/admin | P1; cerrar persistencia y flujo |
| Fotos BYTEA con carga validada, principal y lectura HTTP | Services/Photos, controllers públicos/privados y PhotoGallery | P1 |
| Favoritos, reseñas múltiples, respuestas y moderación posterior | Controllers/Account, Owner y Admin; Services/Favorites y Reviews | P2 |
| Eventos, métricas, archivo y publicidad de período fijo | Services/Metrics y Ads, BackgroundJobs y paneles | P2 |
| CSV/XLSX, previsualización, duplicados y aplicación idempotente | Services/Imports, AdminImportsController y panel admin | P2 |
| Seguridad, contratos e integridad espacial/transaccional | tests/backend.IntegrationTests, db/tests y frontend/tests | P0/P1; ampliar al activar P2 |

Para llegar desde la base actual a esta estructura deben resolverse estos puntos:

1. **Separar dominio e Identity.** Actualmente `AppDbContext` hereda de IdentityDbContext. El objetivo es que el contexto del dominio sea Database First y que `Data/IdentityDbContext.cs` gestione exclusivamente Identity y sesiones. Mover `ApplicationUser` y `ApplicationRole` desde `Models/` a `Identity/`, ajustando namespaces y referencias. Definir baseline y esquema de Identity antes de generar sus migraciones.
2. **Sustituir el SQL mixto de Identity.** El actual `Data/Migrations/IdentityMigration.sql` mezcla tablas del dominio e Identity; no forma parte del mecanismo final. Reemplazarlo por migraciones aisladas solo después de revisar equivalencia y estado de la base. No ejecutarlo junto a `db/init/01_schema.sql`. Las funciones, triggers, EXCLUDE e índices GIN/GiST se mantienen mediante SQL del dominio; no usar `EnsureCreated` ni `Migrate` automático sobre `app/catalog/geo`.
3. **Agregar persistencia explícita.** `RefreshToken.cs` es un modelo propuesto que requiere migración, hash y control de familias de sesión. Las solicitudes de verificación requieren tabla y contrato propios si se elige un flujo de autoservicio; `owner_identity` no los reemplaza. Resolver las referencias de usuario TEXT/Guid antes de crear relaciones nuevas. No asumir que estas tablas ya existen.
4. **Cerrar decisiones y contratos.** Registrar D-01..D-08 del documento API: altas no verificadas, evidencias, titularidad, límites espaciales, edición de reseñas, IDs de usuario, planificador y retención de eventos. Los endpoints de mantenimiento de catálogos, territorios, parámetros, recuperación de cuenta, sugerencias y registro de eventos necesitan contratos adicionales antes de implementarse. La existencia de una carpeta no cierra esas decisiones.
5. **Completar configuración y operación.** Definir JWT, validación de usuarios activos, políticas por negocio, rate limiting, CORS, límites de archivos, ProblemDetails y salud HTTP. Usar una cuenta DB de aplicación con permisos mínimos, secretos externos y HTTPS en despliegue. Los jobs P2 requieren un planificador acordado y protección contra ejecución simultánea; no implican un nuevo microservicio.
6. **Integrar pruebas y entrega.** Agregar los proyectos de `tests/` a `backend.slnx` y ajustar el Dockerfile si la restauración usa esa solución: el contexto actual de build es `backend/`, mientras que las pruebas propuestas quedan fuera. Retirar `PruebaController.cs` al disponer de endpoints reales y salud independiente. Eliminar `NuxtWelcome` al conectar el frontend. Versionar OpenAPI y ejecutar las pruebas con PostgreSQL/PostGIS real antes de publicar.

Para nuevas instalaciones, `init/` construye el esquema base y los cambios posteriores se aplican en orden mediante el proceso de migración documentado. En bases existentes se aplican únicamente los incrementos pendientes; `seeds/`, `migrations/` y `scripts/` no se ejecutan por el solo hecho de existir. Los datos territoriales deben conservar procedencia y geometrías válidas; sin límites confiables, una ubicación se marca para revisión.

Quedan fuera de esta estructura inicial las promociones y productos P3, mapas de calor, análisis estadísticos avanzados, polígonos dibujados por visitantes, motor propio de rutas y arquitectura distribuida. El resto del README describe el funcionamiento **actual** del repositorio, hasta implementar esta propuesta.

## Variables y secretos

Si `.env` todavía no existe:

```powershell
Copy-Item .env.example .env
```

Completar las contraseñas localmente antes de arrancar. No sobrescribir un `.env` existente. `.env` se utiliza para interpolar Compose; no se envía entero a los servicios y no se incorpora a las imágenes.

| Variable de `.env` | Uso | Predeterminado / requisito |
| --- | --- | --- |
| `POSTGRES_DB` | Nombre de base | Obligatoria; ejemplo `directorio` |
| `POSTGRES_USER` | Usuario inicial PostgreSQL | Obligatoria; ejemplo `directorio` |
| `POSTGRES_PASSWORD` | Contraseña DB y conexión backend | Obligatoria, sin valor predeterminado |
| `PGADMIN_DEFAULT_EMAIL` | Acceso pgAdmin | `admin@example.com` |
| `PGADMIN_DEFAULT_PASSWORD` | Acceso pgAdmin | Obligatoria al iniciar `tools`; vacía impide iniciar pgAdmin |
| `BACKEND_PORT` | Puerto HTTP del host | `8000` |
| `FRONTEND_PORT` | Puerto HTTP del host | `3000` |
| `PGADMIN_PORT` | Puerto del host | `5050` |
| `POSTGRES_PORT` | Puerto del host, solo override dev | `5432` |

Compose construye `ConnectionStrings__DefaultConnection` con host `db`. La contraseña se entrecomilla para admitir punto y coma; si contiene comillas dobles, duplicarlas para la sintaxis Npgsql o suministrar la cadena completa desde la configuración segura de despliegue. Para caracteres `$` en `.env`, usar valores entre comillas simples y comprobar la interpolación sin imprimir secretos (`docker compose config --quiet`).

El backend usa también `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_HTTP_PORTS` y `TZ`; `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes` y `Jwt:RefreshTokenDays` están declarados en appsettings pero aún no se consumen por autenticación. Su cadena de conexión compartida está vacía: debe suministrarse mediante variables o User Secrets.

Nuxt no tiene `runtimeConfig` ni variables públicas de API declaradas. El Dockerfile fija `NITRO_PRESET=node-server` durante el build; en ejecución define `NODE_ENV=production`, `NITRO_HOST=0.0.0.0` y `NITRO_PORT=3000`. No hay argumentos de build con secretos. Las futuras variables `NUXT_PUBLIC_*` serán visibles al navegador; nunca colocar credenciales en ellas.

## Docker Compose

Desde la raíz, validar sin mostrar contraseñas y construir:

```powershell
docker compose config --quiet
docker compose build
docker compose up -d --wait
```

El archivo base publica únicamente backend y frontend en loopback. PostgreSQL no publica ningún puerto. API: http://localhost:8000/api/v1/Prueba. Frontend: http://localhost:3000. OpenAPI y Scalar solo se habilitan en Development.

Para desarrollo con PostgreSQL accesible desde Windows y documentación de API:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml config --quiet
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build --wait
```

API nativa del contenedor: http://localhost:8000; OpenAPI: http://localhost:8000/openapi/v1.json; Scalar: http://localhost:8000/scalar/v1. Este override sigue utilizando builds de producción y no proporciona hot reload.

pgAdmin es opcional. Completar su contraseña y ejecutar:

```powershell
docker compose --profile tools up -d pgadmin
```

Abrir http://localhost:5050 y registrar host `db`, puerto `5432` y las credenciales DB locales. Desde Windows, con el override dev, usar `localhost` y `POSTGRES_PORT`.

```powershell
docker compose ps
docker compose logs -f backend frontend
docker compose logs --tail 100 db
docker compose stop
docker compose down
```

Usar la misma combinación de archivos `-f` para las operaciones posteriores en desarrollo. `down` conserva `pgdata` y `pgadmin_data`. No eliminar volúmenes para aplicar cambios de esquema. Se conserva el nombre de proyecto `geodirectorio` y el montaje PostgreSQL 18 `/var/lib/postgresql` para mantener los volúmenes existentes.

## PostgreSQL, SQL y Entity Framework

En un volumen vacío, el entrypoint ejecuta `db/init/01_schema.sql` y después `02_test.sql`. Las pruebas terminan con `ROLLBACK`. El esquema requiere PostgreSQL 18 (`uuidv7`) y las extensiones PostGIS, pg_trgm, unaccent y btree_gist. Las herramientas de rutas de la imagen no implican que el SQL actual cree la extensión pgRouting.

Comprobar disponibilidad y versión usando las variables dentro del contenedor, sin escribir contraseñas en los comandos:

```powershell
docker compose exec db sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
docker compose exec db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT PostGIS_Version();"'
```

Cambiar `POSTGRES_*` no cambia usuarios ni contraseñas de una base ya inicializada. Los scripts de init tampoco se vuelven a ejecutar sobre un volumen existente. `01_schema.sql` no es una migración idempotente: no reaplicarlo sobre tablas existentes. Preparar y revisar SQL incremental, con respaldo probado antes de desplegarlo.

`backend/Data/Migrations/IdentityMigration.sql` contiene creación de tablas de Identity **y tablas del esquema principal**. No ejecutarlo sobre `01_schema.sql`: provocaría colisiones. No hay migraciones C# ni snapshot versionados, y no existe una secuencia de `dotnet ef database update` reproducible. Antes de generar migraciones, definir y revisar una línea base para SQL e Identity; no ejecutar `Add-Migration` o scaffold con sobrescritura automática como parte del despliegue.

Las entidades actuales proceden de ingeniería inversa del esquema. Mantener SQL, modelos y DbContext coordinados. No se han modificado las tablas, modelos ni la lógica de negocio en esta actualización.

## Backend y frontend sin Docker

Arrancar solo PostgreSQL con acceso local:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d db --wait
```

En una terminal, configurar la conexión real mediante User Secrets (sustituir el marcador, nunca versionarlo):

```powershell
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Host=localhost;Port=5432;Database=directorio;Username=directorio;Password=<contraseña-local>' --project backend/backend.csproj
dotnet restore backend/backend.slnx
dotnet run --project backend/backend.csproj --launch-profile http
```

Ajustar base, usuario y puerto si cambian en `.env`. ASP.NET Core nativo no carga el `.env` de Compose. El perfil `http` usa http://localhost:5089 y Development; también existe `https` en `launchSettings.json` para desarrollo con certificado local.

En otra terminal:

```powershell
npm install --global pnpm@12.16.1
Set-Location frontend
pnpm install --frozen-lockfile
pnpm dev
```

Nuxt inicia normalmente en http://localhost:3000. Para ejecutar su salida de producción desde `frontend`:

```powershell
pnpm build
$env:NODE_ENV = 'production'
node .output/server/index.mjs
```

## Compilación y pruebas

Desde la raíz:

```powershell
dotnet build backend/backend.slnx --configuration Release
pnpm --dir frontend install --frozen-lockfile
pnpm --dir frontend build
docker compose config --quiet
docker compose -f docker-compose.yml -f docker-compose.dev.yml --profile tools config --quiet
docker compose build backend frontend db
git diff --check
```

No existen proyectos de pruebas .NET ni un script `test` de frontend. Una compilación correcta no sustituye pruebas funcionales. La prueba SQL existente puede ejecutarse sobre una base de desarrollo con el esquema ya creado (usa transacción y ROLLBACK):

```powershell
Get-Content -Raw -Encoding UTF8 db/init/02_test.sql | docker compose exec -T db sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

## Despliegue y mantenimiento

No hay pipeline CI/CD, terminación TLS ni registro de imágenes configurados. El archivo base es un punto de partida para despliegue en un host protegido: usa Production, reinicios automáticos y puertos loopback. No proporciona HTTPS ni una exposición pública completa. El proxy/TLS de la infraestructura y las políticas de autenticación deben resolverse antes de publicar la aplicación.

En el host de destino, proporcionar el `.env` privado, validar Compose, construir o distribuir imágenes revisadas y arrancar con `docker compose up -d --wait`. Revisar logs, salud frontend y consulta de API. Aplicar únicamente SQL incremental aprobado sobre una base existente; no montar migraciones improvisadas ni ejecutar automáticamente el SQL Identity. Mantener backups fuera del volumen y probar restauraciones.

`POSTGRES_USER` es el usuario de inicialización con privilegios elevados, que actualmente utiliza también la API. Antes de exposición pública, provisionar un rol de aplicación con permisos mínimos y suministrar su conexión mediante configuración segura; no cambiar permisos de una base existente sin revisar las operaciones SQL que necesita la aplicación. No habilitar pgAdmin públicamente. Los secretos en variables de entorno son visibles para operadores con acceso a Docker: restringir ese acceso y usar el gestor de secretos de la infraestructura cuando esté disponible.

## Solución de problemas

- **Falta una variable obligatoria:** completar `.env`. Validar con `config --quiet`, porque `config` sin esa opción puede imprimir credenciales.
- **pgAdmin no inicia:** definir `PGADMIN_DEFAULT_PASSWORD`; una contraseña vacía se rechaza. Los datos de una instalación previa pueden conservar su cuenta original.
- **API falla al consultar:** revisar logs, conexión y existencia de `catalog.parameter_definition`; PostgreSQL saludable no garantiza esquema válido.
- **Nombre `db` no resuelve desde Windows:** usar `localhost` con el override dev. Dentro de Compose usar `db`.
- **Puerto ocupado:** ajustar el puerto del host en `.env`; mantener los puertos internos 5432, 8080 y 3000.
- **Cambios SQL no aparecen:** el volumen ya está inicializado. Crear una migración incremental y conservar los datos.
- **Nuxt falla en instalación:** usar la versión de pnpm fijada y Node compatible. Conservar el lockfile; no resolverlo eliminándolo.
- **OpenAPI/Scalar no aparecen:** están habilitados únicamente en Development.

Referencias de configuración: [contenedores .NET](https://learn.microsoft.com/en-us/dotnet/core/docker/), [despliegue Nuxt](https://github.com/nuxt/nuxt/blob/main/docs/1.getting-started/16.deployment.md), [redes Compose](https://docs.docker.com/compose/how-tos/networking) y [ciclo de soporte Node](https://github.com/nodejs/Release).
