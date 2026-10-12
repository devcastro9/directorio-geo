# directorio-geo

Plataforma web de descubrimiento de negocios para el mercado boliviano: fichas con ubicación en mapa, búsqueda por texto, ciudad y categoría, y exposición pagada con vigencia.

> Estado: MVP en desarrollo. Ciudad y nicho aún por validar.

## Stack

- Python 3.13 y Django 6.1
- PostgreSQL + PostGIS (GeoDjango)
- django-environ, django-simple-history, django-htmx, django-unfold, Pillow
- Docker y Docker Compose

El código puede ejecutarse directamente desde Windows con el entorno Conda `geodjango`, mientras PostgreSQL/PostGIS corre en Docker. Para desplegar el stack completo, Compose construye y ejecuta la imagen de Django junto a PostGIS.

## Requisitos

- Windows 11 y Miniconda con el entorno `geodjango` preparado para GeoDjango (incluye Django, GDAL, GEOS, PROJ y el driver PostgreSQL).
- Docker Desktop con Docker Compose v2.
- Git.

## Desarrollo local en Windows

1. Copia las plantillas de entorno. `.env` configura Compose; `.env.local` configura Django ejecutado en Windows. Mantén sus credenciales de PostgreSQL sincronizadas y cambia las claves de ejemplo.

```powershell
Copy-Item .env.example .env
Copy-Item .env.local.example .env.local
```

Genera una clave de Django desde el entorno Conda y pégala en ambos archivos:

```powershell
conda activate geodjango
python -c "from django.core.management.utils import get_random_secret_key as k; print(k())"
```

2. Inicia únicamente PostgreSQL/PostGIS. Compose lee `.env`, que usa el hostname `db` dentro de la red Docker; Django local lee `.env.local`, que usa `localhost`.

```powershell
docker compose up -d db
```

3. En una base nueva, `db/init/001_esquema.sql` se aplica al inicializar el volumen. Si el volumen ya existía antes de montar ese script, aplícalo una sola vez:

```powershell
Get-Content -Raw -Encoding UTF8 db\init\001_esquema.sql | docker compose exec -T db psql -U directorio -d directorio
```

Si cambias usuario, contraseña o nombre de base en `.env`, actualiza también `DATABASE_URL` en `.env.local`.

4. Activa Conda y ejecuta Django en PowerShell:

```powershell
conda activate geodjango
python manage.py migrate
python manage.py createsuperuser
python manage.py runserver
```

Abre el listado en http://localhost:8000/negocios/ o el admin en http://localhost:8000/admin/.

El proyecto no instala ni configura Conda. Si `geodjango` no tiene las dependencias nativas requeridas por GeoDjango, consulta la documentación de GeoDjango para Windows antes de instalar `requirements.txt` con pip.

## Despliegue con Docker Compose

Prepara `.env` como arriba y ejecuta:

```powershell
docker compose up --build -d
docker compose exec web python manage.py migrate
docker compose exec web python manage.py createsuperuser
```

El esquema SQL se inicializa al crear un volumen nuevo. Django dentro de Compose usa `DATABASE_URL` con el hostname `db`; no usa `.env.local`. El código va dentro de la imagen y no se monta desde el host sobre `/app`.

El `Dockerfile` incluye el código fuente en la imagen y arranca Django con Gunicorn. WhiteNoise sirve los estáticos recolectados al iniciar el contenedor. En el servidor Linux solo se requiere Docker Engine y el plugin Docker Compose; clona o copia este repositorio y su `.env`, y ejecuta los comandos de arriba. No hace falta instalar Python, Conda, GDAL ni dependencias del proyecto en el host.

Antes de exponerlo públicamente, configura `DEBUG=False`, una `SECRET_KEY` segura, `ALLOWED_HOSTS` con tus dominios y HTTPS mediante un proxy inverso.

```powershell
docker compose logs -f web
docker compose down
```

`docker compose down` conserva los datos. Evita `docker compose down -v` si necesitas conservar la base.

## Base de datos (SQL primero)

El esquema del negocio se define en SQL, no con migraciones de Django.

- Los scripts viven en `db/init/` y se numeran: `001_esquema.sql`, `002_...sql`.
- Los modelos de la app `negocios` usan `managed = False`: Django lee y escribe, pero nunca crea ni altera esas tablas.
- No ejecutes `makemigrations negocios`.
- `migrate` solo crea las tablas propias de Django (usuarios, sesiones, admin).
- Los scripts montados en `docker-entrypoint-initdb.d` solo corren con el volumen vacío. Para aplicar cambios después, ejecútalos a mano contra PostgreSQL.
- Si cambias una tabla, actualiza también el modelo; Django no detecta la diferencia.

## Comandos útiles

```powershell
# Verificar conexión y versión de PostGIS desde Django local (Conda activo)
python manage.py shell -c "from django.db import connection; c = connection.cursor(); c.execute('SELECT PostGIS_Version()'); print(c.fetchone()[0])"

# Consola SQL
docker compose exec db psql -U directorio -d directorio

# Logs
docker compose logs -f web
docker compose logs -f db

# Detener (conserva los datos)
docker compose down
```

Evita `docker compose down -v` si tienes datos: borra el volumen de la base.

## Estructura

```
config/          Configuración de Django (settings, urls)
negocios/        App principal (modelos, vistas, plantillas)
db/init/         Scripts SQL del esquema
Dockerfile
docker-compose.yml
.dockerignore
requirements.txt
.env.example       Plantilla de variables para Compose
.env.local.example Plantilla de variables para Django local
```

## Notas

- PostgreSQL 18 espera el volumen en `/var/lib/postgresql` (no en `/var/lib/postgresql/data`).
- El mapa del admin de GeoDjango carga teselas de OpenStreetMap desde internet. Para on-premise habrá que definir teselas propias.
- `.env` contiene secretos y no debe subirse al repositorio.

## Hoja de ruta

- [ ] Categorías y subcategorías
- [ ] Ficha de detalle con mapa
- [ ] Búsqueda sin distinguir tildes (`unaccent`)
- [ ] Negocios destacados con vigencia y marca de publicidad
- [ ] Registro de visitas y acciones de contacto
- [x] Despliegue con gunicorn y estáticos servidos por whitenoise