# CONTRATO FUNCIONAL Y HTTP — DIRECTORIO GEO DE NEGOCIOS

Base: /api/v1
Estado: especificación normativa para desarrollar; incluye módulo de Categorías implementado.

## 0. AUTORIDAD, ALCANCE Y STACK

Reemplaza íntegramente v1.0. Define entradas, salidas, permisos y reglas.
`Funcionalidades_Directorio_Geo_SQL_v1_3.md` establece valor y aceptación;
`README.md` describe instalación y estado actual. Los ejemplos son instancias de los
tipos definidos, no contratos alternos.

**El SQL es la fuente de verdad del modelo de datos.** `db/init/01_schema.sql` no se
modifica. Ante discrepancia entre este documento y el SQL, gana el SQL y se corrige este
documento. Los cambios de BD estrictamente necesarios viven en
`db/init/02_contrato_v1_1.sql` (incremento aparte, §12.2).

Stack inamovible: ASP.NET Core 10/C#, Identity Guid/JWT Bearer, EF Core 10,
Npgsql/NetTopologySuite, PostgreSQL 18 con PostGIS y pgRouting, Database First;
Next.js 16/React 19/Tailwind CSS 4, TypeScript, pnpm y Docker Compose.
- Mapas interactivos con Mapbox GL JS. Es la única excepción a "sin servicios externos":
  requiere token público por entorno y teselas externas.
- pgRouting permanece instalado para isócronas por tiempo futuras; no tiene uso en el
  MVP y la imagen de PostgreSQL debe incluirlo junto a PostGIS.
- Fotos BYTEA en PostgreSQL. Sin microservicios ni caché distribuida.
- **Sin cookies**: sin cookies en backend. La sesión usa solo `Authorization: Bearer`.

Alcance MVP único (desaparece la división P2). P0 = seguridad, integridad y operación;
P1 = catálogo público, administración, registro y propietarios. Total: **84 operaciones** (§1).

Fuera de alcance: reseñas, favoritos, anuncios/pagos, eventos/estadísticas,
autocompletado, suscripciones y pagos de negocio (solo sus catálogos), CRUD de
territorios, GIS avanzado/isócronas y concurrencia optimista (§2.3). Sus tablas no se
eliminan.

Las funciones SQL de dominio (`create_business_with_branch`, `add_photo`,
`add_business_photo`, `set_primary_photo`, `admin_*`, `soft_delete_*`) **se invocan**.

## 1. INVENTARIO CANÓNICO — 84 OPERACIONES

Todas las rutas usan el prefijo `/api/v1`, incluidas las de health.
Acceso: P = pública; S = cualquier sesión; M = Admin o propietario vinculado vigente
(§10); A = Admin. Id de ruta UUID salvo slug y `{code}`, que son texto. Éxito 200 salvo
indicación. Cada operación tiene `operationId` estable (`Grupo.Accion`); el OpenAPI
generado es la fuente del inventario y debe coincidir con estas tablas.

### 1.1 Operaciones específicas (30)

| # | Método | Ruta relativa | Acceso | Entrada → salida |
| --- | --- | --- | --- | --- |
| 01 | GET | `/health` | P | — → Health |
| 02 | GET | `/health/ready` | P | — → Health, 200/503 |
| 03 | POST | `/auth/register` | P | RegisterInput → User, 201 |
| 04 | POST | `/auth/login` | P | LoginInput → AuthSession |
| 05 | POST | `/auth/refresh` | S | sin body → AuthSession |
| 06 | POST | `/auth/logout` | S | sin body → 204 |
| 07 | GET | `/auth/me` | S | — → User |
| 08 | GET | `/territories` | P | query §4.3 → `Page<Territory>` |
| 09 | GET | `/branches` | P | query §5 → `Page<BranchSummary>` / GeoJSON |
| 10 | GET | `/branches/{branchId}` | P/A | UUID → PublicBranch |
| 11 | GET | `/businesses/{slug}` | P | slug → PublicBusiness |
| 12 | GET | `/photos/{photoId}` | P/M/A | UUID → binario §9 |
| 13 | GET | `/management/businesses` | M | query §7 → `Page<ManagedSummary>` |
| 14 | POST | `/management/businesses` | M* | CreateBusiness → ManagedBusiness, 201 |
| 15 | GET | `/management/businesses/{businessId}` | M | UUID → ManagedBusiness |
| 16 | PUT | `/management/businesses/{businessId}` | M | BusinessInput → ManagedBusiness |
| 17 | PUT | `/management/businesses/{businessId}/publication` | A | PublicationInput → ManagedBusiness |
| 18 | DELETE | `/management/businesses/{businessId}` | A | sin body → 204 |
| 19 | POST | `/management/businesses/{businessId}/restore` | A | RestoreInput → ManagedBusiness |
| 20 | POST | `/management/businesses/{businessId}/branches` | M | BranchInput → ManagedBusiness, 201 |
| 21 | PUT | `/management/branches/{branchId}` | M | BranchInput → ManagedBusiness |
| 22 | DELETE | `/management/branches/{branchId}` | M | sin body → 204 |
| 23 | POST | `/management/branches/{branchId}/restore` | A | sin body → ManagedBusiness |
| 24 | POST | `/management/branches/{branchId}/photos` | M | multipart §9 → ManagedBusiness, 201 |
| 25 | PUT | `/management/photos/{photoId}` | M | PhotoInput → ManagedBusiness |
| 26 | DELETE | `/management/photos/{photoId}` | M | sin body → 204 |
| 27 | GET | `/admin/businesses/{businessId}/owners` | A | query paginación → `Page<Owner>` |
| 28 | PUT | `/admin/businesses/{businessId}/owners` | A | OwnerInput → Owner |
| 29 | DELETE | `/admin/businesses/{businessId}/owners/{userId}` | A | sin body → 204 |
| 30 | GET | `/admin/audit` | A | query §11 → `Page<AuditEntry>` |

\* 14: Admin o usuario con rol BusinessOwner (§7).

### 1.2 Catálogos y parámetros (54)

Los GET de lista están siempre paginados (§2.2). Escritura siempre Admin.
Lectura pública devuelve solo activos; Admin puede usar `isActive=true|false` (un
público que envíe `isActive` recibe 400).

| Recurso | Ruta | Lectura | Operaciones | Cant. |
| --- | --- | --- | --- | --- |
| Categorías | `/categories` | P | List, Get `{id}`, Create 201, Update, Delete | 5 |
| Servicios/productos | `/service_product` | P | ídem | 5 |
| Características | `/features` | P | ídem | 5 |
| Métodos de pago | `/payment_method` | P | ídem | 5 |
| Festivos | `/holiday` | P | ídem | 5 |
| Tipos de suscripción | `/subscription_type` | A | ídem | 5 |
| Sinónimos de búsqueda | `/search_synonym` | A | ídem | 5 |
| Valores de parámetro | `/parameter_value` | A | ídem | 5 |
| Definiciones de parámetro | `/parameter_definition` | A | List, Get `{code}`, Create 201, Update | 4 |
| Estado de sucursal | `/branch_status` | P | List, Update `{code}` | 2 |
| Estado de negocio | `/business_status` | P | List, Update `{code}` | 2 |
| Estado de suscripción | `/subscription_status` | A | List, Update `{code}` | 2 |
| Estado de pago de suscripción | `/subscription_payment_status` | A | List, Update `{code}` | 2 |
| Estado de moderación de reseña | `/review_moderation_status` | A | List, Update `{code}` | 2 |

Total: 30 + 54 = **84**. No se cuentan OpenAPI/Scalar de desarrollo, HEAD/OPTIONS técnicos
ni `/Prueba` (se retira). Dos formatos de búsqueda no son dos operaciones.

## 2. CONVENCIONES TRANSVERSALES [P0]

### 2.1 Tipos, formato y validación

JSON camelCase UTF-8, `application/json` salvo GeoJSON/errores/fotos.
UUID = string canónica minúscula 8-4-4-4-12 distinta de cero. Date = YYYY-MM-DD;
Instant = ISO 8601 UTC con Z; Time = HH:mm local 00:00..23:59. Zona única
America/La_Paz. Texto plano Unicode, trim, sin HTML/control; longitud en caracteres.
Password nunca se recorta. Números finitos; JSON no convierte strings a números/bools.
Money = decimal BOB 0..999999999999.99 con hasta dos decimales (`numeric(18,2)`).
`T?` permite null; `T[]` es array; propiedades obligatorias salvo indicación expresa.
Las salidas siempre incluyen null y `[]`; `privateData` existe solo para Admin.

**Sin envoltorio de respuesta**: no existen `ApiResponse` ni `PagedResult`. Éxito = el
tipo directo (objeto, arreglo o `Page<T>`); error = ProblemDetails (§2.4).

Campo desconocido, claves JSON duplicadas, enum o query desconocidos → 400. Requeridos
omitidos → 400. PUT reemplaza campos editables/colecciones; no IDs, padre, slug,
auditoría ni publicación. No hay PATCH. GET/DELETE sin body. **POST sin body ignora el
body recibido.** UUID mal formado → 400; válido inexistente de ruta → 404. Referencia
inactiva/inexistente en query/body → 400 con error de campo. No se aceptan query en
rutas sin query declarada.
JSON máximo 1 MiB; multipart según §9; exceso → 413.

### 2.2 Paginación

`Pagination={page,pageSize,totalItems,totalPages}`; `Page<T>={items:T[],pagination}`.
**Todo GET que devuelva una lista usa `Page<T>`**, sin excepción (catálogos, estados,
territorios, propietarios, auditoría, búsqueda). Un cliente que necesite el catálogo
completo recorre páginas.
`page` predeterminado 1, ≥1; `pageSize` predeterminado 20, 1..100. offset = (page-1)·pageSize
≤ 2000 (si no → 400). totalPages = ceil(total/pageSize), 0 si total = 0. Página posterior al
final dentro del límite → 200 con items vacío.
**Total y página salen de una única consulta** (misma instantánea), contando recursos
distintos. No se exige REPEATABLE READ; páginas pedidas por separado pueden cambiar.
Colecciones anidadas en fichas tienen límites explícitos (p. ej. ≤100 sucursales).

### 2.3 Concurrencia, cabeceras y auditoría

**Concurrencia optimista (ETag/If-Match/revision) queda fuera del MVP y se implementará
después.** Hasta entonces: no hay `revision`, ETag, If-Match, 412 ni 428; rige
"última escritura gana". Cada mutación se ejecuta en **una transacción** que aplica todos
los cambios o ninguno. Ninguna respuesta del MVP debe asumir estas cabeceras; el cliente
no las envía.

Cabeceras: 201 de 14/20/24 incluyen `Location` (GET 15) y `X-Created-Resource-Id` (UUID del
negocio, sucursal o foto creada; no deducirlo por posición). 201 de catálogos incluyen
`Location` del GET por id. 201 de registro no inicia sesión y omite `Location`. CORS expone
`Location` y `X-Created-Resource-Id`.

**Auditoría desde el primer día, solo con triggers de BD.** La API nunca escribe
`app.audit_log`. Para que el trigger registre quién modifica, **toda mutación**
(incluidas las de catálogos) abre una transacción y ejecuta antes
`set_config('cofre.actor_id', '<UUID del JWT>', true)` (clave de `app.actor()` en el SQL).
Se implementa con un interceptor de EF Core y se prueba. El actor sale del JWT, nunca del
request; sin actor la BD registra el UUID de ceros, que se considera defecto. Excluir
tokens/hashes/BYTEA de snapshots y logs (la BD ya reemplaza los bytes por su SHA-256).
No generar eventos ni persistir ubicación del visitante.

### 2.4 Errores y visibilidad

RFC 9457, `application/problem+json`:
`{type:"about:blank",title,status,code,detail,traceId,errors:Record<string,string[]>}`;
`errors={}` salvo validación; mensajes en español sin SQL ni stack.
```json
{
  "type": "about:blank",
  "title": "Datos inválidos",
  "status": 400,
  "code": "validation",
  "detail": "Corrige los campos indicados.",
  "traceId": "00-e64702616a304882abc6a29852b0f013-44ed91db827af094-01",
  "errors": {"radiusM": ["Debe estar entre 1 y 50000."]}
}
```
HTTP/code: 400 validation; 401 unauthorized (`WWW-Authenticate: Bearer`); 403 forbidden;
404 not_found; 406 not_acceptable; 409 conflict; 413 payload_too_large;
415 unsupported_media_type; 429 rate_limited con `Retry-After`; 500 internal_error;
503 unavailable.
- Excepciones de triggers/constraints de BD (P0001, unicidad, FK) se mapean a 409 o 400
  por campo; nunca se filtra el texto SQL.
- 401: token ausente/inválido/vencido/revocado, usuario inactivo o bloqueado.
- 403: cuenta válida sin rol suficiente. M con rol y recurso ajeno → 404.
- No publicado/archivado/cerrado permanente → 404 público, incluidas fotos; el preview
  autenticado se rige por §9.
- Público JSON/GeoJSON/fotos: `Cache-Control: no-store`. Sin OutputCache. Toda salida
  privada/auth usa no-store. Solo HTTPS en producción.

## 3. AUTENTICACIÓN [P0]

`User={id:UUID,email,firstName,lastName,roles:string[]}`. Roles exactos `Admin`,
`BusinessOwner`, `User`, orden alfabético. Email 3..254, único normalizado
(`RequireUniqueEmail`); UserName = email normalizado. firstName/lastName 1..150. Password
12..128 con mayúscula, minúscula, dígito y símbolo; nunca trim ni log. El login acepta el
mismo rango de longitud pero no reaplica complejidad. Sin campos Identity internos en salida.

- **03 registro**: `RegisterInput={email,password,firstName,lastName}` → 201 `User` con
  `roles=["User"]`, sin sesión. Email duplicado → 409. No asigna privilegios. Una cuenta
  `User` no gestiona negocios hasta ser vinculada por un Admin (§10).
- **04 login**: `LoginInput={UserName,password}` → 200 `AuthSession`. Error/inactividad/bloqueo
  → 401 genérico sin revelar si el email existe. Identity bloquea 15 min tras 5 intentos;
  se registra `LastLoginAt`.
- `AuthSession={accessToken,tokenType:"Bearer",expiresIn:900,user:User}`.
- **JWT HS256**, secreto aleatorio ≥ 32 bytes, issuer/audience configurados; claims
  `sub` UUID, `jti` UUID, `roles`, `stp` (SHA-256 base64url del SecurityStamp), `auth_time`,
  `iat`, `exp`. Vigencia 15 minutos, tolerancia 30 s. En **cada** ruta privada se valida
  algoritmo fijo/firma/iss/aud/exp y se consulta la BD: usuario `IsActive`, sin lockout, roles
  actuales y `stp` igual al SecurityStamp vigente. Los claims no sustituyen permisos de BD.
- **05 refresh**: con Bearer **vigente** emite un `AuthSession` nuevo conservando `auth_time`.
  Si han pasado más de 12 h desde `auth_time` → 401 (nuevo login). El token vencido no
  se renueva; el frontend renueva antes de expirar y serializa las renovaciones.
- **06 logout**: rota el SecurityStamp del usuario → 204. Invalida **todos** los tokens
  vigentes del usuario (decisión de simplicidad; sin tablas de sesión).
- **07 me**: sesión activa → 200 `User`; no renueva token.
- El token vive solo en memoria o `sessionStorage`; **nunca** `localStorage`, cookie, URL ni
  logs.
- Sin confirmación de email ni recuperación pública. El operador verifica la identidad por
  contacto conocido y restablece con `UserManager.GeneratePasswordResetTokenAsync/
  ResetPasswordAsync` mediante herramienta local que pide secretos de forma interactiva
  (sin argumentos ni logs), rota el SecurityStamp y entrega la credencial temporal por canal
  externo. No se actualizan hashes por SQL.
- Sin cookies no hay CSRF. CORS: producción un origen HTTPS; Development orígenes
  explícitos; se exponen `Location` y `X-Created-Resource-Id`.
- **Límites por minuto**, ventana fija, cola 0 (3 políticas): `auth` 5/IP (login y registro);
  `público` 120/IP (resto de rutas anónimas, búsqueda y health); `privado` 120/usuario
  (toda ruta con sesión, incluidas refresh, logout y mutaciones). Forwarded headers solo
  de proxies conocidos antes de derivar la IP.

## 4. CATÁLOGOS, PARÁMETROS Y TERRITORIOS

### 4.1 Elementos de catálogo

Cada tabla del esquema `catalog` expone su propio controller; no existe `/catalogs`.
```
CatalogItem   = {id:UUID, code, name, description?, sortOrder:int, isActive:bool}
ServiceProduct= CatalogItem + {itemType:"S"|"P"}      // S servicio, P producto
Category      = CatalogItem + {parentId:UUID?}
CatalogInput  = {code:1..50 único, name:1..150, description?, sortOrder:int}
                (+ itemType en service_product; + parentId? en categories)
```
Create (201) y Update (PUT) usan `CatalogInput`; PUT reemplaza todos los campos editables,
incluido `code`, y agrega `isActive`. **Delete es baja lógica** (`active=false`; el trigger de
BD escribe `deleted_at`/`deleted_by_user_id`); PUT con `isActive:true` la restaura. DELETE ya
inactivo → 204. Código duplicado → 409. Orden `sortOrder,name,id`. Filtros de lista:
`search` (substring normalizado en `name`/`code`) e `isActive` (solo Admin).

**Categorías**: planas, el cliente arma el árbol. Filtros `parentId` (hijos directos) y
`rootOnly`, mutuamente excluyentes (combinados → 400). `parentId` debe existir y estar activo
(el trigger no valida actividad; lo hace la API); ciclo → 409. DELETE/`isActive:false` →
409 si tiene hijas activas o si dejaría sin categoría vigente a una sucursal publicada
(excepción del trigger `CATALOG_CATEGORY_COVERAGE`). Las columnas `icon` y `color` no
existen en el DDL y se omiten.

**Festivos**: `Holiday={id,name:1..150,holidayDate:Date,territoryId?,recurringAnnually,isActive}`;
`territoryId` null = nacional. Filtro `territoryId`: nacionales + los de ese territorio y sus
ancestros (misma semántica de `app.is_holiday`). Único `(holidayDate, territoryId, name)` → 409.

**Sinónimos**: `SearchSynonym={id,term:1..150,synonym:1..150,locale:1..10 (def. "es-BO"),isActive}`;
único `(term,synonym,locale)` → 409. Alimentan la búsqueda §5.

**Tipos de suscripción**: `CatalogItem` sin campos extra.

### 4.2 Estados y parámetros

`StatusItem={code,name,sortOrder,isActive}`. Los códigos están referenciados por triggers y
funciones del SQL: no se crean ni se eliminan por API. PUT `{code}` edita solo
`name` (1..150), `sortOrder` e `isActive`. En `branch_status` y `business_status`
`isActive:false` → 409. Valores sembrados: branch `active|temporarily_closed|permanently_closed`;
business `pending|published|rejected|archived`; subscription, payment y moderation según el SQL.

`ParameterDefinitionDto={code,description,dataType:"boolean"|"integer"|"numeric"|"text"|"jsonb",
defaultValue:unknown,minNumber?,maxNumber?,allowedValues?:unknown[],isActive}`. Create/Update
(sin DELETE; se desactiva con `isActive:false`); `code` inmutable tras crearse. Los
parámetros consumidos (`photo.max_bytes`, `search.default_radius_m`, `search.default_limit`,
`review.moderation_post`, `analytics.archive_after_days`) no cambian `dataType` y mantienen los
topes de `catalog.validate_parameter_data`; la validación de valor/rango la hace el trigger de
BD y se devuelve como 400 por campo.

`ParameterValueDto={id,code,value:unknown,scopeType:"global"|"branch",scopeId?,validFrom:Instant,
validTo:Instant?,isActive}`. Solo se aceptan `global` y `branch` este último únicamente para
`photo.max_bytes` (regla del trigger). Solape de vigencia → 409. Delete = baja lógica.
`validTo > validFrom`.

### 4.3 Territorios

`Territory={id,parentId?,type:"department"|"province"|"municipality"|"zone"|"neighborhood",
name,codeIne?,bounds?:number[]}` donde `bounds=[minLng,minLat,maxLng,maxLat]` o null (no
geometría). 08 query: `type` opcional; `parentId` opcional (hijos directos); `page/pageSize`.
Sin `parentId`, todos los activos según `type`. Combinación imposible por jerarquía o parent
inexistente/inactivo → 400. Orden nombre normalizado, id. Barrio puede colgar de municipio o
zona. Los departamentos raíz pertenecen a Bolivia; no hay tabla país. La carga territorial es
por scripts con procedencia (fuente, versión, fecha); sin CRUD HTTP.

## 5. BÚSQUEDA JSON/GEOJSON

09 query (opcionales salvo dependencias):
```
q            texto 1..200 (vacío = ausente)
categoryId   UUID, incluye descendientes activos
territoryId  UUID, incluye descendientes activos
lat,lng      requieren ambos
radiusM      entero 1..50000, requiere centro
bbox         minLng,minLat,maxLng,maxLat (cuatro números finitos)
openNow      true|false (def. false)
minPriceBob  Money, precio mínimo de interés
maxPriceBob  Money, precio máximo de interés (≥ minPriceBob)
page, pageSize, format = json|geojson (def. json)
```
Centro sin radio usa el parámetro `search.default_radius_m` (500). Centro/radio y bbox son
excluyentes. bbox: `-180≤minLng<maxLng≤180`, `-90≤minLat<maxLat≤90`, sin antimeridiano, ancho
≤ 13° y alto ≤ 14.5°. Sin centro/bbox → catálogo nacional paginado. Filtros en AND. Sin
`radiusKm`, alias `lon` ni coma decimal.

**Filtro de precio**: una sucursal coincide si tiene al menos un servicio/producto con precio
activo cuyo rango `[min,max]` se solapa con `[minPriceBob,maxPriceBob]` (cualquiera de los
extremos puede omitirse). Sucursales sin precios informados no coinciden cuando se filtra por
precio.

**Visibilidad** (vista `app.published_branch`): negocio `published` y sucursal `active` o
`temporarily_closed`. Una sucursal temporalmente cerrada sigue visible con `isOpen=false`.
Archivado, rechazado, pendiente o cerrada permanente nunca salen.

**Implementación**: una sola consulta SQL parametrizada `app.search_branches` (§12.2)
que devuelve la página y el total. Texto: `app.normalize` (tildes/caso), `trade_name` y
nombre de sucursal por substring literal o `pg_trgm` (`%`), más sinónimos activos `es%` cuyo
término normalizado coincide. `%`/`_` se escapan como literal. Distancia: `ST_DWithin/
ST_Distance` sobre `geography` (metros). bbox: rectángulo `geography` con `ST_Intersects`.
Radio y bbox se filtran antes de ordenar.

**Orden** (idéntico al SQL): patrocinadas primero, luego similitud del `trade_name` con `q`,
luego distancia ascendente (sin centro, nulos al final) y `branch_id`. El módulo de anuncios
está se introduce mediante sql crudo de forma manual, por lo que el orden
efectivo es similitud → distancia → id. `distanceM` es entero redondeado, null sin centro.

```
Point = {type:"Point", coordinates:[lng,lat]}
BranchStatus = active|temporarily_closed|permanently_closed
BranchSummary = {id, businessId, businessSlug, tradeName, name, address,
  territoryId?:UUID, location:Point, distanceM?:int, status:BranchStatus,
  isOpen?:bool, minPriceBob?:Money, maxPriceBob?:Money, rating?:number,
  reviewCount:int, isSponsored:bool, coverPhotoUrl?:string}
```
`minPriceBob`/`maxPriceBob` = mínimo y máximo entre los precios activos de la sucursal (null si
ninguno). `rating` es null si no hay reseñas visibles (sin calificaciones ficticias);
`reviewCount` 0 y `isSponsored` false mientras esos módulos no se usen. Portada = foto activa
con `is_primary`, luego `sortOrder,id`, como URL relativa `/api/v1/photos/{photoId}` o null.
`isOpen`: si no hay horario conocido (§6) es null; si no, `app.is_open_at(branch, evaluatedAt)`.
`openNow=true` solo incluye `true`. `evaluatedAt` es único por respuesta.

JSON: 200 `Page<BranchSummary>`, `application/json`. Ejemplo:
```json
{
  "items": [{
    "id": "b19fd3ef-2195-4e3e-83d9-420a0d4e2222",
    "businessId": "7f3b2c3a-3b4e-4e31-b6bb-1f12f3c9b101",
    "businessSlug": "la-casona",
    "tradeName": "La Casona",
    "name": "Centro",
    "address": "Calle Junín 123",
    "territoryId": "d91c91d4-0b7f-4fa4-9102-50a6b0c33333",
    "location": {"type": "Point", "coordinates": [-68.1193, -16.4897]},
    "distanceM": 1420,
    "status": "active",
    "isOpen": true,
    "minPriceBob": 25.00,
    "maxPriceBob": 90.00,
    "rating": null,
    "reviewCount": 0,
    "isSponsored": false,
    "coverPhotoUrl": null
  }],
  "pagination": {"page": 1, "pageSize": 20, "totalItems": 1, "totalPages": 1}
}
```
GeoJSON: `application/geo+json`, RFC 7946, WGS84, siempre `[lng,lat]`, sin `crs`.
`FeatureCollection={type,features:BranchFeature[],pagination}`;
`BranchFeature={type:"Feature",id:UUID,geometry:Point,properties:BranchSummary SIN id y SIN location}`.
`Feature.id = BranchSummary.id`. Vacío: `features=[]`, total 0. Sin geometrías null, altitud,
estilos ni bbox adicional. `format` elige la representación; `Accept` ausente o `*/*` la admite;
si no admite la solicitada → 406 ProblemDetails. No existen `/map` ni `/nearby`.

## 6. FICHAS Y HORARIOS

`PublicBusiness={id,slug,tradeName,description?,branches:PublicBranch[]}`. 11: slug 1..160
regex `^[a-z0-9]+(-[a-z0-9]+)*$`, exacto. Todas las sucursales visibles, principales primero,
`name,id`, máximo 100. Sin visibles → 404.

`PublicBranch = BranchSummary + {description?, isPrimary, territoryPath:TerritoryRef[],
categories:CatalogRef[], services:ServiceRef[], features:CatalogRef[], paymentMethods:CatalogRef[],
contacts:Contact[], hoursKnown:bool, openingHours:OpeningHour[], hourOverrides:HourOverride[],
holidays:Holiday[], timeZone:"America/La_Paz", evaluatedAt:Instant, photos:Photo[]}`.
- `CatalogRef={id,code,name}`; `TerritoryRef={id,type,name}` (raíz a hoja);
  `ServiceRef={id,code,name,itemType,description?,minPriceBob?,maxPriceBob?}`;
  `Photo={id,url,altText?,isPrimary,sortOrder}`.
- 10 (`GET /branches/{branchId}`): mismo DTO, `distanceM=null`. Anónimo aplica la visibilidad
  de §5; **Admin con Bearer** ve además sucursales de cualquier estado (panel de administración).
- `description` = descripción del negocio. Nunca se exponen `legalName/taxId`, propietarios ni
  datos de auditoría.
- Contactos `{type,value}`: `phone|whatsapp|email|website|facebook|instagram`, uno por tipo, ≤ 6,
  `value` 1..500. phone/whatsapp E.164 `^\+[1-9][0-9]{7,14}$`; email válido; URLs HTTPS sin
  credenciales. No se descargan URLs en servidor. Orden según el enum.
- `OpeningHour={weekday:0..6 (0 domingo),opensAt:Time,closesAt:Time,is24Hours:bool}`. ≤ 28 turnos;
  `closesAt<opensAt` cruza medianoche; sin solapes en la semana circular, intervalos `[abre,cierra)`.
  Con `is24Hours=true` ambas horas son `00:00` (24 h, sin arrastre al día siguiente); sin él
  `opensAt≠closesAt`.
- `HourOverride={date,isClosed,is24Hours,opensAt?,closesAt?,reason?:≤300}`: una por fecha, ≤ 366.
  Cerrada exige horas null y `is24Hours=false`; abierta exige ambas no nulas (`00:00` si
  `is24Hours`). Sustituye al horario semanal y al festivo.
- Calendario público: ayer a hoy+365 días según `evaluatedAt` local. Orden: horarios
  `weekday,opensAt`; overrides por fecha; festivos `fecha,name`; catálogos `name,id`; fotos
  `sortOrder,id`.
- `hoursKnown` = existe algún turno semanal activo u override activo. Si es false:
  `openingHours=[]`, `isOpen=null`. Si es true, `isOpen = app.is_open_at(branch, evaluatedAt)`,
  que ya aplica excepción de hoy, festivo (territorio y ancestros, recurrentes), turnos nocturnos
  de ayer y cierre de la sucursal (`temporarily_closed` → false). No se calcula una regla
  distinta en el navegador. No existe "cerrado toda la semana" explícito: se modela con un
  override cerrado o sin horario conocido.
- Los festivos de salida son `Holiday` expandido a `{date,name}` en la ventana; distintos por fecha/nombre.

## 7. GESTIÓN DE NEGOCIOS

**Modelo de estado (según SQL)**: el negocio solo tiene estado de **publicación**
(`pending|published|rejected|archived`); no existe estado comercial de negocio. `archived`
≡ baja lógica (el trigger escribe `deleted_at`). La sucursal tiene estado operativo
(`active|temporarily_closed|permanently_closed`); `permanently_closed` ≡ baja lógica.

13 query: `q` 1..200 (tradeName/slug, substring normalizado), `publicationStatus` opcional
(`pending|published|rejected|archived`), `deleted=false|true` (def. false; `true` equivale a
`archived`; solo Admin, propietario → 403), `page/pageSize`. Filtros en AND; orden
`updatedAt DESC, id ASC`. M solo lista vínculos propios vigentes.
`ManagedSummary={id,slug,tradeName,publicationStatus,deletedAt?,updatedAt,branchCount}`
(`branchCount` = sucursales no eliminadas).

`BusinessInput={tradeName:1..200,description?:≤5000,legalName?:≤250,taxId?:≤50}`. Vacío → null.
`taxId` único exacto, sin reglas tributarias. El propietario debe **omitir** `legalName` y
`taxId` (incluso null → 400). `CreateBusiness={business:BusinessInput,branch:BranchInput}`.
No hay `priceLevel` ni estado comercial en el negocio.

14: crea el negocio en `pending` y su primera sucursal con `isPrimary=true`, en una
transacción. Permiso: Admin o usuario con rol BusinessOwner; el propietario queda vinculado
al crear (atómico). Nunca publica. Slug automático e inmutable: ASCII minúscula con guiones,
base ≤ 140, fallback `negocio`; colisión → base + `-` + 12 hex del UUID. Único incluso entre
archivados; no se reutiliza. POST duplicado puede crear otro: el cliente deshabilita el doble
envío y no reintenta sin conocer el resultado. Sin claves de idempotencia.

```
ManagedBusiness = {id, slug, tradeName, description?, publicationStatus, createdAt,
  updatedAt?, deletedAt?, branches:ManagedBranch[], privateData?:{legalName?,taxId?}}
ManagedBranch = {id, businessId, status:BranchStatus, createdAt, deletedAt?,
  input:BranchInput, photos:Photo[]}
```
`privateData` solo Admin. `input` son los valores editables almacenados; para una sucursal
dada de baja `input.status` es `active` (valor con el que se restaura). Admin ve eliminadas;
propietario no. Orden: vigentes primero, principales primero, `name,id`. Límite de 100
vigentes y 500 históricas por negocio → 409, nunca se trunca. Fotos solo activas, ≤ 20 por
sucursal; sin binario. 15: Admin lee archivado; M solo vigente vinculado.

16 actualiza `BusinessInput`; negocio archivado → 409 (usar 19).

`PublicationInput={status:"pending"|"published"|"rejected"}`. 17 solo Admin; no acepta
`archived`. Publicar exige: nombre, descripción no vacía y slug; ≥ 1 sucursal no eliminada;
**todas** sus sucursales no eliminadas con dirección, punto y municipio válidos (§8), ≥ 1
categoría activa (el trigger diferido `*_PUBLICATION_CATEGORY` lo refuerza) y ≥ 1 contacto.
No exige precio, foto ni horario. Falta → 400 por campo. Un negocio `published` conserva las
condiciones al editarse (si no, rollback 400); no se despublica en silencio.

18 (Admin): baja lógica = `status='archived'` y todas las sucursales no eliminadas pasan a
`permanently_closed` (los triggers escriben `deleted_at/by`); no toca fotos ni propietarios.
Ya archivado → 204 sin cambio; conserva los timestamps de bajas previas.

`RestoreInput={branchIds:UUID[]}` (1..100 distintos, del negocio y eliminados). 19: solo
negocio archivado (otro estado → 409). Pasa el negocio a `pending` y restaura **solo** las
sucursales seleccionadas a `active`; las demás permanecen dadas de baja. Conserva el slug. No
restaura fotos eliminadas individualmente. Valida referencias y ubicación; conflicto → 409 o
400 por campo.

## 8. SUCURSALES Y COLECCIONES

```
ServiceInput = {serviceProductId:UUID, description?:string, minPriceBob?:Money, maxPriceBob?:Money}
BranchInput = {name:1..200, address:1..1000, territoryId:UUID, location:Point,
  status:"active"|"temporarily_closed", isPrimary:bool,
  categoryIds:UUID[], services:ServiceInput[], featureIds:UUID[], paymentMethodIds:UUID[],
  contacts:Contact[], openingHours:OpeningHour[], hourOverrides:HourOverride[]}
```
Todo obligatorio. Límites: categorías 0..10; services/features/pagos 0..30 cada uno; contactos,
horarios y overrides según §6. En `ServiceInput`, `minPriceBob` y `maxPriceBob` van **ambos
informados o ambos null**, con `min ≤ max`; precio fijo = `min = max`; ambos null = sin precio.
No existe `permanently_closed` en la entrada: la baja es DELETE (22). `isPrimary` no es único:
el SQL admite varias sucursales principales. No se editan `code`, `slug` de sucursal,
`emergency/home_service` ni `accessibility`.
```json
{
  "name": "Centro",
  "address": "Calle Junín 123",
  "territoryId": "d91c91d4-0b7f-4fa4-9102-50a6b0c33333",
  "location": {"type": "Point", "coordinates": [-68.1193, -16.4897]},
  "status": "active",
  "isPrimary": true,
  "categoryIds": ["c1b6d2e0-2b9e-4c7e-91a2-38ce1d2f1111"],
  "services": [{"serviceProductId": "5a0e2d3c-8f61-4b7a-9c10-2d4f6a8b0c12",
                "description": null, "minPriceBob": 25.00, "maxPriceBob": 90.00}],
  "featureIds": [],
  "paymentMethodIds": [],
  "contacts": [{"type": "phone", "value": "+59122000000"}],
  "openingHours": [{"weekday": 1, "opensAt": "08:00", "closesAt": "18:00", "is24Hours": false}],
  "hourOverrides": []
}
```
**Ubicación**: Point con exactamente dos coordenadas, SRID 4326. El CHECK de la tabla
(lng −70..−57, lat −23.5..−9) es solo prefiltro. `territoryId` debe ser municipio o descendiente
activo con ancestros activos; el municipio ancestro debe tener `boundary` válida con procedencia
que cubra el punto (`ST_Covers`); si la hoja tiene `boundary` también debe cubrirlo. Sin municipio
confiable → 400 por territorio. **Al mover el punto el trigger `BRANCH_TERRITORY` re-resuelve el
territorio** (el más específico con `boundary` que lo cubre) cuando la petición no cambia
`territoryId`; la respuesta devuelve el territorio almacenado. Si cambian punto y territorio, se
respeta el territorio indicado tras validarlo.

20 alta (límites 100 vigentes / 500 históricas). 21 reemplazo completo; no cambia `business_id`.
**22 baja lógica** = `status='permanently_closed'` (el trigger escribe `deleted_at/by`); también
se permite dar de baja la última sucursal (decisión del SQL). Un negocio publicado sin sucursales
visibles sigue `published` pero 11 devuelve 404 hasta tener una. Ya baja → 204. 23 (Admin,
negocio no archivado): sucursal → `active`, valida referencias/punto; ya vigente → 200 sin cambio;
no restaura el negocio.
Mutaciones de sucursal/foto sobre negocio archivado → 409 (salvo baja ya efectuada → 204); sobre
sucursal eliminada → 409 (solo preview Admin y 23). 20 nunca sobre negocio archivado.
Si `published`, no se puede dejar a una sucursal sin categoría vigente (trigger → 409).

**PUT de colecciones**, sin DELETE físico, todo en la misma transacción:
- Categorías, características, pagos: ausentes `active=false`; presentes upsert `active=true`
  por PK. Todo ID debe existir y estar activo (y la categoría sin ancestro inactivo).
- Servicios: upsert en `branch_service_product`; con precio, upsert en `service_product_price`
  (una fila activa por sucursal+servicio); sin precio, la fila de precio se desactiva; servicios
  ausentes se desactivan junto con su precio.
- Contactos: se **actualiza el valor en el lugar** (una fila activa por tipo, `is_primary=true`,
  índice `CONTACT_PRIMARY_IDX`); tipo ausente → baja lógica. La historia está en auditoría.
- Horarios semanales: se reactivan coincidencias, se insertan nuevos y se desactivan los demás;
  `openingHours=[]` → sin horario semanal.
- Excepciones: se reemplaza el calendario editable de ayer a hoy+365; las anteriores se conservan
  sin enviarse; fuera de ventana → 400; omitidas `active=false`; upsert por fecha (UNIQUE
  branch+fecha). Si el día del servidor cambia durante el guardado → 400 para recargar.

## 9. FOTOS Y PREVISUALIZACIÓN

24 multipart: `file` (uno), `altText` opcional ≤ 300 (vacío → null), `isPrimary` true/false y
`sortOrder` entero 0..999 obligatorios. Sin partes extra, URL remota ni base64. JPEG/PNG/WebP,
≥ 1 byte, 20 activas por sucursal. **Tamaño máximo = parámetro `photo.max_bytes`** (valor de la
sucursal o global, `catalog.get_parameter`; por defecto 5 MiB, tope 50 MiB), el mismo que valida
el trigger; el límite multipart es ese valor + 1 MiB. Exceso → 413; formato → 415;
corrupta/dimensiones/metadatos → 400.
Se decodifica realmente; se rechazan animadas, corruptas, dimensiones fuera de 64..8192 por eje o
> 24 megapíxeles. Se recodifica el mismo formato eliminando EXIF/GPS y se vuelve a comprobar el
tamaño. `filename` se guarda null. El trigger de firma no sustituye al decodificador. Biblioteca
.NET compatible con Linux y licencia de la organización (sin ImageSharp sin licencia comercial).

`PhotoInput={altText?,isPrimary,sortOrder}`, todo requerido. 25 solo metadatos/portada; para cambiar
bytes se sube otra y se da de baja la anterior. La primera foto de una sucursal es siempre principal.
`isPrimary=true` desmarca las demás de esa sucursal (la BD no tiene índice único; el SQL admite
varias, la API mantiene una). `false` sobre la portada sin sustituta → 409. `sortOrder` admite
empates (desempata `id`). 26 baja lógica (`active=false`); si era portada, la siguiente por
`sortOrder,id` pasa a principal; ya baja → 204. No hay rutas `/primary` ni `/order`.

12: lee los bytes solo aquí. Anónimo: foto activa de sucursal visible de negocio publicado.
Con Bearer, M también de borrador/pendiente propio; Admin también de negocio/sucursal archivados.
Foto dada de baja individualmente → siempre 404. No autorizado → 404; JWT inválido → 401. Sin
token en query ni URLs firmadas. 200 con MIME real, `Content-Length`, `X-Content-Type-Options:
nosniff`, `Content-Disposition: inline`, `Cache-Control: no-store`; sin ETag/304. Preview privado:
`fetch` con Bearer y Blob URL, liberada al desmontar el componente.

## 10. PROPIETARIOS

Registro: §3. Sin solicitudes, evidencias ni reclamos. El Admin verifica por fuera de la app
(contacto comercial conocido o entrevista) y vincula; la evidencia queda en la auditoría de BD
(quién y cuándo), no en columnas.

`OwnerInput={email}`. 28: busca el email normalizado de un usuario **activo**; inexistente o
inactivo → 400 por `email`. En una transacción: upsert en `business_owner` (`active=true`; si
estaba revocado el trigger limpia `deleted_at`) y rol `BusinessOwner` al usuario (conserva `User`).
Nuevo o reactivado → 200; vigente sin cambios → 200 sin alterar nada. Negocio archivado → 409. No
publica.
`Owner={userId,email,firstName,lastName,isActive,linkedAt:Instant,revokedAt?:Instant}`.
27: `Page<Owner>` (incluye revocados); máximo 20 vínculos históricos por negocio, la 21.ª → 409
(reactivar uno existente sí se permite). 29: `active=false` (baja lógica auditada); no borra
usuario ni quita el rol; ya revocado → 204; inexistente → 404.

**Permiso M** (se evalúa en BD en cada petición, equivale a `app.is_business_owner`): usuario
activo sin lockout, rol `BusinessOwner`, `business_owner.active` del negocio y negocio no
`archived`. Revocar o desactivar surte efecto en la siguiente petición. Admin: todas las
operaciones. Propietario: 13, 15, 16, 20–22, 24–26 y preview 12 de lo suyo; 14 con vínculo
atómico; **no** 17–19, 23, 27–29 ni 30; no edita `taxId/legalName`. Edición de un negocio
publicado entra directamente con las mismas validaciones y auditoría; el Admin puede despublicarlo.

## 11. AUDITORÍA Y SALUD

**30 `GET /admin/audit`** (Admin). La captura la hacen exclusivamente los triggers de BD
(`audit_capture_row`/`audit_capture_truncate` sobre todas las tablas de `app`, `catalog` y
`geo`, y sobre `AspNetUserRoles`, §12.2); el endpoint solo lee `app.audit_log`.
Query: `entityTable` opcional (nombre de tabla); `entityKey` opcional y exige `entityTable`;
`actorUserId` opcional UUID; `from/to` ISO UTC opcionales, juntos si alguno aparece, `from ≤ to`,
máximo 366 días (sin fechas: últimos 30 días hasta ahora); `page/pageSize`. Rango `[from,to]`.
Orden `changedAt DESC, id DESC`.
```
AuditEntry = {id:string (BIGINT decimal), entitySchema, entityTable, entityKey?:string,
  operation:"I"|"U"|"D"|"T", actorUserId:UUID, changedAt:Instant}
```
`entityKey` = `id` del registro; si no existe `id`, `code`, `branch_id` o `UserId` (en ese orden)
del snapshot. Bajas y restauraciones son `U`. **Nunca se devuelven `old_value`/`new_value`.**
`actorUserId` con UUID de ceros indica cambio sin actor (defecto o proceso del sistema).
No hay dashboard ni métricas.

`Health={status:"ok"|"unavailable"}`. 01 `/health`: proceso sin BD → 200 ok. 02 `/health/ready`:
comprueba conexión, extensiones (PostGIS, pg_trgm, unaccent, btree_gist, pgRouting) y versión de
esquema esperada → 200 ok, si no 503 unavailable. Excepción explícita a ProblemDetails. Sin
detalles internos. Compose usa ready (pg_isready no valida aplicación ni esquema).

## 12. PERSISTENCIA

### 12.1 Línea base (ya existe en `01_schema.sql`)

- Identity y `__EFMigrationsHistory` están en el baseline. No ejecutar
  `backend/Data/Migrations/IdentityMigration.sql` encima. Sin `EnsureCreated/Migrate` en arranque.
- Un `AppDbContext` Identity Guid/Database First; personalizaciones en `partial`.
- Estados: `business.status` ∈ catálogo `business_status`; `branch.status` ∈ `branch_status`; el
  trigger `app.touch` asocia la baja lógica a `archived` / `permanently_closed` y la restauración a
  su salida. Las demás tablas usan `active=false`.
- Sin `business.revision`, `branch.hours_known`, `owner_identity`, sesiones ni tabla de control
  de migraciones: **no son parte de este contrato**.
- `branch_hour_override.active` ya existe. `audit_log` ya guarda `created_by_user_id`
  (actor) y reemplaza bytes de fotos por su SHA-256.
- Índices de principal único (sucursal/foto) permanecen sin habilitar: se admiten varias principales.

### 12.2 Incremento `db/init/02_contrato_v1_1.sql` (archivo aparte)

1. **Identity fuera de las políticas de borrado/truncado**: elimina `policy_no_delete` y
   `policy_no_truncate` de `AspNet*` y `__EFMigrationsHistory`, y hace que el event trigger no
   las vuelva a instalar en el esquema `public`. No se vuelve a ejecutar
   `install_table_policies()` sin argumentos tras aplicarlo.
2. **Auditoría de asignación de roles**: trigger `audit_capture_row` sobre `AspNetUserRoles`
   (sin datos sensibles). `AspNetUsers` no se audita (contiene hash de contraseña).
3. **Índices de lectura de auditoría** sobre `app.audit_log` (tabla+clave+fecha, actor+fecha).
4. **`app.search_branches`**: variante de `app.find_branches` con total en la misma consulta,
   filtros de precio, precio mín/máx por sucursal y `rating` nulo sin reseñas. `find_branches`
   no se modifica.

### 12.3 Requisitos de despliegue (no incluidos en el incremento)

- No sembrar roles `Admin`, `BusinessOwner`, `User` (por el backend al iniciar o script
  de despliegue).
- Índice único sobre `NormalizedEmail` recomendado además de `RequireUniqueEmail`
  (revisar duplicados antes).
- Cargas territoriales y festivos por scripts con fuente, versión, fecha y año revisado; SRID,
  validez, jerarquía y cobertura comprobados. Sin barrios inventados.
- Copia de seguridad y ensayo de restauración antes de aplicar incrementos; un error aborta todo.
  `ready` compara la versión de esquema esperada por el backend desplegado; las peticiones de
  salud nunca aplican cambios.

## 13. FRONTEND (Next.js) Y CRITERIOS DE TERMINADO

Next.js 16 (App Router), React 19, Tailwind 4, TypeScript, pnpm. Producción: un origen HTTPS,
proxy `/api/v1` hacia el backend y el resto Next.js. Variables: `NEXT_PUBLIC_API_BASE` (cliente),
`API_BASE_URL` (SSR, accesible en red) y `NEXT_PUBLIC_MAPBOX_TOKEN` (token público; sin él la
página funciona en modo lista). Sin cookies ni middleware de sesión: las páginas privadas son
componentes cliente que usan el token en memoria; el SSR público no necesita credenciales. La
protección de rutas del cliente no sustituye a la API.

Páginas: `/` buscador, `/businesses/[slug]` ficha, `/login`, `/register`, `/management`
listado, `/management/businesses/new`, `/management/businesses/[id]` edición,
`/admin/businesses/[id]/owners`, `/admin/audit`, `/admin/catalogs/[recurso]` (CRUD de catálogos,
festivos, sinónimos, tipos de suscripción), `/admin/parameters` (definiciones y valores) y
`/admin/statuses`. Sin panel de reclamos.

Filtros en la URL; cambiar un filtro reinicia `page=1`. Un solo GET GeoJSON alimenta lista y mapa
Mapbox GL JS; se muestra total y página, no todos los marcadores. Radio en metros hacia la API y
km en la interfaz; no mostrar distancia 0 si es null. "Buscar en esta zona" quita centro/radio y
usa `bbox`. Permiso geográfico denegado → pin o municipio. La lista funciona sin teselas.
Direcciones: `https://www.google.com/maps/dir/?api=1&destination=lat,lng` (sin origen del
visitante, sin rutas propias); enlaces externos seguros en nueva pestaña. Filtros de precio
`minPriceBob/maxPriceBob` en el buscador principal.

Formularios PUT cargan el GET y construyen el DTO de entrada; no reenvían la salida como body.
400 muestra errores por campo; 401 intenta un único refresh compartido (§3) y, si falla, vuelve
a login. No se reintentan mutaciones con resultado de red incierto: se consulta primero. Los
límites se aplican en API y formulario; campos vacíos nullable se envían como null; sin horas con
segundos, fechas locales en timestamps UTC ni dinero con más de dos decimales. Carga/vacío/error,
móvil/teclado y formatos de dinero/horario son parte de la entrega.

Pruebas obligatorias con PostgreSQL 18/PostGIS/pgRouting reales:
- Todas las rutas, métodos, DTO, cabeceras, permisos y límites; OpenAPI con esquemas concretos
  (JSON, GeoJSON, multipart, binarios, ProblemDetails). Inventario de **84 operaciones** sin rutas
  duplicadas ni funciones sin interfaz.
- Paginación en todo GET de lista (total y página coherentes en una consulta, offset > 2000).
- Mismos IDs/orden/paginación en JSON y GeoJSON, `[lng,lat]`, radios 1/10000/50000, bbox y bordes,
  centro+bbox → 400, descendientes, filtro de precio, conteo sin duplicados.
- Atomicidad por mutación; baja/restauración selectiva sin resucitar sucursales no elegidas; baja
  de la última sucursal; negocio archivado/rechazado/pendiente oculto; temporal visible con
  `isOpen=false`; datos privados ausentes en respuestas públicas.
- Horario desconocido, turnos nocturnos, 24 h, domingo, festivo, override abierto/cerrado.
- Auth: login, lockout, refresh (vigente/vencido/12 h), logout invalida todos los tokens,
  `IsActive`, propietario A no edita B, revocación efectiva en la siguiente petición, sin
  privilegios por registro, rate limits (3 políticas).
- **Auditoría**: cada mutación (negocios, sucursales, catálogos, propietarios, roles) deja fila
  con el `actorUserId` correcto y ninguna con UUID de ceros; el endpoint nunca expone valores.
- Fotos: firma falsa, corrupta, dimensiones, tamaño según `photo.max_bytes`, EXIF eliminado,
  límite por sucursal, portada y preview autorizado; geolocalización denegada y teselas caídas.

Compilar no sustituye aceptación. No afirmar pruebas aprobadas por este texto.

## 14. REFERENCIAS NORMATIVAS

- [GeoJSON: RFC 7946](https://www.rfc-editor.org/rfc/rfc7946)
- [Problem Details: RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)
- [PostGIS: ST_DWithin](https://postgis.net/docs/ST_DWithin.html)
- [pgRouting](https://pgrouting.org/)

## 15. REGLAS COMPLEMENTARIAS

- Todo ID de catálogo en `BranchInput` existe y está activo; no se acepta categoría con ancestro
  inactivo ni territorio con ancestros inactivos. Lecturas públicas solo muestran referencias
  activas. El mantenimiento de catálogo que dé de baja un elemento en uso debe resolver las fichas
  afectadas en la misma transacción (los triggers rechazan dejar publicada una sucursal sin
  categoría vigente).
- Cambiar una sucursal a otro estado no la elimina; solo DELETE (22) la da de baja. `isOpen` y
  filtros usan el estado propio de la sucursal.
- Lectura de foto de negocio publicado con sucursal temporalmente cerrada sigue siendo pública;
  archivado o cerrada permanente exige permiso de preview.
- `Override.reason` y `description` vacíos → null (borrador); publicar exige descripción. Longitudes:
  catálogo `code` 50 y `name` 150; territorio `name` 150 y `codeIne` 30; `Holiday.name` 150.
- No hay sufijos de tamaño ni transformación de fotos. HEAD/OPTIONS técnicos no suman rutas
  funcionales. Health solo produce su JSON declarado.
- Las respuestas 201 de sucursal/foto contienen el agregado completo; el recurso nuevo se
  identifica con `X-Created-Resource-Id`.
- La autorización se comprueba antes de aplicar cambios y se evalúa contra la BD dentro de la
  transacción de edición.
- OpenAPI generado se versiona con los nombres de tipos de este documento y se mantiene en la misma
  revisión que backend y frontend. Los tipos definidos aquí son normativos; no usar diccionarios
  libres en lugar de DTO.
- **Post-MVP (diseño reservado)**: concurrencia optimista con ETag/If-Match (412/428), columna de
  revisión en `business` y bloqueo de agregado; isócronas con pgRouting; módulos de reseñas,
  favoritos, anuncios, eventos y suscripciones de negocio.