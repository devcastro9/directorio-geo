# Frontend — Directorio Geo de Negocios de Bolivia

> **Versión:** 1.0  
> **Fecha:** 2026-11-16  
> **Stack:** Next.js 16 (App Router), React 19, TypeScript 5, Tailwind CSS 4, Turbopack, pnpm  
> **Documentación del proyecto:** [README principal](../README.md) | [Requerimientos Frontend](../requirements-frontend.md) | [Contrato API](../REQUERIMIENTOS_API.md) | [Reglas de Agentes](../AGENTS.md)

Aplicación web pública y panel de administración para el Directorio Geo de Negocios de Bolivia. Permite a los usuarios buscar sucursales, ver resultados en lista y mapa interactivo, consultar información detallada de comercios y acceder a funciones de gestión de catálogo.

---

## 1. Stack Tecnológico

| Componente | Tecnología | Versión |
| --- | --- | --- |
| Framework Web | Next.js (App Router, Turbopack) | `16.4.6` |
| Biblioteca UI | React | `19.3.0` |
| Lenguaje | TypeScript | `^5` |
| Estilos | Tailwind CSS (v4) con `@tailwindcss/turbopack` | `4.3.3` |
| Gestor de Paquetes | pnpm | `12.16.1` |
| Mapas | Mapbox GL JS (`react-map-gl`) | Compatible con tokens públicos |
| Contenedor | Docker (Node 22 Alpine, salida `standalone`) | Multi-stage |

---

## 2. Estructura del Proyecto

```text
frontend/
├── Dockerfile              # Construcción multi-stage ligera (standalone)
├── .dockerignore           # Exclusiones de contexto Docker
├── AGENTS.md               # Directrices específicas de Next.js 16 (mantenido por next dev)
├── README.md               # Este documento
├── eslint.config.mjs       # Configuración de ESLint 9
├── next.config.ts          # Configuración de Next.js (standalone, reactCompiler, Turbopack)
├── package.json            # Dependencias y scripts
├── pnpm-lock.yaml          # Bloqueo de versiones determinista
├── public/                 # Archivos estáticos servidos directamente
│   ├── robots.txt          # Reglas para indexadores de motores de búsqueda
│   └── *.svg               # Iconos base
└── src/
    └── app/                # Next.js App Router
        ├── favicon.ico
        ├── globals.css     # Estilos globales con Tailwind v4
        ├── layout.tsx      # Layout raíz de la aplicación
        └── page.tsx        # Página principal de búsqueda y mapa
```

---

## 3. Variables de Entorno

Crear un archivo `.env.local` en el directorio `frontend/` (o configurar las variables en el entorno de despliegue):

```env
# URL base de la API del Backend (en cliente o desarrollo)
NEXT_PUBLIC_API_URL=http://localhost:8080/api/v1

# Token de acceso público para mapas interactivos
NEXT_PUBLIC_MAPBOX_TOKEN=pk.eyJ1IjoiZXhhbXBsZSIsImEiOiJjbGV4YW1wbGUwMDAwIn0...
```

---

## 4. Desarrollo Local

### Requisitos previos
- Node.js `^22.22.3 || ^24.15.0 || >=26.0.0`
- pnpm `12.16.1` habilitado (`corepack enable`)

### Comandos principales

```powershell
# Instalar dependencias
pnpm install

# Iniciar servidor de desarrollo con Turbopack (http://localhost:3000)
pnpm dev

# Comprobar tipos y formateo de código con ESLint
pnpm lint

# Compilar para producción (genera salida optimizada en .next/standalone)
pnpm build

# Iniciar servidor de producción localmente
pnpm start
```

---

## 5. Integración con el Backend

- Todas las peticiones al backend se dirigen al prefijo canónico `/api/v1/`.
- En desarrollo local con Docker Compose, el backend se publica en `http://localhost:8000` (o `8080` de forma nativa).
- Las respuestas exitosas del backend llegan envueltas en el tipo `{ data: T, traceId?: string }` (`ApiResponse<T>`).
- Las colecciones paginadas incluyen `{ items: T[], totalCount: number, page: number, pageSize: number, totalPages: number }` (`PagedResult<T>`).
- En caso de error, el backend devuelve respuestas RFC 7807 (`application/problem+json`).

---

## 6. Docker y Despliegue

La imagen de producción utiliza la opción `output: "standalone"` de Next.js, reduciendo el tamaño del contenedor final a un mínimo (~150MB en Alpine):

```powershell
# Construcción de la imagen Docker desde la raíz del repositorio
docker compose build frontend

# Ejecución del contenedor
docker compose up -d frontend
```
El contenedor expone internamente el puerto `3000` y realiza verificaciones de salud mediante `fetch` nativo de Node.js.
