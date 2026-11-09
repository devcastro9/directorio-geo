# Frontend Directorio Geo

Nuxt 4, Vue 3 y TypeScript; pnpm 12.16.1 y lockfile existente. Actualmente muestra `NuxtWelcome` y no consume la API. No hay script de pruebas.

Instalación y entorno: [README principal](../README.md). Contratos normativos: [REQUERIMIENTOS_API.txt](../REQUERIMIENTOS_API.txt), especialmente secciones 2 a 11. Alcance: [funcionalidades](../Funcionalidades_Directorio_Geo_SQL_v1_3.txt).

```powershell
pnpm install --frozen-lockfile
pnpm dev
pnpm build
```

La salida de producción es `.output`; Docker ejecuta `.output/server/index.mjs` como usuario `node`. Usar Node compatible con `package.json`.

Implementación pendiente:

- Sustituir la bienvenida por `NuxtLayout` y `NuxtPage`; usar `app/pages`, componentes Vue, composables y middleware Nuxt.
- Buscador con filtros sincronizados en URL, tarjetas y mapa cliente Leaflet; consumir una misma página GeoJSON para lista y marcadores. Mostrar total y paginación.
- «Cerca de mí» requiere permiso; «Buscar en esta zona» cambia radio por bbox. Presentar distancias en km a partir de `distanceM`. GeoJSON usa `[lng,lat]`.
- Ficha por slug y selección de sucursal, contactos, horarios/festivos, fotos y llegada externa. No mostrar cerrado cuando `isOpen` sea null.
- Login y panel management en P1. Registro y autogestión en P2. Formularios privados utilizan los DTO completos del contrato; guardar ETag del negocio y enviar If-Match. Ante 412, conservar formulario y ofrecer recargar, sin reintentar sobrescritura.
- API pública configurable con `runtimeConfig.public.apiBase`; base privada SSR separada. Estas variables aún no existen. Producción bajo un origen HTTPS; proxy de `/api/v1` al backend.
- Access token en memoria, refresh exclusivamente en cookie HttpOnly; nunca localStorage. Renovación única compartida entre solicitudes, sin refresh concurrentes. Al recargar consultar refresh antes de entrar al panel; no requiere SSR de páginas privadas.
- Manejar ProblemDetails, errores por campo, vacío, carga, falta de permiso geográfico y mapa no disponible. Los permisos de navegación no sustituyen los de la API.
- Imágenes públicas usan URL API; borradores se previsualizan mediante fetch autenticado y Blob URL, liberada al desmontar.

No colocar secretos en configuración pública ni agregar Pinia, router manual o servicios externos sin una necesidad concreta. Teselas y atribución se configuran por entorno. La lista funciona aunque el mapa no esté disponible.
