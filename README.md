# directorio-geo

Base de datos geoespacial para un directorio de negocios del mercado boliviano. El esquema contempla negocios, sucursales, categorías, territorio, búsqueda, auditoría y exposición comercial.

> Estado: esquema SQL en desarrollo. El backend web todavía no forma parte de este repositorio.

## Stack actual

- PostgreSQL 18 + PostGIS 3.6
- Docker Desktop y Docker Compose v2
- pgAdmin 4 para administrar la base de datos

El repositorio actual levanta PostgreSQL/PostGIS y pgAdmin. El servicio `web` está declarado en `docker-compose.yml`, pero requiere `backend/Dockerfile` y el código Django, que aún no están incluidos.

## Requisitos

- Docker Desktop con Docker Compose v2.
- Git.

## Configuración

Copia la plantilla de variables y cambia las credenciales antes de iniciar los contenedores:

```powershell
Copy-Item .env.example .env
```

`.env` no debe subirse al repositorio. Compose usa estas variables para crear la base, el usuario y la cuenta de pgAdmin.

## Iniciar PostgreSQL y pgAdmin

Para levantar únicamente la base de datos y pgAdmin:

```powershell
docker compose up -d db pgadmin
```

Direcciones locales:

- PostgreSQL/PostGIS: `localhost:5432`
- pgAdmin: http://localhost:5050/

Inicia sesión en pgAdmin con `PGADMIN_DEFAULT_EMAIL` y `PGADMIN_DEFAULT_PASSWORD` de `.env`. Para registrar el servidor PostgreSQL desde pgAdmin usa:

- Host: `db`
- Puerto: `5432`
- Base de datos: `POSTGRES_DB`
- Usuario: `POSTGRES_USER`
- Contraseña: `POSTGRES_PASSWORD`

Desde programas ejecutados directamente en Windows, el host de PostgreSQL es `localhost`, no `db`.

## Inicializar el esquema

En un volumen nuevo, PostgreSQL ejecuta automáticamente los scripts montados en `/docker-entrypoint-initdb.d`. El esquema principal está en `db/init/001_esquema.sql`.

Si el volumen ya existía antes de agregar o modificar el script, aplícalo manualmente. El siguiente comando usa los valores de la plantilla; ajústalos si cambiaste `.env`:

```powershell
Get-Content -Raw -Encoding UTF8 db\init\001_esquema.sql | docker compose exec -T db psql -U directorio -d directorio
```

Para comprobar que el servicio está listo:

```powershell
docker compose exec db pg_isready -U directorio -d directorio
docker compose exec db psql -U directorio -d directorio -c "SELECT PostGIS_Version();"
```

El script también solicita las extensiones `unaccent`, `pg_trgm`, `btree_gist`, `pgcrypto` y `pgrouting`. La imagen configurada debe incluirlas; si `pgrouting` no está disponible, la inicialización fallará y será necesario usar una imagen que la incluya o retirar esa extensión del esquema.

## SQL primero

- Los scripts viven en `db/init/` y siguen el formato `001_...sql`, `002_...sql`.
- Los scripts de `docker-entrypoint-initdb.d` solo se ejecutan automáticamente cuando el volumen `pgdata` está vacío.
- Los cambios posteriores deben aplicarse manualmente contra PostgreSQL y probarse antes de desplegarlos.
- Si se modifica una tabla, actualiza el esquema SQL y cualquier aplicación que consuma esa tabla.

## Comandos útiles

```powershell
# Consola SQL
docker compose exec db psql -U directorio -d directorio

# Estado y logs
docker compose ps
docker compose logs -f db
docker compose logs -f pgadmin

# Detener los servicios y conservar los datos
docker compose down

# Detener y borrar también los volúmenes (destructivo)
docker compose down -v
```

`docker compose down` conserva los datos en los volúmenes `pgdata` y `pgadmin_data`. No uses `docker compose down -v` si necesitas conservar la base.

## Estructura

```
db/init/001_esquema.sql  Esquema PostgreSQL/PostGIS
docker-compose.yml       Servicios db, pgadmin y web reservado
.env.example              Plantilla de variables para Compose
.dockerignore
```

## Próximos pasos

- [ ] Confirmar la imagen de PostgreSQL con soporte para pgRouting.
- [ ] Añadir el backend Django/GeoDjango.
- [ ] Añadir la interfaz web y las búsquedas por texto, ciudad y categoría.
- [ ] Cargar catálogos, territorios y datos de OpenStreetMap.
- [ ] Implementar negocios destacados, vigencias y registro de visitas.
