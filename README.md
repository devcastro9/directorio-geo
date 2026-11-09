# Directorio Geo de negocios de Bolivia

Documentación rectificada: versión 2.0, 2026-11-09.

El producto previsto permitirá encontrar sucursales, consultar horarios, contactos, precios orientativos y fotos, y abrir indicaciones para llegar. El visitante no necesita cuenta ni conocimientos geográficos.

## Documentos y alcance

- [Funcionalidades](Funcionalidades_Directorio_Geo_SQL_v1_3.md): alcance, valor para el usuario, entregas y criterios de aceptación.
- [Contrato API](REQUERIMIENTOS_API.md): fuente normativa de rutas, parámetros, DTO JSON/GeoJSON, permisos, reglas y cambios SQL necesarios.
- [README del backend](backend/README.md): estructura ASP.NET Core, configuración, ejecución y bloqueos actuales.
- [README del frontend](frontend/README.md): estructura Nuxt, comandos e integración pendiente con la API.
- Este README: entorno compartido, base de datos, despliegue y orden de implementación.

La documentación comparte una sola planificación: **P0**, base técnica; **P1**, MVP público administrable; **P2**, autogestión por propietarios. P0/P1 suman **27 operaciones HTTP**; P2 añade **4**, para **31** en total. Cada combinación método+ruta cuenta una vez; OpenAPI/Scalar de desarrollo no son operaciones del producto. JSON y GeoJSON son dos representaciones de una misma búsqueda.

Quedan fuera del alcance comprometido favoritos, reseñas, publicidad, métricas de visitantes, importaciones masivas, autocompletado, análisis GIS y motor de rutas. Sus tablas existentes se conservan; no obligan a desarrollar interfaces o endpoints. No agregar esas funcionalidades por el solo hecho de estar en la base.

## Estado real y stack inamovible

La única operación implementada en los controladores es `GET /api/v1/Prueba`, que devuelve entidades de `catalog.parameter_definition`. Nuxt muestra `NuxtWelcome`. Identity y paquetes JWT están presentes; `Program.cs` aún no configura autenticación ni autorización y permite cualquier origen CORS. El servicio de autenticación es incompleto. Las rutas de la especificación son trabajo pendiente, no una descripción de una API terminada.

| Área | Disponible en el código | Pendiente |
| --- | --- | --- |
| Backend | Consulta de prueba, contexto EF y modelos SQL/Identity | Completar autenticación y DTO; implementar las operaciones del contrato |
| Frontend | Bienvenida Nuxt, scripts de desarrollo y build, imagen Nitro | Buscador, mapa, fichas, formularios y consumo de API |
| Base | Línea base SQL y pruebas transaccionales | Incrementos, datos territoriales comprobados y correcciones de integridad |
| Operación | Compose, healthcheck DB y frontend, pgAdmin opcional | Salud de API, proxy HTTPS y entrega automatizada |

El trabajo de autenticación contiene bloqueos visibles por inspección: `AuthService` no implementa `IAuthService` y faltan los tipos `UserResponse` y `AuthSession`. El [README del backend](backend/README.md) detalla también la diferencia entre los DTO provisionales y el contrato. Los comandos de arranque del backend y de construcción del conjunto requieren resolver esos bloqueos. El frontend puede desarrollarse por separado porque todavía no depende de la API.

| Componente | Base del repositorio que se conserva |
| --- | --- |
| Backend | ASP.NET Core 10, C#, `net10.0`, EF Core 10.0.14 |
| Datos | Npgsql 10.0.3, NetTopologySuite, Database First |
| Autenticación | ASP.NET Core Identity con Guid y JWT Bearer |
| Frontend | Nuxt 4, Vue 3, TypeScript; pnpm 12.16.1 y lockfile existente |
| Base | PostgreSQL 18, PostGIS 3.6, esquemas app/catalog/geo/public |
| Fotos | BYTEA en PostgreSQL, entrega binaria mediante API |
| Ejecución | Docker Compose, Node 22 Alpine y Nitro node-server |

`frontend/package.json` requiere Node `^22.22.3 || ^24.15.0 || >=26.0.0`. Las versiones efectivas de frontend las fija `pnpm-lock.yaml`. No sustituir frameworks, base, almacenamiento de fotos ni mecanismo de autenticación. pgRouting, osm2pgrouting y osmium-tool están instalados en `db/Dockerfile`; se conservan como parte de la imagen actual, sin desarrollar un motor de rutas. pgAdmin es opcional.

## Estructura actual y arquitectura acordada

Un monolito modular ASP.NET Core, un frontend Nuxt y una base PostgreSQL. Un único `AppDbContext` con Identity; SQL versionado como fuente de verdad. La estructura existente relevante es:

```text
directorio-geo/
  README.md
  REQUERIMIENTOS_API.md
  Funcionalidades_Directorio_Geo_SQL_v1_3.md
  docker-compose.yml
  docker-compose.dev.yml
  .env.example
  backend/
    README.md
    backend.csproj
    backend.slnx
    Program.cs
    Controllers/                 # Prueba y controladores sin acciones
    DTOs/                        # Contratos provisionales
    Services/                    # Autenticación incompleta
    Data/AppDbContext.cs
    Data/Migrations/IdentityMigration.sql
    Identity/
    Models/
    Middleware/                  # Clase de errores vacía
    BackgroundJobs/              # Clases vacías, fuera del alcance comprometido
    Dockerfile
  frontend/
    README.md
    package.json
    pnpm-lock.yaml
    nuxt.config.ts
    app/app.vue                  # NuxtWelcome y NuxtRouteAnnouncer
    public/
    Dockerfile
  db/
    Dockerfile
    init/01_schema.sql
    init/02_test.sql
```

Las carpetas de pruebas, `db/migrations/`, los módulos completos de API y las páginas/composables/componentes del frontend son trabajo futuro. El contrato API define la organización y el comportamiento a implementar; no hace falta crear carpetas vacías. Los controladores resuelven HTTP, los servicios aplican permisos/transacciones y las consultas proyectan DTO usando PostGIS. No se necesita un Repository por tabla ni servicios distribuidos.

El contexto Docker del backend es `backend/`. Si se incorpora el proyecto de pruebas dentro de esa carpeta, excluir sus fuentes del proyecto web y agregarlo explícitamente a la solución. La interfaz traducirá los filtros técnicos a «Cerca de mí», «Buscar en esta zona» y «Abierto ahora».

Producción objetivo: un origen HTTPS con `/api/v1/*` dirigido al backend y el resto a Nuxt mediante el proxy de infraestructura. Nuxt deberá declarar `runtimeConfig.public.apiBase=/api/v1` y una base privada configurable para consultas SSR, accesible desde su red. Compose aún no incorpora ese proxy ni runtimeConfig. En desarrollo se permiten explícitamente los orígenes locales configurados, con credenciales; no combinar credenciales con `AllowAnyOrigin`.

Mapa objetivo: componente Vue exclusivo de navegador con Leaflet, sin sustituir Nuxt. URL de teselas y atribución son configuración pública; no se incluye un servicio de teselas propio. El entorno de entrega debe proporcionar un proveedor autorizado con su atribución antes de habilitar el mapa. La lista sigue funcionando si las teselas fallan.

## Instalación con Docker

Todos los comandos de este README parten de la raíz del repositorio. Requisitos: Docker con contenedores Linux y Compose v2. Desde la raíz, copiar la plantilla solo si no existe `.env`:

```powershell
if (-not (Test-Path -LiteralPath .env)) {
    Copy-Item -LiteralPath .env.example -Destination .env
}
```

Completar los valores privados localmente. No sobrescribir un `.env` existente ni versionarlo.

| Variable actual | Uso |
| --- | --- |
| POSTGRES_DB / POSTGRES_USER / POSTGRES_PASSWORD | Obligatorias; inicialización DB y conexión actual de API |
| BACKEND_PORT / FRONTEND_PORT | 8000 / 3000 por defecto |
| POSTGRES_PORT | 5432; publicado solo con override dev |
| PGADMIN_DEFAULT_EMAIL / PGADMIN_DEFAULT_PASSWORD | Usuario y contraseña para perfil tools; contraseña obligatoria al usarlo |
| PGADMIN_PORT | 5050 por defecto |

Compose construye `ConnectionStrings__DefaultConnection` con host `db`. La contraseña se entrecomilla para Npgsql: si contiene comillas dobles, duplicarlas en la cadena o proporcionar la cadena completa desde configuración segura. Para `$` en `.env`, utilizar comillas simples y validar sin imprimir secretos.

```powershell
docker compose config --quiet
docker compose build
docker compose up -d --wait
```

Frontend: http://localhost:3000. Prueba actual: http://localhost:8000/api/v1/Prueba. El archivo base usa Production y puertos loopback. DB no expone puerto; la red `database` es interna. `pg_isready` no comprueba esquema ni credenciales de la API. No existe aún healthcheck del backend; el frontend comprueba `/` con Node.

Para desarrollo:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml config --quiet
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build --wait
```

DB accesible en localhost:5432; OpenAPI en http://localhost:8000/openapi/v1.json y Scalar en http://localhost:8000/scalar/v1. El override no proporciona hot reload. Usar la misma combinación de archivos `-f` para detener o consultar este entorno.

pgAdmin opcional, una vez definida su contraseña:

```powershell
docker compose --profile tools up -d pgadmin
```

Abrir http://localhost:5050 y registrar host `db`, puerto 5432 y credenciales locales. Desde Windows usar `localhost` con el override dev.

```powershell
docker compose ps
docker compose logs --tail 100 backend frontend db
docker compose stop
docker compose down
```

`down` conserva volúmenes. No eliminar volúmenes para actualizar el esquema. Se mantienen el proyecto `geodirectorio` y el montaje PostgreSQL 18 `/var/lib/postgresql`.

## Desarrollo sin Docker para la aplicación

Mantener PostgreSQL en Docker:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d db --wait
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Host=localhost;Port=5432;Database=directorio;Username=directorio;Password=<contraseña-local>' --project backend/backend.csproj
dotnet restore backend/backend.slnx
dotnet run --project backend/backend.csproj --launch-profile http
```

El backend nativo usa http://localhost:5089 y no carga el `.env` de Compose. Sustituir puerto, base y credenciales por los locales. En otra terminal:

```powershell
npm install --global pnpm@12.16.1
pnpm --dir frontend install --frozen-lockfile
pnpm --dir frontend dev
```

Las claves `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes=15` y `Jwt:RefreshTokenDays=30` existen en appsettings pero aún no se consumen. La implementación debe añadir `Jwt:SigningKey` mediante secretos (al menos 32 bytes aleatorios), `Security:AllowedOrigins` y las bases API de Nuxt. Las credenciales del primer administrador deberán suministrarse por un procedimiento privado de inicialización con UserManager, todavía pendiente; nunca por registro público ni con contraseña fija. Ninguna clave privada va en `NUXT_PUBLIC_*`.

## Base y migraciones

En un volumen vacío se ejecutan `01_schema.sql` y `02_test.sql`; el segundo termina con ROLLBACK. PostgreSQL 18 es necesario por `uuidv7`. Se crean PostGIS, pg_trgm, unaccent y btree_gist. La línea base ya incluye tablas Identity y su historial. `backend/Data/Migrations/IdentityMigration.sql` también crea tablas de dominio: **no ejecutarlo sobre la línea base**.

La política es una sola: incrementos SQL numerados en `db/migrations/`, revisados y aplicados con `psql -v ON_ERROR_STOP=1` durante mantenimiento, registrados por versión y checksum en una tabla de control a incorporar. Cada incremento se ejecuta una vez, en transacción, con respaldo y prueba de restauración. El arranque de la API no ejecuta `EnsureCreated`, `Migrate` ni scripts DDL. No añadir DbUp/Flyway como requisito.

Para instalaciones nuevas, inicializar con la línea base y después aplicar incrementos. Para existentes, inventariar tablas/historial y aplicar solo pendientes; los archivos de init no se vuelven a ejecutar. Cambiar `.env` no cambia contraseñas de una base inicializada. Ver el contrato API, sección 12, para las correcciones exactas del SQL. Regenerar modelos solo tras aplicar y probar el esquema; conservar Identity y personalizaciones partial.

## Verificación y orden de implementación

1. P0: línea base y SQL incremental; catálogos y límites territoriales con procedencia; rol DB de aplicación; autenticación y transacciones con auditoría; salud y errores.
2. P1: administración completa de negocios/sucursales/fotos, publicación, búsqueda JSON/GeoJSON y fichas Nuxt. Retirar `/Prueba` al completar las rutas reales.
3. P2: registro y asociación administrativa de propietarios; reutilizar las rutas de management con autorización por negocio.

Comprobaciones disponibles:

```powershell
git diff --check
dotnet build backend/backend.slnx --configuration Release
pnpm --dir frontend install --frozen-lockfile
pnpm --dir frontend build
docker compose config --quiet
Get-Content -Raw -Encoding UTF8 db/init/02_test.sql | docker compose exec -T db sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

Actualmente no existen pruebas .NET ni script test frontend. El backend tiene los bloqueos de código indicados en su README. Los comandos anteriores son instrucciones de comprobación, no resultados de ejecución; esta actualización documental no compila ni inicia servicios. La implementación debe incorporar las pruebas de aceptación de los dos documentos normativos y OpenAPI con los DTO concretos, no respuestas ilustrativas sin esquema.

Antes de exposición pública: configurar HTTPS/proxy, cerrar CORS, desactivar logs sensibles, usar un rol DB con permisos mínimos en vez del usuario inicial, preparar backups y comprobar restauración. No hay pipeline CI/CD ni terminación TLS incorporados. pgAdmin permanece en loopback. La salud lista de la futura API no debe depender de datos de negocios ni exponerse como diagnóstico detallado.

Problemas habituales: si falla una variable, completar `.env` y ejecutar `config --quiet`; si falla `/Prueba`, revisar conexión/esquema; si `db` no resuelve desde Windows, usar localhost; si un puerto está ocupado, cambiar el puerto del host; si no aparece OpenAPI, usar Development. Conservar siempre el lockfile de pnpm.
