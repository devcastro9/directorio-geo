# FUNCIONALIDADES DEL DIRECTORIO GEO DE NEGOCIOS

Versión 2.0 | Fecha: 2026-11-09

## 1. OBJETIVO Y AUTORIDAD DOCUMENTAL


Ayudar a una persona a encontrar un negocio en Bolivia, saber si atiende,
consultar cómo contactarlo y cómo llegar. La unidad de búsqueda es la SUCURSAL:
un negocio puede tener varias ubicaciones y cada una aparece individualmente.

Este documento fija alcance y aceptación. [REQUERIMIENTOS_API.md](REQUERIMIENTOS_API.md) fija los
contratos normativos. [README.md](README.md) describe instalación, arquitectura y estado real.
Esta versión reemplaza las prioridades contradictorias de versiones anteriores.
No cambia el stack ni implica que la aplicación ya esté implementada.

## 2. ENTREGAS ÚNICAS


P0: integridad, datos iniciales, seguridad administrativa y operación.
P1: MVP público con catálogo administrable. P0/P1: 27 operaciones HTTP.
P2: autogestión por propietarios. Añade 4; total acumulado: 31 operaciones.
P2 no bloquea la primera publicación del directorio.

No se cuentan como funcionalidades las tablas SQL sin API e interfaz. La ruta
/Prueba y NuxtWelcome son la base actual, no el producto terminado.

## 3. VISITANTE SIN CUENTA [P1]


### F01. Buscar por nombre de negocio o sucursal, sin distinguir tildes/mayúsculas.

Valor: encontrar un establecimiento sin conocer su nombre exacto.
Aceptación: resultados por sucursal; filtros combinados con AND; orden estable;
búsqueda vacía muestra catálogo publicado, paginado; cero resultados ofrece
limpiar filtros, no sustituye la búsqueda por recomendaciones ocultas.

### F02. Filtrar por categoría y ubicación administrativa disponible.

Valor: encontrar el tipo de negocio en el lugar deseado.
Aceptación: categorías y territorios incluyen descendientes activos. La jerarquía
es departamento > provincia > municipio > zona opcional > barrio opcional.
No inventar barrios ni exigir esos niveles cuando no estén disponibles.

### F03. «Cerca de mí» o seleccionar un punto en el mapa.

Valor: encontrar opciones cercanas sin introducir coordenadas.
Aceptación: permiso explícito del navegador; si se deniega, permitir municipio
o punto manual. Radio inicial 10 km, opciones de interfaz 1/5/10/25/50 km.
La API trabaja en metros. Mostrar distancia en línea directa; no tiempo de viaje.
La ubicación del visitante no se persiste ni se registra en analítica.

### F04. Ver lista y mapa coherentes; «Buscar en esta zona».

Valor: elegir según la ubicación sin una herramienta GIS compleja.
Aceptación: una misma consulta/representación alimenta lista y marcadores;
GeoJSON usa [longitud, latitud]. Al mover el mapa, consultar solo al pulsar
«Buscar en esta zona», reiniciar página y sustituir el modo radio por bbox.
Mostrar el número total y que los marcadores corresponden a la página actual.
No ocultar un límite de resultados ni prometer que se muestran todos.
Lista utilizable sin mapa; teselas con atribución y proveedor configurado.

### F05. Consultar ficha de negocio y sus sucursales.

Valor: comparar información comercial útil y elegir una ubicación concreta.
Aceptación: nombre, descripción, categoría, dirección, estado de atención,
horarios, excepciones, contactos, fotos, servicios, características, medios de
pago y precio orientativo cuando exista. Nunca inventar precios ni mostrar cero
como sustituto de un dato ausente. No exponer NIT, razón social ni propietarios.
La ficha pública solo incluye sucursales visibles; máximo operativo 100 por negocio.

### F06. «Abierto ahora» con horario local.

Valor: evitar desplazamientos a un negocio cerrado.
Aceptación: America/La_Paz; turnos múltiples, cruce de medianoche y excepciones
por fecha. Festivos aplicables cierran por defecto; una excepción puede abrir.
Una fecha declarada cerrada anula cualquier arrastre del día anterior.
Sin información suficiente, mostrar «Horario no informado», nunca «Cerrado» por
ausencia de datos. «Abierto ahora» solo incluye valores confirmados true.

### F07. Contactar y abrir indicaciones de llegada.

Valor: llamar, escribir o llegar desde la aplicación habitual del visitante.
Aceptación: teléfono, WhatsApp, email y enlaces comerciales válidos. Navegación
externa mediante https://www.google.com/maps/dir/?api=1&destination=lat,lng;
no se envía origen ni datos privados. No hay cálculo de rutas propio.

## 4. ADMINISTRADOR [P0/P1]


### F08. Iniciar/cerrar sesión y mantenerla de forma segura.

Aceptación: Identity Guid, JWT corto y refresh rotativo; usuario desactivado o
sesión revocada pierde acceso; sin credenciales administrativas predeterminadas.
No se abre registro público en el MVP. El operador crea/restablece cuentas por
un procedimiento privado que usa Identity, no edición de hashes en SQL.

### F09. Crear y editar un negocio junto con su primera sucursal.

Valor: alimentar y corregir un catálogo real sin intervención del desarrollador.
Aceptación: alta atómica en draft, formularios completos y validaciones claras.
Slug estable automático. Las colecciones de contactos/horarios/servicios se
editan dentro de la ficha, sin pantallas CRUD independientes por tabla.
El punto debe cubrirse por un municipio válido y verificarse dentro de Bolivia.

### F10. Publicar, retirar y dar baja/restaurar.

Valor: evitar fichas incompletas y retirar información obsoleta.
Aceptación: publicación solo administrativa, con datos mínimos definidos en API;
retirar equivale a draft; baja lógica a archived. Restauración de negocio exige
seleccionar explícitamente las sucursales que se recuperarán y vuelve a draft.
No se recuperan accidentalmente sucursales eliminadas antes. Una sucursal también
puede restaurarse individualmente con su negocio vigente. No eliminar la última
sucursal de un negocio vigente. Un negocio cerrado temporalmente sigue visible,
con aviso; cerrado permanentemente deja de ser visible.

### F11. Administrar sucursales y fotos.

Valor: mantener actualizados lugares, horarios e imágenes que orientan al cliente.
Aceptación: ubicación con mapa, sucursal principal única y foto principal única;
JPEG/PNG/WebP hasta 5 MiB y máximo 20 fotos por sucursal; sin base64 en JSON;
lectura BYTEA mediante API. Previsualización privada de borradores para autorizados.
Las operaciones simultáneas no deben sobrescribirse silenciosamente.

### F12. Consultar trazabilidad de cambios.

Valor: identificar correcciones y recuperar la confianza en el catálogo.
Aceptación: actor, fecha, entidad y operación; acceso Admin. No copiar fotos,
contraseñas, tokens ni datos privados al listado de auditoría. Catálogos,
territorios y festivos iniciales se mantienen mediante scripts versionados:
no hay un panel genérico de parámetros ni una herramienta de edición GIS.

## 5. PROPIETARIO [P2, CONTRATOS CERRADOS EN DOCUMENTO API]


### F13. Registrarse y ser asociado por un administrador.

Valor: permitir que quien atiende el negocio mantenga su información.
Aceptación: registro crea solo User. El administrador verifica por contacto
comercial y asigna el usuario por email a un negocio concreto; registra método,
actor y fecha, sin subir documentos de identidad. No hay solicitudes automáticas,
reclamos de titularidad, evidencias sensibles ni un rol Moderator.
Una identidad verificada no otorga acceso a otros negocios. Revocación inmediata.

### F14. Mantener únicamente los negocios asociados.

Aceptación: mismas rutas management que Admin, filtradas/autorizadas por negocio.
Puede editar información, contactos, horarios, fotos y sucursales; no publicar,
restaurar ni dar baja al negocio. Puede crear un negocio en draft solo si tiene
identidad vigente; se asocia al creador en la misma transacción y Admin publica.
Al editar una ficha publicada se actualiza directamente con auditoría; no se
construye un sistema de versiones/editorial. El propietario no puede editar
razón social/NIT, cambiar asociaciones ni otorgarse permisos.

## 6. RECORTE DE ALCANCE


No comprometidas: favoritos y reseñas (requieren comunidad y moderación),
autocompletado (la búsqueda y filtros cubren el flujo inicial), importaciones
CSV/XLSX (sin volumen acreditado), anuncios/pagos y analítica de visitantes.
Tampoco: mapas de calor, comparación estadística de zonas, polígonos dibujados,
contornos territoriales públicos, productos/promociones, rutas propias,
notificaciones, microservicios o un dashboard de indicadores sin decisiones útiles.

Se conservan las tablas y herramientas SQL existentes. No se crean endpoints,
workers ni interfaces para ellas en P0/P1/P2. No se generan eventos con IP o
user-agent de visitantes. Una ampliación requiere necesidad concreta del cliente
y un contrato nuevo; no es una tarea pendiente de esta entrega.

## 7. SOPORTE SQL REAL Y BRECHAS A IMPLEMENTAR


- app.find_branches YA recibe p_territory_id; no falta ese parámetro.
  Su orden patrocinado y proyección de reseñas no son el contrato del producto:
  implementar consulta adaptada y un conteo con idénticos filtros.
- Usuarios/actores del dominio usan UUID; no hay migración general TEXT a UUID.
- geo.territory.boundary es geography(MultiPolygon,4326); puede ser NULL.
  app.branch.location es geography(Point,4326), obligatorio.
- SQL limita sucursales a una caja aproximada de Bolivia; no certifica fronteras.
  Antes de publicación deben existir municipios con límites válidos y procedencia.
- app.create_business_with_branch publica directamente; NO usar sin adaptar al
  alta draft establecida. No autoriza por sí sola operaciones HTTP.
- Las restricciones diferidas de sucursal mínima existen; serializar cambios
  por negocio también es necesario para altas/bajas concurrentes.
- Los índices únicos de sucursal/foto principal están COMENTADOS: no son garantías
  existentes. Limpiar conflictos y habilitarlos mediante incremento SQL.
- app.is_open_at requiere corrección para fechas cerradas que anulen arrastres;
  el DTO necesita distinguir horario desconocido de false.
- No existen persistencia de refresh tokens ni revisión del agregado para ETag.
  Se añaden hours_known para distinguir horario desconocido de cerrado y active
  en excepciones para reemplazar calendarios sin borrar físicamente históricos.
- owner_identity y business_owner existen; no se agrega tabla de solicitudes.
- Identity ya forma parte de 01_schema.sql. No duplicar su creación con el script
  IdentityMigration.sql ni ejecutar migraciones EF automáticas sobre el dominio.

[REQUERIMIENTOS_API.md](REQUERIMIENTOS_API.md), sección 12, define cambios y orden de implementación.
Estas brechas son tareas técnicas necesarias; esta revisión solo modifica documentos.

## 8. ACEPTACIÓN DE ENTREGA


P0: base reproducible, incrementos controlados, catálogos/territorios comprobados,
cuenta Admin segura, permisos, sesiones, concurrencia, errores y auditoría.
P1: recorrido completo crear > completar sucursal > fotos > publicar > buscar
por texto/radio/bbox > ficha > contacto > baja > desaparición pública > restaurar
como draft > republicar. Lista y mapa con mismos IDs/filtros/orden/paginación.
P2: registrar > verificar/asociar > editar negocio propio > denegar otro negocio
> revocar > denegar inmediatamente al anterior propietario.

Pruebas obligatorias con PostgreSQL/PostGIS real: límite de radio, orden de
coordenadas, fronteras, descendientes, turnos nocturnos/festivos, atomicidad,
última sucursal concurrente, principales únicos, refresh reutilizado, baja de
usuario, ETag obsoleto y ausencia de información privada en respuestas públicas.
Interfaz: móvil, teclado, errores junto al campo, estados de carga/vacío/error,
permiso de ubicación denegado y mapa/teselas no disponibles.
