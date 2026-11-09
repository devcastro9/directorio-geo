# Frontend — Directorio Geo

Interfaz Nuxt 4, Vue 3 y TypeScript del directorio de negocios de Bolivia. El [README principal](../README.md) explica el entorno compartido y Docker Compose; el [README del backend](../backend/README.md) describe la API y sus bloqueos actuales. Los comportamientos a implementar están en [REQUERIMIENTOS_API.md](../REQUERIMIENTOS_API.md) y [Funcionalidades](../Funcionalidades_Directorio_Geo_SQL_v1_3.md).

## Estado actual

[app/app.vue](app/app.vue) contiene `NuxtRouteAnnouncer` y `NuxtWelcome`. No existen páginas de negocio, buscador, mapa, autenticación ni consumo de API. El frontend puede iniciarse por separado porque todavía no depende del backend.

[nuxt.config.ts](nuxt.config.ts) declara `compatibilityDate: '2025-07-15'` y habilita DevTools. No define `runtimeConfig`, base API ni proxy. Leaflet y Pinia no están instalados; el mapa es parte del trabajo futuro.

| Elemento | Configuración actual |
| --- | --- |
| Framework | Nuxt 4, Vue 3 y TypeScript |
| Versiones resueltas en lockfile | Nuxt 4.6.4, Vue 3.5.45 y Vue Router 5.4.0 |
| Gestor de paquetes | pnpm 12.16.1, fijado en package.json |
| Node admitido | `^22.22.3`, `^24.15.0` o `>=26.0.0` |
| Imagen Docker | Node 22 Alpine, Nitro node-server |
| Pruebas y lint | Sin scripts configurados |

Conservar [pnpm-lock.yaml](pnpm-lock.yaml). Las dependencias declaradas en [package.json](package.json) usan rangos; el lockfile determina las versiones instaladas con `--frozen-lockfile`.

## Estructura existente

```text
frontend/
  README.md
  package.json
  pnpm-lock.yaml
  pnpm-workspace.yaml
  nuxt.config.ts
  tsconfig.json
  app/
    app.vue
  public/
    favicon.ico
    robots.txt
  Dockerfile
  .dockerignore
```

`app/pages/`, componentes, composables, layouts, middleware y tipos API todavía no están implementados. Se crearán según los flujos necesarios; no son módulos disponibles por aparecer en la arquitectura objetivo.

## Desarrollo local

Los comandos de esta sección parten de la **raíz del repositorio**. Instalar una versión de Node admitida y el pnpm fijado:

```powershell
npm install --global pnpm@12.16.1
pnpm --dir frontend install --frozen-lockfile
pnpm --dir frontend dev
```

Nuxt utiliza normalmente http://localhost:3000; si el puerto está ocupado, revisar la dirección mostrada por el proceso. También se puede trabajar desde `frontend/` quitando `--dir frontend` de los comandos. No hace falta PostgreSQL ni un backend operativo para mostrar la bienvenida actual.

| Script | Función |
| --- | --- |
| `dev` | Servidor de desarrollo Nuxt |
| `build` | Compilación de producción |
| `preview` | Previsualización de una compilación existente |
| `generate` | Generación estática; disponible, pero no es el modo usado por Docker |
| `postinstall` | Preparación de Nuxt tras instalar dependencias |

## Compilación y ejecución de producción

```powershell
pnpm --dir frontend build
pnpm --dir frontend preview
```

Docker utiliza Nitro `node-server`, salida `.output` y el archivo `.output/server/index.mjs`. Para ejecutar esa salida directamente desde la raíz, tras compilar para ese preset:

```powershell
$env:NODE_ENV = 'production'
node frontend/.output/server/index.mjs
```

Estos comandos son instrucciones reproducibles del proyecto; esta actualización documental no ejecuta builds ni certifica su resultado. No existe un script `test` o `lint` al que remitir una validación funcional.

## Docker Compose

Preparar `.env` según el README principal. Para construir e iniciar solo la interfaz desde la raíz:

```powershell
docker compose config --quiet
docker compose up -d --build frontend
docker compose logs --tail 100 frontend
```

El frontend no declara dependencia de arranque del backend. Con la configuración predeterminada se publica en http://localhost:3000, solo en loopback; `FRONTEND_PORT` cambia el puerto del host. El override de desarrollo no agrega hot reload al frontend.

[Dockerfile](Dockerfile) instala con lockfile, compila con `NITRO_PRESET=node-server` y ejecuta como usuario `node`. Define `NODE_ENV=production`, `NITRO_HOST=0.0.0.0` y `NITRO_PORT=3000`. El healthcheck de Compose consulta `/` usando Node y comprueba la respuesta HTTP, sin verificar conexión con la API.

## Integración pendiente con la API

La implementación debe declarar una base pública `runtimeConfig.public.apiBase` y una base privada para llamadas SSR. Todavía no hay variables de entorno asociadas ni proxy en este frontend; no basta con definir una variable en `.env` para activar esa integración. Nunca colocar secretos en `NUXT_PUBLIC_*`.

| Entorno previsto | Base API para el navegador | Acceso del servidor Nuxt |
| --- | --- | --- |
| Desarrollo nativo | `http://localhost:5089/api/v1` con backend nativo y CORS explícito | Dirección accesible desde el proceso local |
| Desarrollo con backend Docker | `http://localhost:8000/api/v1`, ajustando BACKEND_PORT | `http://backend:8080/api/v1` si Nuxt también está en Compose |
| Producción objetivo | `/api/v1` bajo el mismo origen HTTPS | Base privada configurada y accesible desde su red |

`backend` es un nombre de red de Compose, no un host para el navegador. Compose todavía no incluye el proxy HTTPS del entorno objetivo. CORS actual permite cualquier origen pero no credenciales: deberá ajustarse antes de utilizar refresh por cookie. El backend aún tiene bloqueos de autenticación descritos en su README.

## Orden de implementación

1. Sustituir la bienvenida por `NuxtLayout` y `NuxtPage`. Crear buscador, filtros y ficha por slug con los contratos públicos P1.
2. Agregar mapa exclusivo de navegador con Leaflet y teselas/atribución configuradas por entorno. Una consulta GeoJSON alimenta tarjetas y marcadores; la lista funciona si el mapa no está disponible.
3. Incorporar login y panel de administración P1, con formularios completos de negocio, sucursales, contactos, horarios y fotos.
4. Agregar registro y autogestión P2 al habilitar el backend correspondiente. P0/P1 definen 27 operaciones API; P2 añade cuatro. Esos números corresponden a operaciones HTTP, no a páginas Nuxt.

No agregar router manual, Pinia ni módulos de reseñas, favoritos, publicidad, importaciones o analítica por la existencia de sus tablas. Mantener el alcance acordado.

## Reglas de interfaz y contratos

- Sincronizar filtros con la URL y reiniciar página al cambiarlos. Mostrar total y página; los marcadores son los resultados de esa página.
- GeoJSON usa `[longitud, latitud]`; la API devuelve distancias en `distanceM` y la interfaz las presenta en km. Una distancia null no se muestra como cero.
- «Cerca de mí» solicita permiso del navegador. Si se deniega, permitir municipio o punto manual. «Buscar en esta zona» sustituye centro/radio por bbox.
- `isOpen: null` significa «Horario no informado». No etiquetarlo como cerrado. Horarios y excepciones usan America/La_Paz y la evaluación del servidor.
- El access token se guarda solo en memoria; refresh en cookie HttpOnly. Compartir una renovación entre solicitudes y evitar refresh concurrentes. Las páginas privadas pueden manejar sesión en el cliente, sin SSR autenticado.
- Los formularios construyen el DTO de entrada completo, conservan el ETag del negocio y envían If-Match. Ante 412, conservar el formulario y ofrecer recargar sin sobrescribir automáticamente.
- Mostrar errores ProblemDetails por campo y estados de carga, vacío y error. No reintentar una mutación cuyo resultado sea incierto sin consultar el estado antes.
- Los middleware de navegación orientan al usuario; la API vuelve a comprobar permisos. La preview de fotos privadas utiliza fetch con Bearer y Blob URL, liberada al desmontar.

Validar los recorridos móviles y por teclado, geolocalización denegada, mapa sin teselas, equivalencia lista/mapa y formularios con errores. Los criterios completos están en el contrato API; una compilación correcta por sí sola no verifica esos flujos.
