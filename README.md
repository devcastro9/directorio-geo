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

## Estructura

```text
backend/
  backend.slnx                  Solución con un proyecto
  backend.csproj                Dependencias .NET
  Program.cs                    Configuración HTTP y EF
  Controllers/PruebaController.cs
  Data/AppDbContext.cs
  Data/Migrations/IdentityMigration.sql
  Models/
  Dockerfile
frontend/
  app/app.vue                   Bienvenida de Nuxt
  nuxt.config.ts
  package.json
  pnpm-lock.yaml
  pnpm-workspace.yaml
  Dockerfile
db/
  Dockerfile
  init/01_schema.sql            Esquema principal
  init/02_test.sql              Pruebas transaccionales con ROLLBACK
docker-compose.yml             Base con ASPNETCORE_ENVIRONMENT=Production
docker-compose.dev.yml         Puertos DB y OpenAPI para desarrollo
.env.example                   Plantilla sin contraseñas
```

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
