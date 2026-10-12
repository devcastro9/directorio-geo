# directorio-geo

Plataforma web de descubrimiento de negocios para el mercado boliviano: fichas con ubicación en mapa, búsqueda por texto, ciudad y categoría, y exposición pagada con vigencia.

> Estado: MVP en desarrollo. Ciudad y nicho aún por validar.

## Stack

- Python 3.14 y Django 6.1
- PostgreSQL + PostGIS (GeoDjango)
- django-environ, django-simple-history, django-htmx, django-unfold, Pillow
- Docker y Docker Compose

Todo corre en contenedores y no depende de servicios externos, para poder migrar a on-premise.

## Requisitos

- Docker Desktop (o Docker Engine + Compose)
- Git

## Puesta en marcha

1. Copia el archivo de entorno y edítalo (cambia las claves):

```powershell
   copy .env.example .env
```

   Si cambias `POSTGRES_USER`, `POSTGRES_PASSWORD` o `POSTGRES_DB`, actualiza también `DATABASE_URL`.

2. Genera una `SECRET_KEY` y pégala en `.env`:

```powershell
   docker compose run --rm --no-deps web python -c "from django.core.management.utils import get_random_secret_key as k; print(k())"
```

3. Levanta los servicios:

```powershell
   docker compose up --build
```

4. Crea el esquema de negocios (ver sección Base de datos) y las tablas internas de Django:

```powershell
   Get-Content -Raw -Encoding UTF8 db\init\001_negocios.sql | docker compose exec -T db psql -U directorio -d directorio
   docker compose exec web python manage.py migrate
   docker compose exec web python manage.py createsuperuser
```

5. Abre:

   - Listado: http://localhost:8000/negocios/
   - Admin: http://localhost:8000/admin/

## Base de datos (SQL primero)

El esquema del negocio se define en SQL, no con migraciones de Django.

- Los scripts viven en `db/init/` y se numeran: `001_negocios.sql`, `002_...sql`.
- Los modelos de la app `negocios` usan `managed = False`: Django lee y escribe, pero nunca crea ni altera esas tablas.
- No ejecutes `makemigrations negocios`.
- `migrate` solo crea las tablas propias de Django (usuarios, sesiones, admin).
- Los scripts montados en `docker-entrypoint-initdb.d` solo corren con el volumen vacío. Para aplicar cambios después, ejecútalos a mano con el comando del paso 4.
- Si cambias una tabla, actualiza también el modelo; Django no detecta la diferencia.

## Comandos útiles

```powershell
# Verificar conexión y versión de PostGIS desde Django
docker compose exec web python manage.py shell -c "from django.db import connection; c = connection.cursor(); c.execute('SELECT PostGIS_Version()'); print(c.fetchone()[0])"

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
requirements.txt
.env.example     Plantilla de variables de entorno
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
- [ ] Despliegue con gunicorn y estáticos servidos por whitenoise