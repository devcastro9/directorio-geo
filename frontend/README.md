# Frontend directorio-geo

Nuxt 4 con Vue 3 y TypeScript. Actualmente muestra la bienvenida de Nuxt y no consume la API.

Instalación, versiones, Docker, variables y validaciones se documentan en el [README principal](../README.md).

Desde esta carpeta, con Node compatible y pnpm 12.16.1:

```powershell
pnpm install --frozen-lockfile
pnpm dev
pnpm build
```

La salida de producción es `.output`; la imagen Docker ejecuta `.output/server/index.mjs` con usuario `node`. No hay script de pruebas ni variables públicas de API configuradas.
