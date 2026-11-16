-- PostgreSQL 18 + PostGIS 3.6. Ejecutar despues de 01_schema.sql en una BD de pruebas.
-- psql -v ON_ERROR_STOP=1 -f db/init/02_test.sql
-- Todas las modificaciones, incluidas tablas auxiliares y auditoria, se revierten.
BEGIN;
SELECT set_config('cofre.actor_id','00000000-0000-0000-0000-000000000001',true);

CREATE FUNCTION pg_temp.assert_true(p_ok boolean,p_message text) RETURNS void
LANGUAGE plpgsql AS $$
BEGIN
 IF p_ok IS NOT TRUE THEN RAISE EXCEPTION 'Fallo: %',p_message; END IF;
END $$;

CREATE FUNCTION pg_temp.expect_error(p_sql text,p_message text) RETURNS void
LANGUAGE plpgsql AS $$
BEGIN
 BEGIN
  EXECUTE p_sql;
 EXCEPTION WHEN OTHERS THEN
  IF position(p_message IN SQLERRM)>0 THEN RETURN; END IF;
  RAISE;
 END;
 RAISE EXCEPTION 'No se rechazo la operacion: %',p_sql;
END $$;

-- Las tablas persistentes del dominio incluyen los seis campos, sin FK de usuario.
SELECT pg_temp.assert_true(NOT EXISTS(
 SELECT 1 FROM pg_class t JOIN pg_namespace n ON n.oid=t.relnamespace
 CROSS JOIN (VALUES
  ('created_by_user_id','uuid'),('updated_by_user_id','uuid'),('deleted_by_user_id','uuid'),
  ('created_at','timestamptz'),('updated_at','timestamptz'),('deleted_at','timestamptz')
 ) required(name,type_name)
 WHERE n.nspname IN ('catalog','geo','app') AND t.relkind IN ('r','p') AND t.oid NOT IN ('app.audit_log'::regclass,'app.event'::regclass,'app.event_hist'::regclass,'app.daily_metric'::regclass,'app.monthly_metric'::regclass,'app.favorite'::regclass,'app.photo_blob'::regclass)
 AND NOT EXISTS(SELECT 1 FROM pg_attribute a
  WHERE a.attrelid=t.oid AND a.attname=required.name AND NOT a.attisdropped
   AND a.atttypid=to_regtype(required.type_name))),
 'seis columnas de auditoria en tablas operativas; log y analitica tienen su contrato propio');
SELECT pg_temp.assert_true(NOT EXISTS(
 SELECT 1 FROM pg_constraint fk JOIN pg_class t ON t.oid=fk.conrelid
 JOIN pg_namespace n ON n.oid=t.relnamespace
 JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=ANY(fk.conkey)
 WHERE n.nspname IN ('catalog','geo','app') AND fk.contype='f'
 AND a.attname IN ('created_by_user_id','updated_by_user_id','deleted_by_user_id','moderated_by_user_id')),
 'columnas de usuario sin claves foraneas');
SELECT pg_temp.assert_true(
 EXISTS(SELECT 1 FROM pg_attribute WHERE attrelid='app.review'::regclass AND attname='moderated_at' AND atttypid='timestamptz'::regtype)
 AND EXISTS(SELECT 1 FROM pg_attribute WHERE attrelid='app.review'::regclass AND attname='moderated_by_user_id' AND atttypid='uuid'::regtype),
 'columnas de moderacion de review');
SELECT pg_temp.assert_true(
 to_regclass('app.owner_identity') IS NULL
 AND to_regclass('catalog.service') IS NULL AND to_regclass('app.branch_service') IS NULL
 AND to_regclass('app.service_price') IS NULL,'tablas reemplazadas ausentes');
SELECT pg_temp.assert_true(NOT EXISTS(
 SELECT 1 FROM pg_attribute WHERE attrelid='app.business'::regclass AND NOT attisdropped
 AND attname IN ('publication_status','price_level')) AND NOT EXISTS(
 SELECT 1 FROM pg_attribute WHERE attrelid='app.branch'::regclass AND NOT attisdropped
 AND attname IN ('price_level','min_price_bob','max_price_bob','emergency','home_service','accessibility')),
 'negocio y sucursal sin campos duplicados');

SELECT pg_temp.assert_true(to_regclass('catalog.ad_status') IS NULL
 AND NOT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='app' AND table_name='ad_placement' AND column_name='status')
 AND EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='app' AND table_name='ad_placement' AND column_name='active' AND data_type='boolean' AND is_nullable='NO'),
 'anuncios con active booleano sin catalogo de estados');
SELECT pg_temp.assert_true(to_regclass('app.review_reply') IS NULL
 AND to_regclass('app.holiday') IS NULL AND to_regclass('app.search_synonym') IS NULL
 AND to_regclass('catalog.holiday') IS NOT NULL AND to_regclass('catalog.search_synonym') IS NOT NULL,
 'respuesta integrada y catalogos reubicados');
SELECT pg_temp.assert_true((SELECT array_agg(column_name::text ORDER BY ordinal_position)=
 ARRAY['id','entity_schema','entity_table','operation','transaction_id','old_value','new_value','created_by_user_id','created_at']
 FROM information_schema.columns WHERE table_schema='app' AND table_name='audit_log'), 'contrato actual de audit_log');
SAVEPOINT actor_system_test;
SELECT set_config('cofre.actor_id','',true);
INSERT INTO catalog.search_synonym(term,synonym) VALUES('sistema prueba','sin actor');
SELECT pg_temp.assert_true(EXISTS(SELECT 1 FROM app.audit_log WHERE entity_schema='catalog'
 AND entity_table='search_synonym' AND new_value->>'term'='sistema prueba'
 AND created_by_user_id='00000000-0000-0000-0000-000000000000'), 'actor del sistema sin usuario');
ROLLBACK TO SAVEPOINT actor_system_test;
RELEASE SAVEPOINT actor_system_test;
SELECT set_config('cofre.actor_id','00000000-0000-0000-0000-000000000001',true);

-- Catalogos de estados precargados y relaciones que sustituyen las listas CHECK.
DO $$
DECLARE r record; actual text[];
BEGIN
 FOR r IN SELECT * FROM (VALUES
  ('branch_status',ARRAY['active','temporarily_closed','permanently_closed']),
  ('business_status',ARRAY['draft','pending','published','rejected','archived']),
  ('subscription_status',ARRAY['pending','active','expired','cancelled']),
  ('subscription_payment_status',ARRAY['pending','paid','failed','refunded','cancelled']),
  ('review_moderation_status',ARRAY['visible','hidden','flagged','deleted'])
 ) v(table_name,codes) LOOP
  EXECUTE format('SELECT array_agg(code::text ORDER BY code) FROM catalog.%I',r.table_name) INTO actual;
  PERFORM pg_temp.assert_true(actual=ARRAY(SELECT unnest(r.codes) ORDER BY 1),
   'valores precargados de catalog.'||r.table_name);
  PERFORM pg_temp.expect_error(format('DELETE FROM catalog.%I WHERE code=%L',r.table_name,r.codes[1]),
   'Eliminacion fisica');
  PERFORM pg_temp.expect_error(format('TRUNCATE catalog.%I CASCADE',r.table_name),
   'Eliminacion fisica');
  PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM pg_trigger
   WHERE tgrelid=to_regclass('catalog.'||r.table_name) AND tgname='audit_capture_row' AND tgenabled='O'),
   'auditoria de catalog.'||r.table_name);
 END LOOP;
 FOR r IN SELECT * FROM (VALUES
  ('business','status','business_status'),
  ('branch','status','branch_status'),
  ('business_subscription','status','subscription_status'),
  ('subscription_payment','status','subscription_payment_status'),
  ('review','moderation_status','review_moderation_status')
 ) v(table_name,column_name,catalog_name) LOOP
  PERFORM pg_temp.assert_true(EXISTS(
   SELECT 1 FROM pg_constraint fk
   JOIN pg_attribute source ON source.attrelid=fk.conrelid AND source.attnum=fk.conkey[1]
   JOIN pg_attribute target ON target.attrelid=fk.confrelid AND target.attnum=fk.confkey[1]
   WHERE fk.contype='f' AND fk.conrelid=to_regclass('app.'||r.table_name)
    AND fk.confrelid=to_regclass('catalog.'||r.catalog_name)
    AND cardinality(fk.conkey)=1 AND cardinality(fk.confkey)=1
    AND source.attname=r.column_name AND target.attname='code'),
   'FK de app.'||r.table_name||'.'||r.column_name);
 END LOOP;
END $$;

SELECT pg_temp.expect_error(
 'INSERT INTO app.business(trade_name,status) VALUES(''Estado invalido'',''__invalid__'')',
 'foreign key');
SELECT pg_temp.expect_error(
 'INSERT INTO app.business(trade_name,status) VALUES(''Publicacion invalida'',''__invalid__'')',
 'foreign key');
SELECT pg_temp.expect_error(
 'SELECT app.moderate_review(NULL,''__invalid__'',NULL,NULL)',
 'Estado invalido');

-- El event trigger debe cubrir automaticamente tablas nuevas, sin tocar public.
CREATE TABLE app.__audit_test_metric (a integer,b integer,label text,PRIMARY KEY(a,b));
CREATE TABLE public.__audit_test (id integer PRIMARY KEY);
INSERT INTO app.__audit_test_metric VALUES(1,2,'insert');
UPDATE app.__audit_test_metric SET label='update' WHERE a=1;
INSERT INTO app.__audit_test_metric VALUES(1,2,'upsert')
ON CONFLICT(a,b) DO UPDATE SET label=excluded.label;
MERGE INTO app.__audit_test_metric t USING (VALUES(1,2,'merge')) s(a,b,label)
ON t.a=s.a AND t.b=s.b WHEN MATCHED THEN UPDATE SET label=s.label;
DELETE FROM app.__audit_test_metric WHERE a=1;
INSERT INTO app.__audit_test_metric VALUES(3,4,'truncate');
TRUNCATE app.__audit_test_metric;
INSERT INTO public.__audit_test VALUES(1);

SELECT pg_temp.assert_true(
 (SELECT count(*)=7 FROM app.audit_log WHERE entity_table='__audit_test_metric' AND entity_schema='app'),
 'auditoria de INSERT, UPDATE, UPSERT, MERGE, DELETE y TRUNCATE');
SELECT pg_temp.assert_true(
 EXISTS(SELECT 1 FROM app.audit_log WHERE entity_table='__audit_test_metric' AND operation='T'
 AND old_value->>'label'='truncate' AND old_value @> '{"a":3,"b":4}'::jsonb),
 'instantanea previa de TRUNCATE y clave compuesta');
SELECT pg_temp.assert_true(
 NOT EXISTS(SELECT 1 FROM app.audit_log WHERE entity_table='__audit_test' AND entity_schema='public'),
 'exclusion de public');
SELECT pg_temp.assert_true(
 NOT EXISTS (
 SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE n.nspname IN ('app','geo','catalog') AND c.relkind IN ('r','p')
 AND c.oid NOT IN ('app.audit_log'::regclass,'app.event'::regclass,'app.event_hist'::regclass,
 'app.daily_metric'::regclass,'app.monthly_metric'::regclass,'app.photo_blob'::regclass) AND NOT EXISTS(
 SELECT 1 FROM pg_trigger t WHERE t.tgrelid=c.oid AND t.tgname='audit_capture_row' AND t.tgenabled='O')),
 'cobertura de tablas del dominio salvo analitica y photo_blob');
SELECT pg_temp.expect_error(
 'UPDATE app.audit_log SET entity_table=''alterado'' WHERE id=(SELECT min(id) FROM app.audit_log)',
 'inmutable');
SELECT pg_temp.expect_error('TRUNCATE app.audit_log','inmutable');
SELECT pg_temp.expect_error(
 'INSERT INTO app.audit_log(entity_schema,entity_table,operation) VALUES(''app'',''falso'',''I'')',
 'inmutable');

-- Favorite representa presencia/ausencia, sin estado de borrado logico.
SELECT pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM pg_attribute
 WHERE attrelid='app.favorite'::regclass AND NOT attisdropped AND attname='active'),
 'favorite sin columna active');
SELECT pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM pg_trigger
 WHERE tgrelid='app.favorite'::regclass AND tgfoid='app.touch()'::regprocedure),
 'favorite sin sellado de baja logica');

-- Cobertura de la politica en todas las tablas operativas y de Identity.
DO $$
DECLARE r record;
BEGIN
 FOR r IN SELECT c.oid,n.nspname,c.relname FROM pg_class c
 JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE c.relkind IN ('r','p') AND (n.nspname IN ('app','catalog','geo') OR
  (n.nspname='public' AND (c.relname LIKE 'AspNet%' OR c.relname='__EFMigrationsHistory')))
 AND NOT (n.nspname='app' AND (c.relname IN ('event','event_hist','branch_photo','business_photo','favorite')
  OR right(c.relname,7)='_metric'))
 LOOP
  PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM pg_trigger WHERE tgrelid=r.oid
   AND tgfoid IN ('app.reject_physical_delete()'::regprocedure,'app.protect_audit_log()'::regprocedure)
   AND (tgtype::int & 8)=8 AND tgenabled='O'),'bloqueo DELETE en '||r.relname);
  PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM pg_trigger WHERE tgrelid=r.oid
   AND tgfoid IN ('app.reject_physical_delete()'::regprocedure,'app.protect_audit_log()'::regprocedure)
   AND (tgtype::int & 32)=32 AND tgenabled='O'),'bloqueo TRUNCATE en '||r.relname);
 END LOOP;
END $$;

SELECT pg_temp.expect_error('TRUNCATE public.__audit_test','Eliminacion fisica');
SELECT pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM pg_trigger
 WHERE tgrelid='public.spatial_ref_sys'::regclass AND NOT tgisinternal
 AND tgfoid='app.reject_physical_delete()'::regprocedure),'objetos de extension intactos');
SELECT pg_temp.assert_true((SELECT count(*)=5 FROM pg_extension
 WHERE extname IN ('postgis','pg_trgm','unaccent','btree_gist','pgrouting')),'cinco extensiones conservadas');

-- Cambiar el tipo de un territorio no puede invalidar sus hijos.
DO $$
DECLARE dep uuid; prov uuid; mun uuid;
BEGIN
 INSERT INTO geo.territory(name,territory_type) VALUES('Departamento prueba','department') RETURNING id INTO dep;
 INSERT INTO geo.territory(name,territory_type,parent_id) VALUES('Provincia prueba','province',dep) RETURNING id INTO prov;
 INSERT INTO geo.territory(name,territory_type,parent_id) VALUES('Municipio prueba','municipality',prov) RETURNING id INTO mun;
 PERFORM pg_temp.expect_error(format('UPDATE geo.territory SET territory_type=''department'',parent_id=NULL WHERE id=%L',prov),
  'jerarquia territorial de hijos');
END $$;

-- Una tabla nueva recibe proteccion y metadatos sin instalacion manual.
CREATE TABLE app.__policy_test (
 id int PRIMARY KEY, active boolean NOT NULL DEFAULT true,
 created_at timestamptz NOT NULL DEFAULT now(), created_by_user_id uuid DEFAULT app.actor(),
 updated_at timestamptz, updated_by_user_id uuid, deleted_at timestamptz, deleted_by_user_id uuid);
INSERT INTO app.__policy_test(id) VALUES(1);
UPDATE app.__policy_test SET id=id WHERE id=1;
CREATE TEMP TABLE audit_snapshot AS SELECT * FROM app.__policy_test;
UPDATE app.__policy_test SET active=false WHERE id=1;
SELECT pg_temp.assert_true((SELECT p.deleted_at IS NOT NULL AND p.deleted_by_user_id=app.actor()
 AND p.updated_at IS NOT DISTINCT FROM o.updated_at
 AND p.updated_by_user_id IS NOT DISTINCT FROM o.updated_by_user_id
 FROM app.__policy_test p CROSS JOIN audit_snapshot o),'baja solo sella deleted, conserva updated');
UPDATE app.__policy_test SET id=id WHERE id=1;
SELECT pg_temp.assert_true((SELECT updated_at IS NOT NULL AND deleted_at IS NOT NULL
 AND updated_by_user_id=app.actor() FROM app.__policy_test),'editar una baja sella updated');
UPDATE app.__policy_test SET active=true WHERE id=1;
SELECT pg_temp.assert_true((SELECT deleted_at IS NULL AND deleted_by_user_id IS NULL
 AND updated_by_user_id=app.actor() FROM app.__policy_test),'restauracion sella updated y limpia baja');
INSERT INTO app.__policy_test(id,active) VALUES(2,false);
SELECT pg_temp.assert_true((SELECT deleted_at IS NOT NULL AND deleted_by_user_id=app.actor()
 AND updated_at IS NULL FROM app.__policy_test WHERE id=2),'insertar inactivo audita baja');
SELECT pg_temp.expect_error('DELETE FROM app.__policy_test WHERE id=1','Eliminacion fisica');
SELECT pg_temp.expect_error('TRUNCATE app.__policy_test','Eliminacion fisica');
SELECT pg_temp.expect_error('TRUNCATE app.branch CASCADE','Eliminacion fisica');

-- Ninguna tabla de public recibe captura ni sellado de auditoria, incluido Identity.
SELECT pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM pg_trigger tr
 JOIN pg_class c ON c.oid=tr.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE n.nspname='public' AND tr.tgfoid IN
 ('app.touch()'::regprocedure,'app.capture_audit()'::regprocedure,
  'app.capture_truncate_audit()'::regprocedure)),'public e Identity sin triggers de auditoria');
-- Tambien se excluyen tablas futuras de public aunque tengan las seis columnas.
CREATE TABLE public.__metadata_test (LIKE app.__policy_test INCLUDING DEFAULTS);
INSERT INTO public.__metadata_test(id) VALUES(1);
UPDATE public.__metadata_test SET active=false WHERE id=1;
SELECT pg_temp.assert_true((SELECT updated_at IS NULL AND deleted_at IS NULL
 AND updated_by_user_id IS NULL AND deleted_by_user_id IS NULL
 FROM public.__metadata_test),'public no sella columnas de auditoria');
SELECT pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM app.audit_log WHERE entity_schema='public'),
 'ninguna captura de public');

-- Una definicion inactiva permite dar de baja sus valores historicos.
INSERT INTO catalog.parameter_definition(code,description,data_type,default_value)
VALUES('test.retired','Prueba de baja','integer','1');
INSERT INTO catalog.parameter_value(code,value) VALUES('test.retired','2');
UPDATE catalog.parameter_definition SET active=false WHERE code='test.retired';
UPDATE catalog.parameter_value SET active=false WHERE code='test.retired';
SELECT pg_temp.assert_true((SELECT deleted_at IS NOT NULL AND deleted_by_user_id=app.actor()
 FROM catalog.parameter_value WHERE code='test.retired'),'baja con definicion inactiva');

DO $$
DECLARE
 bid uuid; second_bid uuid; sid uuid; sid2 uuid; cat uuid; pic1 uuid; pic2 uuid; adid uuid; test_review_id uuid; item_id uuid; unassigned_item_id uuid; no_price_id uuid; subscription_type_id uuid; subscription_id uuid; payment_id uuid; test_type text;
 uid uuid:='00000000-0000-0000-0000-000000000001';
 p geography:=ST_SetSRID(ST_MakePoint(-68.1193,-16.4897),4326)::geography;
 imagebytes bytea:=decode('ffd8ff010203040506070809','hex');
BEGIN
 SELECT id INTO STRICT cat FROM catalog.category WHERE code='pharmacy';
 bid:=app.create_business_with_branch(uid,'Farmacia prueba','Casa central','Av. de prueba 123',
  -68.1193,-16.4897,p_category_ids=>ARRAY[cat]);
 SELECT id INTO STRICT sid FROM app.branch WHERE business_id=bid;
 SET CONSTRAINTS ALL IMMEDIATE;
 SET CONSTRAINTS ALL DEFERRED;
 PERFORM pg_temp.assert_true((SELECT app.is_business_owner(uid,bid) AND created_by_user_id=uid FROM app.business WHERE id=bid),
  'propietario directo y actor creador');
 PERFORM pg_temp.assert_true(app.is_business_owner(uid,bid) AND
  NOT app.is_business_owner('00000000-0000-0000-0000-000000000002',bid),'control de propietario directo');
 UPDATE app.branch SET name=name WHERE id=sid;
 PERFORM pg_temp.assert_true((SELECT created_by_user_id=uid AND updated_by_user_id=uid
  AND created_at IS NOT NULL AND updated_at>=created_at FROM app.branch WHERE id=sid),'sellado de actualizacion');
 PERFORM pg_temp.expect_error(format('UPDATE app.business SET status=''active'' WHERE id=%L',bid),'foreign key');
 PERFORM pg_temp.expect_error(format('UPDATE app.branch SET status=''published'' WHERE id=%L',sid),'foreign key');

 PERFORM pg_temp.assert_true(
  EXISTS(SELECT 1 FROM app.business WHERE id=bid AND status='published'),
  'publicacion con categoria');

 INSERT INTO app.business(trade_name,status) VALUES('Publicado sin sucursales','published');
 SET CONSTRAINTS ALL IMMEDIATE;
 PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.business b
  WHERE b.trade_name='Publicado sin sucursales' AND b.status='published'
  AND NOT EXISTS(SELECT 1 FROM app.branch s WHERE s.business_id=b.id)),
  'negocio publicado sin sucursales permitido');
 SET CONSTRAINTS ALL DEFERRED;
 PERFORM pg_temp.expect_error(format('UPDATE app.branch SET business_id=NULL WHERE id=%L',sid),'not-null constraint');
 PERFORM pg_temp.expect_error(format('UPDATE app.branch SET business_id=%L WHERE id=%L',
  'ffffffff-ffff-ffff-ffff-ffffffffffff',sid),'foreign key');

 INSERT INTO app.branch(business_id,name,address,location,is_primary)
 VALUES(bid,'Otra principal','Direccion de prueba',p,true) RETURNING id INTO sid2;
 INSERT INTO app.branch_category(branch_id,category_id) VALUES(sid2,cat);
 SET CONSTRAINTS ALL IMMEDIATE;
 SET CONSTRAINTS ALL DEFERRED;
 PERFORM pg_temp.assert_true((SELECT app.is_business_owner(uid,bid) AND created_by_user_id=uid FROM app.business WHERE id=bid),
  'propietario directo y actor creador');
 PERFORM pg_temp.assert_true(app.is_business_owner(uid,bid) AND
  NOT app.is_business_owner('00000000-0000-0000-0000-000000000002',bid),'control de propietario directo');
 UPDATE app.branch SET name=name WHERE id=sid;
 PERFORM pg_temp.assert_true((SELECT created_by_user_id=uid AND updated_by_user_id=uid
  AND created_at IS NOT NULL AND updated_at>=created_at FROM app.branch WHERE id=sid),'sellado de actualizacion');
 PERFORM pg_temp.expect_error(format('UPDATE app.business SET status=''active'' WHERE id=%L',bid),'foreign key');
 PERFORM pg_temp.expect_error(format('UPDATE app.branch SET status=''published'' WHERE id=%L',sid),'foreign key');

 PERFORM pg_temp.assert_true(
  (SELECT count(*)=2 FROM app.branch WHERE business_id=bid AND is_primary),'varias sucursales principales');
 -- Estados operativos y financieros ordinarios no representan bajas.
 INSERT INTO catalog.subscription_type(code,name) VALUES('test_type','Tipo prueba') RETURNING id INTO subscription_type_id;
 INSERT INTO app.business_subscription(business_id,subscription_type_id,starts_at,ends_at,amount_bob)
 VALUES(bid,subscription_type_id,now(),now()+interval '1 month',100) RETURNING id INTO subscription_id;
 UPDATE app.business_subscription SET status='active' WHERE id=subscription_id;
 PERFORM pg_temp.assert_true((SELECT updated_by_user_id=uid AND deleted_at IS NULL
 FROM app.business_subscription WHERE id=subscription_id),'activar suscripcion es actualizacion');
 UPDATE app.business_subscription SET status='expired' WHERE id=subscription_id;
 PERFORM pg_temp.assert_true((SELECT deleted_at IS NULL FROM app.business_subscription WHERE id=subscription_id),
  'vencimiento no es baja de suscripcion');
 UPDATE app.business_subscription SET status='cancelled' WHERE id=subscription_id;
 PERFORM pg_temp.assert_true((SELECT deleted_by_user_id=uid AND deleted_at IS NOT NULL
 FROM app.business_subscription WHERE id=subscription_id),'cancelacion sella baja de suscripcion');
 INSERT INTO app.subscription_payment(subscription_id,amount_bob) VALUES(subscription_id,100) RETURNING id INTO payment_id;
 UPDATE app.subscription_payment SET status='paid',paid_at=now() WHERE id=payment_id;
 PERFORM pg_temp.assert_true((SELECT updated_by_user_id=uid AND deleted_at IS NULL
 FROM app.subscription_payment WHERE id=payment_id),'pago confirmado es actualizacion');
 UPDATE app.subscription_payment SET status='cancelled' WHERE id=payment_id;
 PERFORM pg_temp.assert_true((SELECT deleted_by_user_id=uid AND deleted_at IS NOT NULL
 FROM app.subscription_payment WHERE id=payment_id),'cancelacion sella baja de pago');
 PERFORM app.soft_delete_branch(sid2,uid);
 PERFORM pg_temp.assert_true((SELECT deleted_at IS NOT NULL AND deleted_by_user_id=uid
 AND updated_at IS NULL FROM app.branch WHERE id=sid2),'primera baja no inventa actualizacion');
 UPDATE app.branch SET status='temporarily_closed' WHERE id=sid;
 PERFORM pg_temp.assert_true((SELECT updated_by_user_id=uid AND deleted_at IS NULL
 AND deleted_by_user_id IS NULL FROM app.branch WHERE id=sid),'cierre temporal es actualizacion');
 UPDATE app.branch SET status='active' WHERE id=sid;
 PERFORM pg_temp.expect_error(format('UPDATE app.branch_category SET active=false WHERE branch_id=%L;
  SET CONSTRAINTS ALL IMMEDIATE',sid),'categoria vigente');
 SET CONSTRAINTS ALL DEFERRED;
 PERFORM pg_temp.expect_error(format('UPDATE catalog.category SET active=false WHERE id=%L;
  SET CONSTRAINTS ALL IMMEDIATE',cat),'categoria vigente');
 SET CONSTRAINTS ALL DEFERRED;

 -- Servicios y productos con precio fijo, rango y precio opcional.
 FOR test_type IN SELECT unnest(ARRAY['S','P']) LOOP
  INSERT INTO catalog.service_product(code,name,item_type)
  VALUES('test_'||test_type,'Item de prueba '||test_type,test_type) RETURNING id INTO item_id;
  INSERT INTO app.branch_service_product(branch_id,service_product_id) VALUES(sid,item_id);
  INSERT INTO app.service_product_price(branch_id,service_product_id,amount_min_bob,amount_max_bob)
  VALUES(sid,item_id,25,25);
  PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.service_product_price
   WHERE branch_id=sid AND service_product_id=item_id AND active AND amount_min_bob=amount_max_bob),
   'precio fijo para tipo '||test_type);
  UPDATE app.service_product_price SET active=false WHERE branch_id=sid AND service_product_id=item_id;
  INSERT INTO app.service_product_price(branch_id,service_product_id,amount_min_bob,amount_max_bob)
  VALUES(sid,item_id,20,30);
  PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.service_product_price
   WHERE branch_id=sid AND service_product_id=item_id AND active AND amount_min_bob<amount_max_bob),
   'rango de precio para tipo '||test_type);
  PERFORM pg_temp.expect_error(format(
   'UPDATE app.service_product_price SET amount_max_bob=10 WHERE branch_id=%L AND service_product_id=%L AND active',sid,item_id),
   'check constraint');
  PERFORM pg_temp.expect_error(format(
   'INSERT INTO app.service_product_price(branch_id,service_product_id,amount_min_bob,amount_max_bob) VALUES(%L,%L,1,2)',sid,item_id),
   'unique constraint');
  INSERT INTO catalog.service_product(code,name,item_type)
  VALUES('no_price_'||test_type,'Sin precio '||test_type,test_type) RETURNING id INTO no_price_id;
  INSERT INTO app.branch_service_product(branch_id,service_product_id) VALUES(sid,no_price_id);
  PERFORM pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM app.service_product_price
   WHERE branch_id=sid AND service_product_id=no_price_id AND active),'precio opcional para tipo '||test_type);
 END LOOP;
 INSERT INTO catalog.service_product(code,name) VALUES('default_service','Servicio predeterminado') RETURNING id INTO unassigned_item_id;
 PERFORM pg_temp.assert_true((SELECT item_type='S' FROM catalog.service_product WHERE id=unassigned_item_id),'tipo S predeterminado');
 PERFORM pg_temp.expect_error(format(
  'INSERT INTO app.service_product_price(branch_id,service_product_id,amount_min_bob,amount_max_bob) VALUES(%L,%L,1,2)',sid,unassigned_item_id),
  'foreign key');
 PERFORM pg_temp.expect_error(
  'INSERT INTO catalog.service_product(code,name,item_type) VALUES(''invalid_type'',''Tipo invalido'',''X'')','check constraint');
 PERFORM pg_temp.expect_error(
  'INSERT INTO catalog.service_product(code,name,item_type) VALUES(''null_type'',''Tipo nulo'',NULL)','not-null constraint');
 PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.audit_log WHERE entity_schema='catalog'
  AND entity_table='service_product' AND operation='I' AND new_value->>'item_type'='P'),'auditoria de productos');

 pic1:=app.add_photo(sid,uid,imagebytes,'image/jpeg',true);
 pic2:=app.add_photo(sid,uid,imagebytes,'image/jpeg',true);
 PERFORM app.set_primary_photo(sid,pic1,uid);
 PERFORM pg_temp.assert_true(
  (SELECT count(*)=2 FROM app.branch_photo WHERE branch_id=sid AND is_primary),
  'varias fotos principales mediante funciones');

 -- Un permiso de administrador nulo no autoriza a un usuario sin propiedad.
 PERFORM pg_temp.expect_error(format(
  'SELECT app.add_photo(%L,%L,%L::bytea,''image/jpeg'',p_is_admin=>NULL)',
  sid,'00000000-0000-0000-0000-000000000002',imagebytes),'Solo propietario');
 PERFORM pg_temp.expect_error(format(
  'SELECT app.set_primary_photo(%L,%L,%L,p_is_admin=>NULL)',
  sid,pic1,'00000000-0000-0000-0000-000000000002'),'Permiso denegado');

 -- Lunes 22:00 a martes 02:00, America/La_Paz.
 INSERT INTO app.opening_hour(branch_id,weekday,opens_at,closes_at)
 VALUES(sid,1,'22:00','02:00');
 PERFORM pg_temp.assert_true(app.is_open_at(sid,'2026-11-10 01:00:00-04'),'turno nocturno');
 INSERT INTO app.branch_hour_override(branch_id,service_date,is_closed)
 VALUES(sid,'2026-11-10',true);
 PERFORM pg_temp.assert_true(NOT app.is_open_at(sid,'2026-11-10 01:00:00-04'),
  'cierre excepcional anula turno de ayer');
 UPDATE app.branch_hour_override SET active=false WHERE branch_id=sid;
 PERFORM pg_temp.assert_true(app.is_open_at(sid,'2026-11-10 01:00:00-04'),
  'desactivar excepcion restaura horario regular');
 INSERT INTO catalog.holiday(name,holiday_date) VALUES('Feriado prueba','2026-11-10');
 PERFORM pg_temp.assert_true(NOT app.is_open_at(sid,'2026-11-10 01:00:00-04'),
  'feriado cierra todo el dia');
 UPDATE app.branch_hour_override SET active=true,is_closed=false,is_24_hours=true,
  opens_at='00:00',closes_at='00:00' WHERE branch_id=sid;
 PERFORM pg_temp.assert_true(app.is_open_at(sid,'2026-11-10 01:00:00-04'),
  'excepcion 24 horas prevalece sobre feriado');
 UPDATE app.branch_hour_override SET active=false WHERE branch_id=sid;
 UPDATE catalog.holiday SET active=false WHERE name='Feriado prueba';
 INSERT INTO app.opening_hour(branch_id,weekday,opens_at,closes_at,is_24_hours)
 VALUES(sid,3,'00:00','00:00',true);
 PERFORM pg_temp.assert_true(app.is_open_at(sid,'2026-11-11 23:59:59-04'),'dia regular 24 horas');
 PERFORM pg_temp.assert_true(NOT app.is_open_at(sid,'2026-11-12 00:00:00-04'),
  '24 horas no se arrastra al dia siguiente');

 INSERT INTO app.review(branch_id,user_id,rating,body) VALUES(sid,uid,5,'Muy bueno'),(sid,uid,4,'Bien');
 PERFORM pg_temp.assert_true((SELECT count(*)=2 FROM app.review WHERE branch_id=sid),
  'resenas multiples');
 INSERT INTO catalog.parameter_value(code,value) VALUES('review.moderation_post','false');
 INSERT INTO app.review(branch_id,user_id,rating) VALUES(sid,uid,3);
 PERFORM pg_temp.assert_true(
  (SELECT count(*)=1 FROM app.review WHERE branch_id=sid AND moderation_status='hidden'),
  'parametro de moderacion');
 -- Funciones que antes dependian de tablas de propietarios y campos duplicados.
 PERFORM app.admin_update_business(bid,uid,'Farmacia prueba','Descripcion actualizada');
 PERFORM app.admin_publish_business(bid,uid);
 SET CONSTRAINTS ALL IMMEDIATE;
 SET CONSTRAINTS ALL DEFERRED;
 PERFORM pg_temp.assert_true((SELECT owners=1 AND status='published'
  FROM app.admin_business_summary WHERE id=bid),'resumen con propietario y estado unico');
 INSERT INTO app.review(branch_id,user_id,rating,body)
 VALUES(sid,uid,5,'Para moderar') RETURNING id INTO test_review_id;
 PERFORM app.moderate_review(test_review_id,'visible','Aprobada',uid);
 PERFORM pg_temp.assert_true((SELECT created_by_user_id=uid AND updated_by_user_id=uid
  AND moderated_by_user_id=uid AND moderated_at IS NOT NULL FROM app.review WHERE id=test_review_id),
  'actores y fecha de moderacion sin FK');
 PERFORM pg_temp.assert_true(app.reply_to_review(test_review_id,uid,'Gracias')=test_review_id,
  'respuesta devuelve ID de resena');
 PERFORM app.reply_to_review(test_review_id,uid,'Gracias de nuevo');
 PERFORM pg_temp.assert_true((SELECT reply_text='Gracias de nuevo' AND reply_by_user_id=uid
  AND replied_at IS NOT NULL FROM app.review WHERE id=test_review_id),'respuesta integrada y actualizable');
 PERFORM pg_temp.expect_error(format('SELECT app.reply_to_review(%L,%L,'' '')',test_review_id,uid),'Respuesta requerida');
 PERFORM pg_temp.expect_error(format(
  'SELECT app.reply_to_review(%L,%L,''Sin permiso'')',test_review_id,'00000000-0000-0000-0000-000000000002'),
  'No es propietario');
 -- Un negocio tiene varios propietarios y un propietario tiene varios negocios.
 INSERT INTO app.business_owner(business_id,user_id) VALUES(bid,'00000000-0000-0000-0000-000000000002');
 PERFORM pg_temp.assert_true(app.is_business_owner('00000000-0000-0000-0000-000000000002',bid),
  'segundo propietario autorizado');
 PERFORM app.reply_to_review(test_review_id,'00000000-0000-0000-0000-000000000002','Respuesta compartida');
 INSERT INTO app.business(trade_name) VALUES('Segundo negocio') RETURNING id INTO second_bid;
 INSERT INTO app.business_owner(business_id,user_id) VALUES(second_bid,uid);
 PERFORM pg_temp.assert_true((SELECT count(*)=2 FROM app.business_owner WHERE user_id=uid AND active),
  'usuario con varios negocios');
 PERFORM pg_temp.assert_true((SELECT owners=2 FROM app.admin_business_summary WHERE id=bid),'resumen con varios propietarios');
 PERFORM pg_temp.expect_error(format('INSERT INTO app.business_owner(business_id,user_id) VALUES(%L,%L)',bid,uid),'unique constraint');
 UPDATE app.business_owner SET active=false,deleted_at=now() WHERE business_id=bid AND user_id='00000000-0000-0000-0000-000000000002';
 PERFORM pg_temp.assert_true(NOT app.is_business_owner('00000000-0000-0000-0000-000000000002',bid),'revocacion de propietario');

 PERFORM app.add_business_photo(bid,uid,imagebytes,'image/jpeg');
 PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.business_photo WHERE business_id=bid AND created_by_user_id=uid),
  'fotos de negocio con propietario directo');

 UPDATE app.branch SET deleted_at=now() WHERE id=sid;
 PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.published_branch WHERE branch_id=sid),
  'fecha de baja no decide la vigencia de una sucursal activa');
 UPDATE app.branch SET deleted_at=NULL WHERE id=sid;
 UPDATE app.business_owner SET active=false WHERE business_id=bid AND user_id=uid;
 PERFORM pg_temp.assert_true(NOT app.is_business_owner(uid,bid),'active falso revoca propiedad sin fecha de baja');
 UPDATE app.business_owner SET active=true WHERE business_id=bid AND user_id=uid;
 PERFORM pg_temp.assert_true(app.toggle_favorite(uid,sid),'favorito on');
 PERFORM pg_temp.assert_true((SELECT favorites=1 FROM app.dashboard_branch_metrics WHERE branch_id=sid),
  'metricas cuentan filas de favoritos sin active');
 PERFORM pg_temp.expect_error(format('INSERT INTO app.favorite(user_id,branch_id) VALUES(%L,%L)',uid,sid),
  'unique constraint');
 PERFORM pg_temp.assert_true(NOT app.toggle_favorite(uid,sid),'favorito off');
 PERFORM pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM app.favorite WHERE user_id=uid AND branch_id=sid),
  'desmarcar favorito elimina fisicamente');
 PERFORM app.toggle_favorite(uid,sid);
 -- Favorite admite DELETE y TRUNCATE, ambos conservan captura de auditoria.
 DELETE FROM app.favorite WHERE user_id=uid AND branch_id=sid;
 PERFORM pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM app.favorite WHERE user_id=uid AND branch_id=sid)
  AND EXISTS(SELECT 1 FROM app.audit_log WHERE entity_table='favorite' AND operation='D'
   AND old_value->>'branch_id'=sid::text),'borrado fisico de favorite auditado');
 PERFORM app.toggle_favorite(uid,sid);
 TRUNCATE app.favorite;
 PERFORM pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM app.favorite)
  AND EXISTS(SELECT 1 FROM app.audit_log WHERE entity_table='favorite' AND operation='T'),
  'TRUNCATE de favorite permitido y auditado');
 PERFORM pg_temp.assert_true((SELECT favorites=0 FROM app.dashboard_branch_metrics WHERE branch_id=sid),
  'favoritos borrados excluidos de metricas');
 UPDATE app.branch_photo SET active=false WHERE id=pic1;
 PERFORM pg_temp.expect_error(format('SELECT app.set_primary_photo(%L,%L,%L)',sid,pic1,uid),'Foto inexistente');
 UPDATE app.branch_photo SET active=true WHERE id=pic1;
 UPDATE app.review SET moderation_status='deleted' WHERE id=test_review_id;
 PERFORM pg_temp.assert_true((SELECT deleted_by_user_id=uid AND deleted_at IS NOT NULL
 FROM app.review WHERE id=test_review_id),'auditoria de baja de resena');
 PERFORM pg_temp.expect_error(format('SELECT app.reply_to_review(%L,%L,''Respuesta'')',test_review_id,uid),'no rows');

 PERFORM pg_temp.assert_true(
  EXISTS(SELECT 1 FROM app.find_branches(NULL,p,5000) f WHERE f.branch_id=sid
   AND ST_Equals(f.location::geometry,p::geometry) AND f.branch_status='active'),
  'busqueda devuelve ubicacion y estados');
 PERFORM pg_temp.expect_error('SELECT * FROM app.find_branches(p_limit=>NULL)','Paginacion');
 PERFORM pg_temp.expect_error('SELECT * FROM app.find_branches(p_offset=>NULL)','Paginacion');
 PERFORM pg_temp.expect_error(
  'SELECT * FROM app.find_branches(p_center=>''SRID=4326;POINT EMPTY''::geography)','Centro');
 PERFORM pg_temp.expect_error(
  'SELECT * FROM app.find_branches(p_bbox=>''SRID=4326;POINT(-68 -16)''::geography)','BBox');
 PERFORM pg_temp.expect_error(
  'SELECT * FROM app.find_branches(p_radius_m=>100)','Radio');
 PERFORM pg_temp.expect_error(
  'UPDATE catalog.parameter_definition SET max_number=200,default_value=''101'' WHERE code=''search.default_limit''',
  'consumidor');
 PERFORM pg_temp.expect_error(
  'INSERT INTO catalog.parameter_value(code,value,scope_type,scope_id)
   VALUES(''photo.max_bytes'',''1000'',''branch'',''00000000-0000-0000-0000-000000000000'')',
  'Alcance');
 INSERT INTO catalog.parameter_value(code,value) VALUES('search.default_limit','1');
 PERFORM pg_temp.assert_true((SELECT count(*)<=1 FROM app.find_branches()),'limite configurable');
 INSERT INTO catalog.parameter_value(code,value) VALUES('search.default_radius_m','1');
 PERFORM pg_temp.assert_true(NOT EXISTS(
  SELECT 1 FROM app.find_branches(p_center=>ST_SetSRID(ST_MakePoint(-68.12,-16.49),4326)::geography)
  f WHERE f.branch_id=sid),'radio predeterminado configurable');

 INSERT INTO app.ad_placement(branch_id,starts_at,ends_at,active)
 VALUES(sid,now()-interval '1 hour',now()+interval '1 hour',true)
 RETURNING id INTO adid;
 PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.active_ads() WHERE ad_id=adid),
  'anuncio activo y vigente sin condicion de pago');
 UPDATE app.ad_placement SET active=false WHERE id=adid;
 PERFORM pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM app.active_ads() WHERE ad_id=adid),
  'active falso excluye anuncio sin fecha de baja');
 UPDATE app.ad_placement SET active=true,deleted_at=now() WHERE id=adid;
 PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.active_ads() WHERE ad_id=adid),
  'fecha de baja no decide vigencia de anuncio activo');
 UPDATE app.ad_placement SET deleted_at=NULL WHERE id=adid;
 UPDATE app.ad_placement SET ends_at=now()-interval '1 minute' WHERE id=adid;
 PERFORM pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM app.active_ads() WHERE ad_id=adid),
  'anuncio vencido excluido aunque active sea verdadero');
 PERFORM pg_temp.assert_true(app.expire_ads()=1,'vencimiento desactiva anuncio');
 PERFORM pg_temp.assert_true((SELECT NOT active FROM app.ad_placement WHERE id=adid),
  'anuncio vencido queda con active falso');
 PERFORM pg_temp.assert_true(app.expire_ads()=0,'vencimiento no repite desactivaciones');
 UPDATE app.ad_placement SET active=true,ends_at=now()+interval '1 hour' WHERE id=adid;
 UPDATE app.branch SET status='permanently_closed' WHERE id=sid;
 PERFORM pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM app.active_ads() WHERE ad_id=adid),
  'sin publicidad de sucursal cerrada permanentemente');
 UPDATE app.branch SET status='temporarily_closed' WHERE id=sid;
 PERFORM pg_temp.assert_true(EXISTS(
  SELECT 1 FROM app.find_branches() f WHERE f.branch_id=sid AND f.branch_status='temporarily_closed'),
  'cierre temporal visible con estado');
 UPDATE app.branch SET status='active' WHERE id=sid;

 INSERT INTO app.event(event_type,ip_address,user_agent,branch_id,occurred_at)
 VALUES('branch_view','192.0.2.10','SmokeTest/1.0',sid,now()-interval '8 days');
 PERFORM pg_temp.assert_true(app.archive_events(now()-interval '7 days')>=1,'archivado atomico');
 PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.event_hist WHERE branch_id=sid),'historico');
 PERFORM app.consolidate_daily((now() AT TIME ZONE 'America/La_Paz')::date-8);
 PERFORM app.consolidate_daily((now() AT TIME ZONE 'America/La_Paz')::date-8);
 PERFORM pg_temp.assert_true(NOT EXISTS(
  SELECT 1 FROM app.audit_log WHERE entity_schema='app' AND entity_table='daily_metric'),
  'auditoria de metricas excluida');
 PERFORM pg_temp.assert_true(EXISTS(
  SELECT 1 FROM app.audit_log WHERE entity_table='branch_photo' AND operation='I'
  AND NOT (new_value ? 'photo_data') AND new_value->>'photo_sha256' IS NOT NULL),'auditoria de fotos con hash sin bytes');
 PERFORM pg_temp.assert_true(NOT EXISTS(
  SELECT 1 FROM app.audit_log WHERE transaction_id=pg_current_xact_id() AND created_by_user_id IS DISTINCT FROM uid),
  'actor de la transaccion');

 DELETE FROM app.branch_photo WHERE id=pic2;
 PERFORM pg_temp.assert_true(app.recover_photo(pg_catalog.sha256(imagebytes))=imagebytes
  AND EXISTS(SELECT 1 FROM app.audit_log WHERE entity_table='branch_photo' AND operation='D'
   AND old_value->>'id'=pic2::text),'DELETE de foto conserva bytes e historial');
 TRUNCATE app.business_photo;
 PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.audit_log WHERE entity_table='business_photo'
  AND operation='T' AND NOT (old_value ? 'photo_data')),'TRUNCATE de fotos permitido y auditado');
 PERFORM app.toggle_favorite(uid,sid);
 PERFORM app.soft_delete_branch(sid,uid);
 PERFORM pg_temp.assert_true(NOT app.toggle_favorite(uid,sid),
  'se puede desmarcar favorito de sucursal cerrada');
 PERFORM pg_temp.expect_error(format('SELECT app.toggle_favorite(%L,%L)',uid,sid),
  'Sucursal no disponible');
 PERFORM pg_temp.assert_true((SELECT deleted_by_user_id=uid AND deleted_at IS NOT NULL FROM app.branch WHERE id=sid),'actor de baja logica');
 SET CONSTRAINTS ALL IMMEDIATE;
 PERFORM pg_temp.assert_true(
  EXISTS(SELECT 1 FROM app.business WHERE id=bid AND status <> 'archived')
  AND NOT EXISTS(SELECT 1 FROM app.branch WHERE business_id=bid AND status IN ('active','temporarily_closed')),
  'negocio vigente sin sucursales');
 -- Volver a publicar un negocio que quedo sin sucursales vigentes tambien es valido.
 PERFORM app.admin_publish_business(bid,uid);
 SET CONSTRAINTS ALL IMMEDIATE;
 PERFORM app.soft_delete_business(bid,uid);
 PERFORM pg_temp.assert_true(NOT app.is_business_owner(uid,bid),'negocio eliminado revoca acceso de propietarios');
 PERFORM pg_temp.assert_true(NOT EXISTS(
  SELECT 1 FROM app.published_branch WHERE branch_id=sid),'baja logica');
 RAISE NOTICE 'Pruebas de servicios/productos, precios, estados, horarios, busqueda, configuracion, publicacion, fotos y auditoria: OK';
END $$;

ROLLBACK;
