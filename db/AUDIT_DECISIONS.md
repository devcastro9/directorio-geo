# Decisiones del esquema para el MVP

Alcance confirmado el 12 de noviembre de 2026.

## Producto

- Solo Bolivia; ubicaciones y limites territoriales persistidos como geography SRID 4326.
- Los poligonos son infraestructura interna. El usuario no dibuja ni administra SRID.
- Publicidad, resenas, respuestas y favoritos permanecen disponibles en el esquema del MVP.
- Fotos y su historial permanecen en PostgreSQL, sin migracion a almacenamiento externo.
- Se permiten varias sucursales por negocio y varias fotos por sucursal o negocio.
- Se permite conservar un negocio sin sucursales y dar de baja la ultima sucursal.
- Se incorpora geocodificacion posterior al MVP, los mapas son de MapBox.
- Frontend y backend suministran las ubicaciones de los usuarios. Este SQL no obtiene la posicion del dispositivo
  ni convierte una direccion en coordenadas.
- Rutas e isocronas se evaluaran despues del MVP mediante infraestructura interna.
  La cercania actual es distancia geodesica, no tiempo de viaje.
- Full-text y control optimista de versiones quedan para evaluacion posterior.
- No se agregan validaciones territoriales, formatos de contacto o contratos de accesibilidad.
- No se cambia el algoritmo general de busqueda por rendimiento (E2 aplazado).
  Los indices ajustados resuelven expresiones y accesos identificados en E3-E6;
  su beneficio cuantitativo requiere medicion con una carga representativa.

## Publicacion y consulta

Las categorias Hospitales, Clinicas y Farmacias son hijas de Salud y Bienestar.
La publicacion exige categoria vigente y cada sucursal que se crea o restaura en un negocio
publicado debe tener una categoria vigente al terminar la transaccion.
Por eso `app.create_business_with_branch` recibe `p_category_ids uuid[]` y las inserta
en la misma transaccion. El parametro es opcional en la firma para mantener llamadas
nombradas, pero la creacion con publicacion directa rechaza una lista vacia.

Estas comprobaciones de publicacion no obligan a conservar sucursales despues de publicar.
No se agrega validacion continua de los cambios de jerarquia o desactivacion de categorias.

`app.find_branches` conserva el orden: patrocinado, relevancia textual, distancia e ID.
Incluye `location geography`, `branch_status` y `business_status`.
Los cierres temporales siguen visibles con su estado; los permanentes no aparecen.
La publicidad aplica tambien la exclusion de cierres permanentes.
El backend serializara las coordenadas para el mapa sin consultas por cada resultado.

Si se proporciona centro sin radio, se usa el radio configurado.
Sin centro no se aplica radio. Limite y offset explicitamente nulos se rechazan.
El viewport debe ser un Polygon rectangular valido, no vacio, bidimensional y SRID 4326.
Los filtros espaciales siguen usando geography, incluidas sus aristas geodesicas.

## Horarios

`is_24_hours=true` representa un dia completo con ambas horas en 00:00.
Esto aplica al horario semanal y a una excepcion de apertura. No se arrastra al dia siguiente.

Una excepcion activa de hoy sustituye todo su dia civil:
- Si cierra, anula incluso un turno iniciado ayer.
- Si abre 24 horas, prevalece sobre feriados.
- Si tiene horas, sustituye los turnos regulares y el arrastre de ayer.

Sin excepcion, un feriado cierra todo el dia civil. Fuera de esos casos se examinan
los turnos de hoy y el posible turno nocturno de ayer.
Una excepcion desactivada con `active=false` deja de intervenir.
Zona horaria fija: America/La_Paz.

## Configuracion consumida

| Codigo | Consumidor | Alcance administrado |
| --- | --- | --- |
| photo.max_bytes | Validacion de fotos | Global o sucursal, con fallback global |
| search.default_radius_m | Busqueda con centro sin radio explicito | Global |
| search.default_limit | Busqueda sin limite explicito | Global |
| review.moderation_post | Estado predeterminado de nuevas resenas | Global |
| analytics.archive_after_days | Archivado sin fecha de corte explicita | Global |
| ad.require_paid | Seleccion de publicidad activa | Global |

La moderacion posterior crea resenas visibles; con el parametro en false, nuevas resenas
sin estado explicito comienzan ocultas hasta su moderacion. No se modifican retroactivamente.

Se conserva la estructura de alcances y vigencias; se rechazan valores con alcances sin
consumidor MVP. `branch.require_one` deja de existir porque se retiro su regla.
Valores predeterminados, valores configurados y cambios de definicion se validan.
Los limites duros de los consumidores se mantienen aunque se amplien min/max.

El Worker Service de .NET invocara archivado y consolidacion diaria/mensual.
Este cambio no implementa ni programa ese servicio, ni modifica la retencion acordada.

## Auditoria

Todas las tablas persistentes de usuario fuera de public tienen captura por fila.
Se excluyen catalogos internos de PostgreSQL, tablas temporales y el propio destino
`app.audit_log`. El destino se protege contra INSERT directo, UPDATE, DELETE y TRUNCATE;
no se registra recursivamente su propia insercion.

Se registran imagenes completas anterior/posterior, incluyendo los bytes de las fotos
serializados en JSONB. Se capturan INSERT, UPDATE y DELETE, incluidas las acciones de
UPSERT, MERGE, COPY y escrituras mediante vistas actualizables.
TRUNCATE toma una instantanea de cada fila antes de eliminarla, con operacion T.
Las operaciones rechazadas no modifican datos y no generan un registro confirmado.
Un ROLLBACK revierte tanto el cambio como su auditoria.

Las claves simples usan el valor de PK; las compuestas usan un objeto JSON.
Sin PK se conserva la fila como identificador. La clave es TEXT y el indice usa su MD5
junto con esquema, tabla y fecha para admitir claves extensas.
Al consultar por clave se debe comparar tambien el texto completo, ademas del hash.

El event trigger instala cobertura en tablas nuevas o alteradas por migraciones.
Su instalacion requiere superusuario; este privilegio no es necesario en el backend
solo por usar los triggers ya instalados. El propietario de las tablas o un superusuario
puede alterar/deshabilitar triggers: esta auditoria no es una defensa frente al administrador.

El backend y el Worker Service deben usar una transaccion y establecer
`SET LOCAL bolivianegocios.actor_id = 'uuid'` cuando exista un actor autenticado.
Los procesos sin usuario pueden dejar actor nulo. Los privilegios se derivan de la
identidad autenticada del backend, no de campos enviados por el cliente.

## Aplicacion y verificacion

`01_schema.sql` inicializa una base vacia. No se debe volver a ejecutar sobre datos existentes
ni recrear volumenes para aplicar estos cambios.
ASP.NET Core/EF gestiona las migraciones: la migracion del dominio debe incluir tambien
funciones, triggers, indices y los cambios de firmas/retornos descritos aqui.
Editar el inicializador no actualiza automaticamente una base ya creada.
Los modelos EF existentes deben alinearse al incorporar estas modificaciones a una migracion;
no se modifica Identity ni su historial en public.

Pruebas de regresion en una base aislada PostgreSQL 18 + PostGIS 3.6:

```text
psql -v ON_ERROR_STOP=1 -d base_de_pruebas -f db/init/01_schema.sql
psql -v ON_ERROR_STOP=1 -d base_de_pruebas -f db/init/02_test.sql
```

Las pruebas revierten sus datos y DDL con ROLLBACK. El estado de su ejecucion debe
reportarse por separado; su presencia no equivale a una validacion satisfactoria.
Si cambian las reglas del diccionario public.unaccent, reconstruir los indices
que dependen de app.normalize, incluidos los de nombres, categorias y sinonimos.
