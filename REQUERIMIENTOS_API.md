# CONTRATO FUNCIONAL Y HTTP — DIRECTORIO GEO DE NEGOCIOS

Versión 2.0 | Fecha: 2026-11-09 | Base: /api/v1
Estado: especificación normativa para desarrollar; NO API implementada.

## 0. AUTORIDAD, ALCANCE Y STACK


Reemplaza íntegramente los contratos anteriores. Define entradas, salidas,
permisos y reglas. [Funcionalidades_Directorio_Geo_SQL_v1_3.md](Funcionalidades_Directorio_Geo_SQL_v1_3.md) establece el valor
y aceptación; [README.md](README.md) describe instalación y estado actual. Los ejemplos son
instancias de los tipos definidos, no contratos alternos.

Stack inamovible: ASP.NET Core 10/C#, Identity Guid/JWT Bearer, EF Core 10,
Npgsql/NetTopologySuite, PostgreSQL 18/PostGIS, Database First; Nuxt 4/Vue 3/
TypeScript, pnpm y Docker Compose. Fotos BYTEA en PostgreSQL. Sin motor de rutas,
almacenamiento externo obligatorio, microservicios, colas ni caché distribuida.

P0 = seguridad, integridad y operación. P1 = catálogo público y administración.
P0/P1 = MVP: 27 operaciones. P2 = registro y propietarios: 4 adicionales, 31 total.
P2 reutiliza rutas management; no duplica rutas para owner y admin.
Fuera de P0/P1/P2: reseñas, favoritos, anuncios/pagos, eventos/estadísticas,
autocompletado, importaciones masivas, CRUD genérico de catálogos/parámetros,
contornos territoriales y GIS avanzado. Sus tablas no se eliminan. No hay
endpoints, workers ni contratos parciales para esos módulos.

## 1. INVENTARIO CANÓNICO — 27 OPERACIONES MVP + 4 P2


Todas las rutas usan el prefijo /api/v1, INCLUIDAS las rutas health.
P=pública; A=Admin; M=Admin o propietario asociado vigente en P2.
En P1 toda M exige Admin. Id de ruta UUID salvo slug, que es texto.
Por defecto éxito 200; 201/204 se indican. Entradas/salidas tipadas en §2..11.

| ID | Método | Ruta relativa | Acceso | Entrada → salida |
| --- | --- | --- | --- | --- |
| 01 | `GET` | `/catalogs` | P | ninguna -> Catalogs |
| 02 | `GET` | `/territories` | P | query §4 -> `Page<Territory>` |
| 03 | `GET` | `/branches` | P | query §5 -> `Page<BranchSummary>`/GeoJSON |
| 04 | `GET` | `/businesses/{slug}` | P | slug -> PublicBusiness |
| 05 | `GET` | `/branches/{branchId}` | P | UUID -> PublicBranch |
| 06 | `GET` | `/photos/{photoId}` | P/M | UUID -> binario §9 |
| 07 | `POST` | `/auth/login` | P | LoginInput -> AuthSession |
| 08 | `POST` | `/auth/refresh` | P | cookie, sin body -> AuthSession |
| 09 | `POST` | `/auth/logout` | P | cookie, sin body -> 204 |
| 10 | `GET` | `/auth/me` | sesión | ninguna -> User |
| 11 | `GET` | `/management/businesses` | M | query §7 -> `Page<ManagedSummary>` |
| 12 | `POST` | `/management/businesses` | M | CreateBusiness -> ManagedBusiness, 201 |
| 13 | `GET` | `/management/businesses/{businessId}` | M | UUID -> ManagedBusiness |
| 14 | `PUT` | `/management/businesses/{businessId}` | M | BusinessInput -> ManagedBusiness |
| 15 | `PUT` | `/management/businesses/{businessId}/publication` | A | PublicationInput -> ManagedBusiness |
| 16 | `DELETE` | `/management/businesses/{businessId}` | A | sin body -> 204 |
| 17 | `POST` | `/management/businesses/{businessId}/restore` | A | RestoreInput -> ManagedBusiness |
| 18 | `POST` | `/management/businesses/{businessId}/branches` | M | BranchInput -> ManagedBusiness, 201 |
| 19 | `PUT` | `/management/branches/{branchId}` | M | BranchInput -> ManagedBusiness |
| 20 | `DELETE` | `/management/branches/{branchId}` | M | sin body -> 204 |
| 21 | `POST` | `/management/branches/{branchId}/restore` | A | sin body -> ManagedBusiness |
| 22 | `POST` | `/management/branches/{branchId}/photos` | M | multipart §9 -> ManagedBusiness, 201 |
| 23 | `PUT` | `/management/photos/{photoId}` | M | PhotoInput -> ManagedBusiness |
| 24 | `DELETE` | `/management/photos/{photoId}` | M | sin body -> 204 |
| 25 | `GET` | `/admin/audit-logs` | A | query §10 -> `Page<AuditEntry>` |
| 26 | `GET` | `/health` | P | ninguna -> Health |
| 27 | `GET` | `/health/ready` | P | ninguna -> Health, 200/503 |
| 28 | `POST` | `/auth/register` | P/P2 | RegisterInput -> User, 201 |
| 29 | `GET` | `/admin/businesses/{businessId}/owners` | A/P2 | ninguna -> OwnerList |
| 30 | `PUT` | `/admin/businesses/{businessId}/owners` | A/P2 | OwnerInput -> Owner |
| 31 | `DELETE` | `/admin/businesses/{businessId}/owners/{userId}` | A/P2 | sin body -> 204 |

No se cuentan OpenAPI/Scalar de desarrollo ni /Prueba, que se retira. Dos formatos
de búsqueda no son dos operaciones. P1 no registra las rutas 28..31 (404).

## 2. CONVENCIONES TRANSVERSALES [P0]


### 2.1 Tipos, formato y validación

JSON camelCase UTF-8, application/json salvo GeoJSON/errores/fotos.
UUID=string canónica minúscula 8-4-4-4-12 distinta de cero; Guid Identity.
Date=YYYY-MM-DD; Instant=ISO 8601 UTC con Z; Time=HH:mm local 00:00..23:59.
Zona única America/La_Paz. Texto plano Unicode, trim, sin HTML/control, longitud
en caracteres Unicode. No interpretar texto como markup. Password nunca trim.
Números finitos; JSON no convierte strings a números/bools. Money=número decimal
BOB 0..999999999999.99, hasta dos decimales; C# decimal/PostgreSQL numeric.
T? permite null; T[] es array; propiedades obligatorias salvo indicación expresa.
Salidas siempre incluyen null y []; excepción: privateData existe solo para Admin.
Arrays sin repetidos por ID/tipo/fecha según contrato. Campo desconocido, JSON
con claves duplicadas, enum o query desconocidos ->400. Campos requeridos omitidos
->400. PUT reemplaza campos editables/colecciones; no IDs, padre, slug, auditoría
ni publicación. No hay PATCH/JSON Patch. GET/DELETE sin body; POST sin body exige
longitud cero, no {}. UUID mal formado ->400; válido inexistente de ruta ->404.
Referencia inactiva/inexistente en query/body ->400, con error de campo.
JSON máximo 1 MiB; multipart 6 MiB, archivo 5 MiB; exceso ->413.

### 2.2 Paginación

Pagination={page:int,pageSize:int,totalItems:int,totalPages:int}.
`Page<T>`={items:T[],pagination:Pagination}.
page predeterminado 1, >=1; pageSize predeterminado 20, 1..100.
offset=(page-1)*pageSize máximo 2000; exceso ->400. totalPages=ceil(total/pageSize),
0 si total=0. Página posterior al final dentro del límite ->200 items vacío.
Contar recursos distintos, no joins multiplicados. Conteo y página en la misma
instantánea REPEATABLE READ de lectura; páginas solicitadas por separado pueden
cambiar con nuevas publicaciones. Listas no paginadas tienen límites explícitos.

### 2.3 Concurrencia, cabeceras y auditoría

Agregado business con sucursales/fotos/asociaciones. Añadir revision BIGINT >=1
DEFAULT 1. Cada mutación bloquea business SELECT FOR UPDATE, comprueba permiso
y revisión bajo bloqueo, aplica todo y aumenta revision una vez por cambio
efectivo y actualiza business.updated_at, también por cambios en hijos. No incrementar
en reintento sin efecto. Mantenimiento SQL sigue regla.
Mutaciones autorizadas bloquean usuario antes de business, siempre en ese orden;
revocar/desactivar usuario usa el mismo bloqueo para evitar carreras.

GET13 y todas las respuestas ManagedBusiness incluyen ETag fuerte
"business:{uuid}:v{revision}" y Cache-Control:no-store. revision en JSON es string
decimal para JavaScript. No usar updatedAt como versión.
If-Match obligatorio en IDs 14..24 y 30..31: usa ETag del negocio padre también
para sucursal/foto/propietario. Falta ->428; formato inválido, wildcard o varios
ETags ->400; obsoleto ->412. No reintentar sobrescritura automáticamente.
GET29 y PUT30 incluyen ETag del negocio. Respuestas 204 incluyen ETag resultante sin body.
Alta12 no requiere If-Match. No enviar actor/role/verified/isAdmin/revision en body.
201 de 12/18/22 incluye Location de GET13 y ManagedBusiness completo. Para identificar
la sucursal/foto creada se añade cabecera X-Created-Resource-Id: UUID del recurso
nuevo (también en 12, UUID del negocio). Exponer esa cabecera y ETag por CORS.
201 registro no inicia sesión; omitir Location porque no hay GET público del usuario.

Actor derivado de JWT, nunca request. La clave transaccional definitiva es
bolivianegocios.actor_id, usada por app.actor() de la línea base; set_config con true
para no filtrar identidad al reutilizar conexión. Auditoría dentro de transacción,
incluidos contactos/horas/relaciones. Excluir tokens/hashes/BYTEA de snapshots/logs.
No generar eventos ni persistir ubicación del visitante.

### 2.4 Errores y visibilidad

RFC 9457, application/problem+json, tipo uniforme:
{type:"about:blank",title:string,status:int,code:string,detail:string,
 traceId:string,errors:`Record<string,string[]>`}.
errors={} salvo validación; mensajes españoles sin SQL ni stack.
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
HTTP/code: 400 validation; 401 unauthorized (WWW-Authenticate:Bearer);
403 forbidden; 404 not_found; 406 not_acceptable; 409 conflict;
412 stale_version; 413 payload_too_large; 415 unsupported_media_type;
428 precondition_required; 429 rate_limited con Retry-After segundos;
500 internal_error; 503 unavailable. 409 detalla última sucursal, unicidad,
límite operativo o transición incompatible. Publicación incompleta ->400 por campo.
401: token ausente/inválido/revocado, usuario inactivo/bloqueado. 403 cuenta válida
sin rol. M con rol y recurso ajeno ->404. No publicado/eliminado/cerrado permanente
->404 público, incluidas fotos; preview autenticado §9.
Público JSON/GeoJSON/fotos: Cache-Control:no-store. Sin OutputCache en MVP.
Retirar afecta nuevas consultas, no borra copias vistas antes por el visitante.
Todas las salidas privadas/auth usan no-store. Servir solo por HTTPS en producción.

## 3. AUTENTICACIÓN [P0, REGISTRO P2]


User={id:UUID,email:string,firstName:string,lastName:string,roles:string[]}.
Roles exactos Admin, BusinessOwner, User; orden alfabético. No Moderator.
Email 3..254 formato email, único normalizado mediante índice DB;
UserName=email normalizado. firstName/lastName 1..150. Password 12..128,
mayúscula, minúscula, dígito y símbolo; nunca trim ni log. Sin campos Identity
internos en salida. No asignar privilegios desde registro. Password de login acepta el mismo rango
de longitud pero no reaplica reglas de complejidad a contraseñas ya existentes.
LoginInput={email:string,password:string}.
07: credenciales correctas/cuenta activa ->200 AuthSession y cookie refresh.
Error/inactividad/bloqueo ->401 genérico sin revelar si existe email.
Identity bloquea 15 minutos tras 5 intentos; registrar LastLoginAt.
En P1 solo cuentas Admin creadas privadamente pueden login; P2 cualquier activa.
AuthSession={accessToken:string,tokenType:"Bearer",expiresIn:900,user:User}.
JWT HS256, secreto aleatorio mínimo 32 bytes, issuer/audience configurados;
claims sub UUID,sid UUID,jti UUID,roles,iat,exp. Vigencia 15 minutos, tolerancia
30 s. Validar algoritmo fijo/firma/iss/aud/exp y sid activo en CADA ruta privada.
Consultar IsActive/lockout/roles actuales; claims no sustituyen permisos de DB.

Refresh: 32 bytes criptográficos base64url, hash SHA-256 en DB, nunca texto claro.
Cookie dg_refresh, HttpOnly, Secure, SameSite=Strict, Path=/api/v1/auth, sin Domain.
Secure=false solo Development HTTP local. Familia vence a los 30 días del login,
sin extensión por renovación. Cookie Max-Age corresponde al tiempo restante.
08: cookie vigente ->rotación atómica, 200 AuthSession y cookie nueva. Sin cookie,
expirada/revocada ->401 y eliminar cookie. Reutilizar consumido revoca toda
familia, incluidos JWT por sid. Bloquear familia: una sola renovación gana;
frontend serializa refresh. Conservar hashes consumidos hasta vencer familia.
09: con/sin cookie ->204, revocar familia reconocida y eliminar misma cookie/path.
Sin Bearer obligatorio; refresh solo cookie, jamás en JSON/URL. Access token solo
en memoria, nunca localStorage ni cookie.
10: sesión Bearer activa ->200 User, no renueva token.
28/P2: RegisterInput={email,password,firstName,lastName}; 201 User, roles=[User],
sin sesión automática. Email duplicado ->409. Ruta inexistente en P1.

No confirmación email ni recuperación pública por correo en alcance; cuentas
sin privilegios no gestionan negocios. Operador verifica identidad mediante
contacto comercial conocido y restablece privadamente con Identity UserManager
GeneratePasswordResetTokenAsync/ResetPasswordAsync, revoca sesiones y entrega
credencial temporal por canal acordado fuera de app. La herramienta local de
mantenimiento deberá pedir secretos interactivamente, sin argumentos/logs;
no actualizar hashes SQL ni abrir endpoint público de reset sin verificación.

CSRF: login/registro con application/json. Login/refresh/logout/registro exigen
Origin permitido incluso sin body (ausente/distinto ->403). Cliente técnico
incluye Origin permitido. Navegador credentials:include. Producción un origen
HTTPS; Development orígenes explícitos CORS con credentials. Bearer no se
sustituye por cookie. Exponer cabeceras ETag, X-Created-Resource-Id y Location.
Límites por minuto, ventana fija, cola 0: login/registro 5/IP; refresh/logout
30/IP; búsqueda 60/IP; otros públicos 120/IP; mutaciones privadas 30/usuario;
lecturas privadas 120/usuario. Auth/me sin autenticación usa límite público.
Forwarded headers solo de proxies conocidos antes de derivar IP. Health 120/IP.

## 4. CATÁLOGOS Y TERRITORIOS [P1]


CatalogItem={id:UUID,code:string,name:string,sortOrder:int}.
Category=CatalogItem + {parentId:UUID?}.
Catalogs={categories:Category[],services:CatalogItem[],features:CatalogItem[],
 paymentMethods:CatalogItem[]}.
01: sin query, solo activos con ancestros activos; sortOrder,name,id.
Categorías planas; cliente construye árbol. Máximo operativo 500 por colección;
carga inicial debe cumplirlo, no truncar. Sin CRUD: scripts versionados con actor,
sin desactivar referencias que invaliden fichas publicadas. Filtros solo por UUID,
no mezclar code e ID en el mismo parámetro.

Territory={id:UUID,parentId:UUID?,type:string,name:string,codeIne:string?,
 bounds:number[]?}.
type: department|province|municipality|zone|neighborhood.
bounds=[minLng,minLat,maxLng,maxLat] de boundary o null; no geometría ni GeoJSON.
02 query: type opcional enum; parentId opcional UUID (hijos DIRECTOS);
page/pageSize. Sin parentId todos activos según type. Ambos deben cumplir
jerarquía posible o400; parent inexistente/inactivo ->400. Orden nombre
normalizado,id. Solo activos con ancestros activos. Barrio puede colgar de
municipio o zona. Departamentos raíz cargados pertenecen a Bolivia; no tabla país.

## 5. BÚSQUEDA JSON/GEOJSON [P1]


03 query (opcionales salvo dependencias):
 q           texto trim 1..200; vacío/blancos equivale a ausencia.
 categoryId  UUID activo, incluye descendientes activos.
 territoryId UUID activo, incluye descendientes activos.
 lat         número [-90,90], requiere lng.
 lng         número [-180,180], requiere lat.
 radiusM     entero 1..50000, requiere centro; con centro omitido ->400.
 bbox        string minLng,minLat,maxLng,maxLat, cuatro números finitos.
 openNow     true|false, predeterminado false.
 page        entero, predeterminado1, límites §2.
 pageSize    entero, predeterminado20, límites §2.
 format      json|geojson, predeterminado json.

Centro sin radio aplica10000m. Centro/radio y bbox EXCLUYENTES.
bbox -180<=minLng<maxLng<=180, -90<=minLat<maxLat<=90; sin antimeridiano;
ancho máximo13 grados, alto14.5 (caja operativa Bolivia). Radio/bbox fuera de
Bolivia es consulta válida potencialmente vacía. No confundir con validación de
alta. Sin centro/bbox ->catálogo nacional paginado. Filtros AND. No radiusKm,
lon alias ni coma decimal. Boolean query solo true/false; int decimal base10.

Visibilidad: business.published, business/branch.deleted_at NULL, ambos status
no permanently_closed; territorio/ancestros activos y ubicación validada.
Temporarily_closed sigue visible, isOpen=false. Cierre temporal del padre aplica
al estado efectivo de sucursal.
Texto: app.normalize (tildes/caso), trade_name y branch.name; substring literal o
similarity pg_trgm >=0.3; más sinónimos activos es-BO con term normalizado=q.
Escapar %/_ como literal, parametrizar SQL. Sin anuncios/reseñas en orden.
Con centro: distancia sin redondear ASC,id ASC. Sin centro: normalize(tradeName),
normalize(name),id ASC. distanceM entero redondeado para salida, null sin centro.
ST_DWithin/ST_Distance geography en metros. bbox usa rectángulo geometry4326,
ST_Covers sobre punto geometry (incluye borde), con prefiltrado espacial indexado;
no transformar lados lat/lng en arcos geográficos. Filtrar radio antes de ordenar.

Point={type:"Point",coordinates:[lng:number,lat:number]}.
BusinessStatus=active|temporarily_closed|permanently_closed.
BranchSummary={id:UUID,businessId:UUID,businessSlug:string,tradeName:string,
 name:string,address:string,territoryId:UUID,location:Point,distanceM:int?,
 status:BusinessStatus,isOpen:bool?,priceLevel:int?,coverPhotoUrl:string?}.
status efectivo: temporal si cualquier padre/sucursal temporal; permanente nunca
sale público. priceLevel=sucursal si existe, si no negocio;1..4 o null. Portada
activa URL relativa /api/v1/photos/{photoId} o null. Sin calificaciones ficticias.
JSON:200 `Page<BranchSummary>`, application/json. Ejemplo:
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
    "priceLevel": 2,
    "coverPhotoUrl": null
  }],
  "pagination": {"page": 1, "pageSize": 20, "totalItems": 1, "totalPages": 1}
}
```
GeoJSON:application/geo+json, RFC7946 WGS84 grados, SIEMPRE [lng,lat], sin crs.
FeatureCollection={type:"FeatureCollection",features:BranchFeature[],
 pagination:Pagination}; pagination miembro adicional con misma semántica JSON.
BranchFeature={type:"Feature",id:UUID,geometry:Point,
 properties:BranchSummary SIN id y SIN location}.
Sin duplicar coordenadas ni geometrías null, altitud, estilos o bbox adicional.
Feature.id=BranchSummary.id. Vacío features=[], total0. Ejemplo de la misma fila:
```json
{
  "type": "FeatureCollection",
  "features": [{
    "type": "Feature",
    "id": "b19fd3ef-2195-4e3e-83d9-420a0d4e2222",
    "geometry": {"type": "Point", "coordinates": [-68.1193, -16.4897]},
    "properties": {
      "businessId": "7f3b2c3a-3b4e-4e31-b6bb-1f12f3c9b101",
      "businessSlug": "la-casona",
      "tradeName": "La Casona",
      "name": "Centro",
      "address": "Calle Junín 123",
      "territoryId": "d91c91d4-0b7f-4fa4-9102-50a6b0c33333",
      "distanceM": 1420,
      "status": "active",
      "isOpen": true,
      "priceLevel": 2,
      "coverPhotoUrl": null
    }
  }],
  "pagination": {"page": 1, "pageSize": 20, "totalItems": 1, "totalPages": 1}
}
```
format elige representación. Accept ausente o */* la admite; si no admite la
solicitada ->406 ProblemDetails. Resto rutas JSON solo JSON. No /map ni /nearby.

## 6. FICHAS Y HORARIOS [P1]


PublicBusiness={id:UUID,slug:string,tradeName:string,description:string?,
 status:BusinessStatus,priceLevel:int?,branches:PublicBranch[]}.
04: slug1..160 regex ^[a-z0-9]+(-[a-z0-9]+)*$, exacto, sin query/body. Todas las
sucursales visibles, principal primero,name,id, máximo100. Sin visibles ->404.
PublicBranch=BranchSummary + {description:string?,isPrimary:bool,
 businessStatus:BusinessStatus,branchStatus:BusinessStatus,
 territoryPath:TerritoryRef[],categories:CatalogRef[],services:CatalogRef[],
 features:CatalogRef[],paymentMethods:CatalogRef[],contacts:Contact[],
 hoursKnown:bool,openingHours:OpeningHour[],hourOverrides:HourOverride[],
 holidays:Holiday[],timeZone:"America/La_Paz",evaluatedAt:Instant,
 minPriceBob:Money?,maxPriceBob:Money?,photos:Photo[]}.
CatalogRef={id:UUID,code:string,name:string}.
TerritoryRef={id:UUID,type:string,name:string}, raíz a hoja.
Photo={id:UUID,url:string,altText:string?,isPrimary:bool,sortOrder:int}.
Contact={type:string,value:string}; phone|whatsapp|email|website|facebook|instagram.
Una fila por tipo, máximo 6. value no vacío y hasta 500 caracteres; phone/whatsapp E.164 ^\+[1-9][0-9]{7,14}$; email válido;
URLs HTTPS sin credenciales, host válido, <=500. No descargar URLs en servidor.
UI construye tel:/mailto:/wa.me. Orden contactos según enum anterior.

OpeningHour={weekday:int,opensAt:Time,closesAt:Time},0domingo..6sábado.
Máximo28 turnos semanales, opensAt!=closesAt. closesAt<opensAt cruza medianoche.
Sin solapes en semana circular, incluido sábado/domingo; intervalos [abre,cierra).
24h se representa por turnos adyacentes00:00-12:00 y12:00-00:00, no horas iguales.
HourOverride={date:Date,isClosed:bool,opensAt:Time?,closesAt:Time?,reason:string?}.
Una/fecha, máximo366, reason<=300. Cerrada exige horas null; abierta ambas
no nulas diferentes, una franja con cruce permitido. Sustituye semanal/festivo.
Holiday={date:Date,name:string}; nacionales o ancestros territoriales, expandir
recurrentes en ventana de salida, distintos por fecha/nombre.
Calendario público: ayer hasta hoy+365 días inclusive, según evaluatedAt local.
Horarios orden weekday,opensAt; overrides fecha; holidays fecha,name; catálogos
nombre,id; fotos sortOrder,id. EvaluatedAt único por respuesta, también búsqueda.

hoursKnown=false exige openingHours=[]; isOpen=null sin excepción hoy ni festivo.
hoursKnown=true con [] significa cerrado toda semana. Temporal fuerza false.
Festivo sin override y override cerrado hoy fuerzan false TODA fecha, anulando
arrastre previo. En otros casos evaluar hoy y arrastre de ayer según regla de
ayer. Apertura explícita hoy puede coexistir con arrastre válido; alguna franja
abierta ->true. Sin franja, false si hoursKnown o excepción hoy, null si desconocido.
openNow solo incluye true. No calcular una regla distinta en navegador.
05 sin query/body, distanceM=null; también null en sucursales de04.
PublicBranch.description=descripción del negocio. No legalName/taxId, IDs/emails
de propietarios, revisión ni auditoría pública. Precio orientativo1económico,
2medio,3alto,4muy alto; no simular cero por ausencia. Rangos de sucursal BOB
independientes opcionales, ambos informados min<=max. Sin service_price en API.

## 7. GESTIÓN DE NEGOCIOS [P1, PERMISOS P2]


11 query:q opcional1..200 por tradeName/slug; publicationStatus opcional
(draft|published|archived); deleted=false|true predeterminado false; page/pageSize.
Filtros AND, substring normalizada literal; updatedAt DESC,id ASC. Solo Admin
puede deleted=true; propietario ->403. M solo lista vínculos vigentes propios.
ManagedSummary={id:UUID,slug:string,tradeName:string,status:BusinessStatus,
 publicationStatus:string,deletedAt:Instant?,updatedAt:Instant,branchCount:int}.
branchCount=no eliminadas. ID11 solo devuelve resumen.

BusinessInput Admin={tradeName:string,description:string?,status:BusinessStatus,
 priceLevel:int?,legalName:string?,taxId:string?}, todas requeridas.
tradeName1..200; description<=5000; legalName<=250;taxId<=50. Nullable textual
vacío se normaliza null. priceLevel null o1..4. taxId exacto único, sin inferir
reglas tributarias adicionales. Propietario debe OMITIR legalName/taxId, incluso
null se rechaza400; los otros cuatro campos siguen siendo obligatorios.
CreateBusiness={business:BusinessInput,branch:BranchInput}.
12: crea draft y primera sucursal isPrimary=true en una transacción. status alta
active o temporarily_closed, no permanente. Slug automático e inmutable: normalizar
ASCII minúscula, letras/dígitos con guiones, base<=140, fallback negocio. Colisión:
base + guion +12 hex del UUID; reintentar UUID si colisión. Máximo160, UNIQUE
incluye eliminados, no reutilizar slug. P2 propietario requiere identidad vigente,
asociación verificada al creador atómica, sigue draft. No publicación automática.
POST duplicado puede crear otro; deshabilitar doble envío y no reintentar POST
sin conocer resultado. Sin claves de idempotencia en este alcance.

ManagedBusiness={id:UUID,slug:string,tradeName:string,description:string?,
 status:BusinessStatus,priceLevel:int?,publicationStatus:string,
 createdAt:Instant,updatedAt:Instant,deletedAt:Instant?,revision:string,
 branches:ManagedBranch[],privateData:{legalName:string?,taxId:string?}}.
privateData solo Admin. publicationStatus siempre draft|published|archived.
ManagedBranch={id:UUID,businessId:UUID,createdAt:Instant,deletedAt:Instant?,
 input:BranchInput,photos:Photo[]}.
input son valores editables almacenados, sin herencias. Admin ve eliminadas
para restauración; propietario solo no eliminadas. Orden no eliminadas primero,
principal primero,name,id. Máximo100 sucursales no eliminadas y500 históricas
por negocio; alcanzar límite ->409, nunca truncar salida. Fotos solo activas,
hasta20 por sucursal; no devolver binario.
13 permite Admin leer eliminado; M solo vigente asociado.
14 actualiza BusinessInput. Negocio eliminado ->409; usar17.

PublicationInput={status:"draft"|"published"}.
15 solo Admin, draft<->published o mismo estado. Publicar exige nombre,
descripción no vacía y slug; negocio vigente no permanente; >=1 sucursal vigente
no permanente. TODAS las sucursales visibles: dirección, punto/municipio válido,
>=1 categoría activa, >=1 contacto. No exige precio/foto/horario; informar ausencia.
Ningún body acepta pending/rejected/archived. Si ya published, edición debe
preservar condiciones o rollback400; no despublicar silenciosamente. Status del
negocio a permanente pasa a draft atómicamente; no se republica hasta corregir.
16 baja lógica negocio y sucursales, publication=archived. Ya baja con ETag
vigente ->204 sin cambio. No bajar fotos/relaciones ni revocar propietarios;
visibilidad deriva del padre. Conservar timestamps de sucursales bajas previas.

RestoreInput={branchIds:UUID[]},1..100 distintos, del negocio y eliminados.
17 solo negocio eliminado, otro estado ->409. Restaurar exclusivamente selección,
negocio draft; conservar status comercial y slug, otras bajas siguen. Si ninguna
seleccionada conserva principal, asignar primera createdAt,id; si había más de
una (datos legacy), reparar como §12. No restaurar fotos eliminadas individualmente.
Validar referencias/ubicación; conflicto rollback409 o400 por campo. Históricos
que no cumplan contrato deben corregirse por mantenimiento antes de restauración.

## 8. SUCURSALES Y COLECCIONES


BranchInput={name:string,address:string,territoryId:UUID,location:Point,
 status:BusinessStatus,isPrimary:bool,priceLevel:int?,minPriceBob:Money?,
 maxPriceBob:Money?,categoryIds:UUID[],serviceIds:UUID[],featureIds:UUID[],
 paymentMethodIds:UUID[],contacts:Contact[],hoursKnown:bool,
 openingHours:OpeningHour[],hourOverrides:HourOverride[]}.
Todo obligatorio. name1..200,address1..1000; categorías0..10; servicios,
características/pagos0..30 cada una; resto límites §6. emergency/home_service,
accessibility JSONB, service_price, descripción de servicio, code y slug de
sucursal no se exponen/editan: no agregar campos libres.
Ejemplo completo para alta/PUT:
```json
{
  "name": "Centro",
  "address": "Calle Junín 123",
  "territoryId": "d91c91d4-0b7f-4fa4-9102-50a6b0c33333",
  "location": {"type": "Point", "coordinates": [-68.1193, -16.4897]},
  "status": "active",
  "isPrimary": true,
  "priceLevel": 2,
  "minPriceBob": null,
  "maxPriceBob": null,
  "categoryIds": ["c1b6d2e0-2b9e-4c7e-91a2-38ce1d2f1111"],
  "serviceIds": [],
  "featureIds": [],
  "paymentMethodIds": [],
  "contacts": [{"type": "phone", "value": "+59122000000"}],
  "hoursKnown": true,
  "openingHours": [{"weekday": 1, "opensAt": "08:00", "closesAt": "18:00"}],
  "hourOverrides": []
}
```
Point exactamente dos coordenadas, type Point y persistir SRID4326. Rango SQL
lng -70..-57, lat -23.5..-9 prefiltra, NO prueba frontera. territoryId municipio
o descendiente activo con ancestros activos. Municipio ancestro debe tener
boundary válido con procedencia y cubrir punto ST_Covers; si hoja tiene boundary
también debe cubrir. Sin municipio confiable ->400 por territorio, no simular
certeza. Carga territorial es P0. Guardar territorio elegido; ajustar trigger
para no reemplazarlo al mover punto dentro del territorio seleccionado.
18 alta dentro de negocio, límites activo100/histórico500.19 reemplazo completo,
no cambia business_id. Exactamente una principal entre NO eliminadas de negocio
vigente. isPrimary=true desmarca anterior bajo bloqueo; false a actual sin
reemplazo ->409. Cambiar principal guardando nueva con true, no dos solicitudes.
20 baja lógica. Última NO eliminada ->409 aunque cerrada comercialmente. Bloqueo
padre serializa dos bajas. Si principal, asignar restante primera createdAt,id.
Si published queda sin sucursal visible ->409; retirar publicación antes. Ya baja,
negocio vigente y ETag correcto ->204. M/P2 también puede baja sucursal propia.
21 Admin y negocio vigente: restaurar sucursal, validar referencias/punto y
publicación. Ya vigente ->200 sin cambio. No principal si existe otra. Fotos
activas vuelven según publicación, no restaura negocio implícitamente.

PUT colecciones, sin DELETE físico:
- Relaciones IDs: ausentes active=false, presentes upsert active=true por PK.
- Contactos: comparar por tipo; value distinto baja anterior y alta nueva con
  is_primary=true. Un activo/tipo, máximo6; no borrar historia.
- Semanales: desactivar turnos anteriores e insertar/reactivar coincidencias.
  hoursKnown=false requiere [];true con [] cierre habitual.
- Excepciones: reemplaza calendario editable ayer..hoy+365 inclusive, misma
  ventana de GET; anteriores se conservan sin enviarse en input. Fuera ventana
  ->400. Omitidas se desactivan con nueva columna active, upsert por fecha.
  No borrar por trigger físico. La ventana se evalúa al guardar con hora servidor;
  ante cruce de día devolver400 para recargar calendario y conservar formulario.
Todas las colecciones, revisión y auditoría en una sola transacción.

## 9. FOTOS Y PREVISUALIZACIÓN


22 multipart campos:file (un archivo),altText opcional<=300,ausente/vacío null;
isPrimary true/false obligatorio,sortOrder entero0..999 obligatorio. Sin partes
extra, URL remota ni base64. JPEG/PNG/WebP,1..5242880 bytes,20 activas/sucursal.
Decodificar realmente; rechazar animadas, corruptas, dimensiones fuera64..8192
por eje o>24 megapíxeles. Reencodificar mismo formato quitando EXIF/GPS y volver
a comprobar<=5MiB. No guardar rutas/filename cliente; filename null. Sin cambio
oculto de dimensiones.413 bytes;415 formato;400 corrupta/dimensiones/metadatos.
El trigger de firma no sustituye decodificador. Elegir biblioteca .NET compatible
con despliegue Linux y licencia de la organización, sin cambiar stack.
PhotoInput={altText:string?,isPrimary:bool,sortOrder:int},todo requerido.
23 solo metadatos/portada; para cambiar bytes cargar nueva y dar baja anterior.
Primera foto siempre principal aunque false; posteriores true cambian portada
atómicamente. No false a portada sin sustituta ->409. sortOrder admite empates,
orden id; no rutas separadas /primary y /order.
24 baja lógica, si portada asignar primera restante sortOrder,id; si no quedan,
portada null. Ya baja ->204 con ETag correcto.
06 sin query: leer bytes solo aquí. Anónimo solo activa y padre/sucursal visibles.
Con Bearer M puede activa de borrador propio; Admin también de negocio/sucursal
eliminados. Foto eliminada individualmente siempre404. No autorizado404;
JWT presente inválido401. Sin token en query ni URLs firmadas.
200 MIME real,Content-Length,X-Content-Type-Options:nosniff,
Content-Disposition:inline,Cache-Control:no-store. Sin ETag/304 de imagen.
Preview privada fetch Bearer y Blob URL; liberar URL al desmontar componente.

## 10. AUDITORÍA Y SALUD


25 query:entity opcional business|branch|branch_photo|business_owner|
owner_identity|branch_contact|opening_hour|branch_hour_override|branch_category|
branch_service|branch_feature|branch_payment. entityId UUID opcional exige entity;
from/to ISO UTC opcionales, juntos si alguno presente, from<=to, máximo366 días;
page/pageSize. Sin fechas últimos30 días UTC hasta ahora. Rango [from,to].
entityId identifica PK UUID; relaciones compuestas usan branch_id; owner_identity
usa user_id. Orden changedAt DESC,id DESC. Nunca old_value/new_value por HTTP.
AuditEntry={id:string,entitySchema:string,entityTable:string,entityKey:string,
 operation:"I"|"U"|"D",actorUserId:UUID?,changedAt:Instant}.
id BIGINT decimal string. Actor null solo histórico; nuevos cambios requieren
actor. Baja/restauración son U. No dashboard ni métricas.
Health={status:"ok"|"unavailable"}.
26 /health proceso sin DB ->200 ok.27 /health/ready comprueba conexión/extensiones
y versión de esquema esperada ->200 ok, si no503 unavailable. Excepción explícita
a ProblemDetails para salud no lista. Sin conexiones/versiones exactas ni stack.
Rutas sin auth, /api/v1/health y /api/v1/health/ready. Compose deberá usar ready;
pg_isready no comprueba aplicación/esquema. No dependen de negocios de prueba.

## 11. PROPIETARIOS [P2]


Registro28 §3. Sin solicitudes, evidencias, documentos de identidad ni reclamos.
Admin verifica por contacto comercial conocido o entrevista con titular, no basta
email/autodeclaración. Asocia al usuario por email exacto registrado y activo.
OwnerInput={email:string,verificationMethod:"business_contact"|"in_person"}.
30 PUT busca email normalizado; inexistente/inactivo ->400 email. Sin listado
general de usuarios. Bloquear usuario afectado y actor en orden UUID, después
business. Upsert owner_identity/business_owner,verified_at=ahora,revoked_at=null,
identity.expires_at=null,verified_by=Admin; rol BusinessOwner, conservar User.
Todo atómico. Identidad global verificada no otorga otros negocios; se requiere
vínculo específico. No publica. Nuevo/reactivado ->200; vigente sin cambios
retorna200 sin alterar fecha/revisión. Negocio eliminado409.
Owner={userId:UUID,email:string,firstName:string,lastName:string,
 verificationMethod:string,verifiedAt:Instant,revokedAt:Instant?}.
Fechas del vínculo, método actual de owner_identity.29 OwnerList={items:Owner[]},
incluye revocadas;email,userId. Máximo20 asociaciones históricas por negocio;
21ª ->409, se permite reactivar existente. Privada sin paginación, ETag padre.
31 DELETE vínculo: revoked_at=ahora, no borrar usuario/identidad ni otros vínculos.
Ya revocado204,inexistente404. No hace falta quitar rol para denegar: DB manda.
Revocación tras commit deniega nuevas ediciones; locks de usuario/business en
mismo orden que edición. Operador de desactivación/identidad también los usa.

Permiso M/P2: activo,rol BusinessOwner,owner_identity verificada no revocada,
expires_at null o>ahora,vínculo verificado no revocado,negocio vigente.
Admin todas operaciones. Propietario IDs11..14,18..20,22..24 y preview06;
NO15..17/21/25/29..31. Alta12 basta identidad vigente, crea vínculo atómico.
No editar taxId/legalName. Edición publicada entra directamente con validaciones
y auditoría; no sistema editorial adicional. Admin puede retirarla.

## 12. PERSISTENCIA Y CAMBIOS NECESARIOS [P0/P1]


Fuente inspeccionada:db/init/01_schema.sql, no un SQL externo v1.3.
Estos son cambios A IMPLEMENTAR, no migraciones ejecutadas por esta revisión.

### 12.1 Línea base y seguridad

- 01_schema.sql YA incluye Identity/__EFMigrationsHistory. No ejecutar
  backend/Data/Migrations/IdentityMigration.sql encima: incluye dominio y colisiona.
- Incrementos SQL numerados con checksum en tabla control a incorporar
  (version text PK,checksum text,applied_at timestamptz). Aplicar una vez mediante
  psql ON_ERROR_STOP durante mantenimiento/transacción. Base nueva=baseline+
  pendientes; existente inventariar antes de registrar baseline. Sin DbUp/Flyway
  obligatorio ni EnsureCreated/Migrate en arranque.
- Un AppDbContext Identity Guid/Database First; personalizaciones partial.
  Funciones/triggers/índices versionados fuera de scaffold, sin segundo contexto.
- Cuenta DB API sin superusuario/DDL/TRUNCATE; permisos explícitos solo tablas de
  alcance. No escribir audit_log directamente: trigger con propietario restringido,
  search_path seguro, sin escalada de SQL dinámico. Sin acceso API a módulos
  anuncios/eventos/importaciones. Dominio no permite DELETE; sesiones sí permiten
  limpieza privada de filas vencidas. No otorgar DELETE amplio a API.
- Actor transaccional según app.actor() y clave real de línea base; no confiar
  en parámetros actor/p_is_admin del cliente. Autorización siempre backend.

### 12.2 Integridad y lectura

- business.revision bigint>=1 DEFAULT1, una revisión por cambio efectivo.
  Locks usuario/business en orden uniforme y triggers diferidos de sucursal mínima.
- BRANCH_PRIMARY_ONE_IDX/PHOTO_PRIMARY_ONE_IDX están COMENTADOS. Resolver duplicados
  eligiendo primera createdAt,id para sucursal o sortOrder,id para foto. Asignar
  una cuando no hay principal en colección activa no vacía. Habilitar UNIQUE
  parcial por business_id/branch_id con is_primary y deleted_at NULL. API asegura
  exactamente una, índice máximo una.
- Contacto único parcial (branch_id,contact_type) con deleted_at NULL. Revisar
  duplicados reales, bajar extras con auditoría y conservar historia.
- Añadir branch.hours_known boolean NOT NULL DEFAULT false; backfill true con
  horarios activos, false sin ellos. Permite declarar cierre explícito sin turnos.
- Añadir branch_hour_override.active boolean DEFAULT true NOT NULL; consultas
  ignoran inactivas y conservan UNIQUE branch/fecha. Upsert, no DELETE físico.
- Corregir app.is_open_at: cierre de fecha completa anula arrastre; proyección
  nullable según hours_known/excepciones. Reutilizar evaluación §6 en openNow.
- AUTO_TERRITORY solo resuelve si falta territorio; no sustituir explícito válido
  cuando cambia location. API siempre valida y envía territorio.
- Auditoría adicional contactos/horas/relaciones; clave branch_id para compuestas.
  Ningún snapshot duplica BYTEA ni material de autenticación.
- Legacy pending/rejected pasa a draft auditado, sin perder contenido. Archived
  no eliminado pasa a draft; archived queda solo para baja. Publicados sin punto
  territorial validado pasan draft hasta corregir. Inventariar y corregir datos
  que excedan límites de esta especificación antes de habilitar rutas nuevas.

### 12.3 Sesiones Identity

- UNIQUE NormalizedEmail tras revisar duplicados; EmailIndex actual no es único.
- app.auth_session:id UUID PK (sid),user_id UUID FK public.AspNetUsers,
  created_at timestamptz,expires_at timestamptz (=creado+30d),revoked_at nullable;
  CHECK expires>created. FK real con nombre entrecomillado de Identity.
- app.refresh_token:id UUID PK,session_id UUID FK auth_session,token_hash bytea
  UNIQUE32 bytes SHA256,created_at timestamptz,consumed_at nullable; índice session_id.
  Sin valor claro ni hashes en auditoría. Reuso bloquea/revoca familia.
- Limpieza privada de familias vencidas y sus tokens tras expiración absoluta;
  no aplica trigger de prohibición física del dominio a estas tablas efímeras.
- owner_identity/actores YA UUID: no migración genérica TEXT->UUID. Verificar
  referencias existentes antes de agregar FK faltantes a Identity; usuarios por
  desactivación, no borrado físico que rompa historia.

### 12.4 Búsqueda, datos y fotos

- branch.location geography(Point,4326);territory.boundary geography(MultiPolygon,
  4326) nullable. Bbox SQL de altas no certifica frontera.
- app.find_branches YA recibe p_territory_id. Orden patrocinado/proyección reseñas
  no es el contrato. Implementar consulta EF/SQL parametrizada específica con
  count/filtros idénticos y sin consultar anuncios/reseñas. Mantener GIST/trigram;
  validar EXPLAIN con datos representativos, no exigir índice en tablas mínimas.
- Catálogos/departamentos/provincias/municipios con fuente,versión,fecha de carga;
  SRID/validez/jerarquía/cobertura comprobados. Sin barrios inventados. Zona/barrio
  sin boundary es admisible bajo municipio válido y selección manual verificada.
- Festivos por scripts con procedencia y año revisado, sin inventar calendario.
- app.create_business_with_branch publica directo: adaptar a draft o transacción
  equivalente. Función SQL no autoriza automáticamente llamada HTTP.
- app.add_photo/set_primary_photo usan actor/admin derivados del servidor; locks
  padre y desmarcar portada anterior antes de marcar nueva para índice único.
  Elegir decodificador .NET compatible y comprobar límites §9 antes de persistir.

## 13. FRONTEND Y CRITERIOS DE TERMINADO


Páginas P1: / buscador,/businesses/[slug] ficha,/login,/management listado,
/management/businesses/new y /management/businesses/[id] edición,/admin/audit.
P2 agrega /register y asignación dentro de ficha administrativa. Sin panel de
reclamos/verificación adicional. Nuxt app/pages/componentes/composables; mapa
cliente Leaflet con teselas/atribución configuradas por entorno. Sin router manual.
Producción un origen HTTPS, proxy /api/v1 al backend, resto Nuxt. Base pública
runtimeConfig.public.apiBase; base SSR privada configurable, accesible en red.
Sesión solo cliente para paneles privados; SSR público no necesita credenciales.

Filtros en URL; cambio reinicia page1. Un GET GeoJSON alimenta lista y mapa.
Mostrar total/página, no todos los marcadores. Radio en metros/API, km/interfaz;
no distancia0 si null. Buscar en esta zona elimina centro/radio y usa bbox.
Permiso geográfico denegado permite pin/municipio. Lista funciona sin teselas.
Llegada externa:https://www.google.com/maps/dir/?api=1&destination=lat,lng;
no origen del visitante, sin rutas propias. Links externos seguros en nueva pestaña.

Formularios PUT cargan GET13 y construyen DTO entrada, no reenvían salida como
body. Guardar ETag;412 conserva formulario y ofrece recargar, no sobrescribir.
400 muestra errores por campo;401 intenta un único refresh compartido salvo
auth; si falla login. Sin reintentar mutaciones si resultado de red incierto:
volver a consultar primero. Protección Nuxt no sustituye API. Móvil/teclado,
carga/vacío/error y formatos monetarios/horarios legibles son parte de entrega.

Pruebas obligatorias con PostgreSQL18/PostGIS real:
- Todas las rutas/métodos/DTO/cabeceras/permisos/límites; OpenAPI con esquemas
  concretos JSON,GeoJSON,multipart,binarios,ProblemDetails e If-Match/ETag.
  Verificar inventario de 27 operaciones MVP y 31 en P2, no rutas duplicadas ni funciones sin interfaz.
- Mismos IDs/orden/paginación JSON/GeoJSON,[lng,lat],metros1/10000/50000,
  bbox/bordes,centro+bbox400,descendientes,count sin duplicados,offset>2000.
- Atomicidad,última sucursal concurrente,principales concurrentes,ETag obsoleto,
  baja/restauración selectiva sin resucitar fotos o sucursales no elegidas.
- Ocultamiento consistente por publicación/baja/cierre permanente,temporal
  visible con isOpen=false,datos privados ausentes de respuestas públicas.
- Horario desconocido,cierre semanal explícito,turnos nocturnos,domingo,
  festivo/override abierto,cierre de hoy que anula arrastre de ayer.
- Rotación/reuso/logout/CSRF,CORS,IsActive/lockout,propietario A no edita B,
  no se asigna privilegios/publicación,revocación efectiva tras commit.
- Firma foto falsa,corrupta/dimensiones/tamaño,EXIF eliminado,límite fotos,
  portada y preview autorizado; geolocalización denegada y teselas caídas.
Compilar no sustituye aceptación. No afirmar pruebas aprobadas por este texto.
Esta revisión modifica documentación; SQL,backend y frontend siguen pendientes.

## 14. REFERENCIAS NORMATIVAS


- [GeoJSON: RFC 7946](https://www.rfc-editor.org/rfc/rfc7946)
- [Problem Details: RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)
- [PostGIS: ST_DWithin](https://postgis.net/docs/ST_DWithin.html)

## 15. REGLAS COMPLEMENTARIAS PARA IMPLEMENTACIÓN SIN AMBIGÜEDAD


- Todo ID de catálogo de BranchInput debe existir y estar activo; no se acepta
  categoría con ancestro inactivo. Territorio también requiere ancestros activos.
  Lecturas públicas solo muestran referencias activas. Mantenimiento de catálogo
  debe corregir/despublicar fichas afectadas en la misma transacción de baja.
- Las mutaciones de sucursal/foto sobre negocio eliminado devuelven 409, salvo
  baja ya efectuada que puede devolver 204 con revisión vigente; restaurar primero
  el negocio. Sobre sucursal eliminada no se permiten PUT/carga/edición de fotos
  (409); solo preview Admin y restauración21. Alta18 nunca sobre padre eliminado.
- El precio y status que devuelve ManagedBranch.input son propios, no efectivos.
  Si published, cambiar sucursal a permanently_closed puede dejarla invisible;
  si era la última visible, se rechaza409. Siempre se puede retirar publicación
  primero. No confundir baja lógica con cierre comercial.
- Lectura de una foto de negocio publicado pero sucursal temporalmente cerrada
  sigue siendo pública; si permanente o eliminada, requiere permiso de preview.
- Al crear/modificar Override.reason, vacío se normaliza null. description también
  admite null en borrador; publicación exige texto no vacío. Contact.value siempre
  no vacío. sortOrder de catálogos es entero; sus longitudes se limitan al SQL
  (code50 y name150); territory name150 y codeIne30; Holiday.name150.
- Los límites se aplican tanto en API como en el formulario. No aceptar horas
  con segundos, fechas locales en timestamps UTC ni números de dinero con más
  de dos decimales. Nuxt convierte campos vacíos nullable a null antes de enviar.
- No se aceptan query en rutas sin query declarada; las fotos no tienen sufijos
  de tamaño/transformación. HEAD/OPTIONS técnicos del servidor no agregan rutas
  funcionales al inventario. Health solo produce su JSON declarado.
- Las respuestas 201 de sucursal/foto contienen el agregado completo. La cabecera
  X-Created-Resource-Id identifica el recurso nuevo aunque el orden de colección
  cambie; no deducirlo por posición ni asumir la primera fila.
- La autorización y If-Match se comprueban antes de aplicar cambios. En una
  transacción de edición se vuelve a comprobar permiso bajo locks. Cuando varios
  usuarios se bloquean (asignación), bloquearlos en orden UUID antes del negocio.
  La revocación de identidad por mantenimiento revoca además todos sus vínculos,
  bloqueando negocios en orden UUID y aumentando sus revisiones; una verificación
  posterior no debe reactivar vínculos antiguos de otros negocios por accidente.
- La revisión del esquema esperada por ready se configura por versión del
  backend desplegado y se compara con la tabla de control de migraciones; nunca
  se aplican incrementos por petición de salud. Antes de incrementos realizar
  copia y ensayo de restauración; errores abortan toda migración.
- Versionar OpenAPI generado con los nombres de tipos de este documento y
  mantenerlo en la misma revisión que frontend/backend. Las definiciones de
  tipos anteriores son normativas; no usar diccionarios libres para suplir DTO.
