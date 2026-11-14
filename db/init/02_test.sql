-- PostgreSQL 18 + PostGIS 3.6. Ejecutar despues de 01_schema.sql en una BD de pruebas.
-- psql -v ON_ERROR_STOP=1 -f db/init/02_test.sql
-- Todas las modificaciones, incluidas tablas auxiliares y auditoria, se revierten.
BEGIN;
SELECT set_config('bolivianegocios.actor_id','00000000-0000-0000-0000-000000000001',true);

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

-- Catalogos de estados precargados y relaciones que sustituyen las listas CHECK.
DO $$
DECLARE r record; actual text[];
BEGIN
 FOR r IN SELECT * FROM (VALUES
  ('business_status',ARRAY['active','temporarily_closed','permanently_closed']),
  ('publication_status',ARRAY['draft','pending','published','rejected','archived']),
  ('subscription_status',ARRAY['pending','active','expired','cancelled']),
  ('subscription_payment_status',ARRAY['pending','paid','failed','refunded','cancelled']),
  ('review_moderation_status',ARRAY['visible','hidden','flagged']),
  ('ad_payment_status',ARRAY['pending','paid','refunded','cancelled']),
  ('ad_status',ARRAY['draft','active','paused','completed','cancelled']),
  ('import_status',ARRAY['uploaded','validating','needs_review','approved','applied','failed'])
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
  ('branch','status','business_status'),
  ('business','publication_status','publication_status'),
  ('business_subscription','status','subscription_status'),
  ('subscription_payment','status','subscription_payment_status'),
  ('review','moderation_status','review_moderation_status'),
  ('ad_placement','payment_status','ad_payment_status'),
  ('ad_placement','status','ad_status'),
  ('import_batch','status','import_status')
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
 'INSERT INTO app.business(trade_name,publication_status) VALUES(''Publicacion invalida'',''__invalid__'')',
 'foreign key');
SELECT pg_temp.expect_error(
 'SELECT app.moderate_review(NULL,''__invalid__'',NULL,NULL)',
 'Estado invalido');


-- El event trigger debe cubrir automaticamente tablas nuevas, sin tocar public.
CREATE TABLE app.__audit_test (a integer,b integer,label text,PRIMARY KEY(a,b));
CREATE TABLE public.__audit_test (id integer PRIMARY KEY);
INSERT INTO app.__audit_test VALUES(1,2,'insert');
UPDATE app.__audit_test SET label='update' WHERE a=1;
INSERT INTO app.__audit_test VALUES(1,2,'upsert')
ON CONFLICT(a,b) DO UPDATE SET label=excluded.label;
MERGE INTO app.__audit_test t USING (VALUES(1,2,'merge')) s(a,b,label)
ON t.a=s.a AND t.b=s.b WHEN MATCHED THEN UPDATE SET label=s.label;
DELETE FROM app.__audit_test WHERE a=1;
INSERT INTO app.__audit_test VALUES(3,4,'truncate');
TRUNCATE app.__audit_test;
INSERT INTO public.__audit_test VALUES(1);

SELECT pg_temp.assert_true(
 (SELECT count(*)=7 FROM app.audit_log WHERE entity_table='__audit_test' AND entity_schema='app'),
 'auditoria de INSERT, UPDATE, UPSERT, MERGE, DELETE y TRUNCATE');
SELECT pg_temp.assert_true(
 EXISTS(SELECT 1 FROM app.audit_log WHERE entity_table='__audit_test' AND operation='T'
 AND old_value->>'label'='truncate' AND entity_key::jsonb='{"a":3,"b":4}'::jsonb),
 'instantanea previa de TRUNCATE y clave compuesta');
SELECT pg_temp.assert_true(
 NOT EXISTS(SELECT 1 FROM app.audit_log WHERE entity_table='__audit_test' AND entity_schema='public'),
 'exclusion de public');
SELECT pg_temp.assert_true(
 NOT EXISTS (
 SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE n.nspname IN ('app','geo','catalog') AND c.relkind IN ('r','p')
 AND c.oid<>'app.audit_log'::regclass AND NOT EXISTS(
 SELECT 1 FROM pg_trigger t WHERE t.tgrelid=c.oid AND t.tgname='audit_capture_row' AND t.tgenabled='O')),
 'cobertura de todas las tablas del dominio');
SELECT pg_temp.expect_error(
 'UPDATE app.audit_log SET entity_key=''alterado'' WHERE id=(SELECT min(id) FROM app.audit_log)',
 'inmutable');
SELECT pg_temp.expect_error('TRUNCATE app.audit_log','inmutable');
SELECT pg_temp.expect_error(
 'INSERT INTO app.audit_log(entity_schema,entity_table,entity_key,operation) VALUES(''app'',''falso'',''1'',''I'')',
 'inmutable');

DO $$
DECLARE
 bid uuid; sid uuid; sid2 uuid; cat uuid; pic1 uuid; pic2 uuid; adid uuid; item_id uuid; unassigned_item_id uuid; no_price_id uuid; test_type text;
 uid uuid:='00000000-0000-0000-0000-000000000001';
 p geography:=ST_SetSRID(ST_MakePoint(-68.1193,-16.4897),4326)::geography;
 imagebytes bytea:=decode('ffd8ff010203040506070809','hex');
BEGIN
 SELECT id INTO STRICT cat FROM catalog.category WHERE code='pharmacy';
 INSERT INTO app.owner_identity(user_id,verified_at,verification_method) VALUES(uid,now(),'admin');
 bid:=app.create_business_with_branch(uid,'Farmacia prueba','Casa central','Av. de prueba 123',
  -68.1193,-16.4897,p_category_ids=>ARRAY[cat]);
 SELECT id INTO STRICT sid FROM app.branch WHERE business_id=bid;
 SET CONSTRAINTS ALL IMMEDIATE;
 SET CONSTRAINTS ALL DEFERRED;
 PERFORM pg_temp.assert_true(
  EXISTS(SELECT 1 FROM app.business WHERE id=bid AND publication_status='published'),
  'publicacion con categoria');

 PERFORM pg_temp.expect_error(
  format('INSERT INTO app.business(trade_name,publication_status) VALUES(''Sin categoria'',''published'');
   SET CONSTRAINTS ALL IMMEDIATE'), 'categoria');
 SET CONSTRAINTS ALL DEFERRED;

 INSERT INTO app.branch(business_id,name,address,location,is_primary)
 VALUES(bid,'Otra principal','Direccion de prueba',p,true) RETURNING id INTO sid2;
 INSERT INTO app.branch_category(branch_id,category_id) VALUES(sid2,cat);
 SET CONSTRAINTS ALL IMMEDIATE;
 SET CONSTRAINTS ALL DEFERRED;
 PERFORM pg_temp.assert_true(
  (SELECT count(*)=2 FROM app.branch WHERE business_id=bid AND is_primary),'varias sucursales principales');
 PERFORM app.soft_delete_branch(sid2,uid);

 -- Servicios y productos con precio fijo, rango y precio opcional.
 FOR test_type IN SELECT unnest(ARRAY['S','P']) LOOP
  INSERT INTO catalog.service(code,name,item_type)
  VALUES('test_'||test_type,'Item de prueba '||test_type,test_type) RETURNING id INTO item_id;
  INSERT INTO app.branch_service(branch_id,service_id) VALUES(sid,item_id);
  INSERT INTO app.service_price(branch_id,service_id,amount_min_bob,amount_max_bob)
  VALUES(sid,item_id,25,25);
  PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.service_price
   WHERE branch_id=sid AND service_id=item_id AND active AND amount_min_bob=amount_max_bob),
   'precio fijo para tipo '||test_type);
  UPDATE app.service_price SET active=false WHERE branch_id=sid AND service_id=item_id;
  INSERT INTO app.service_price(branch_id,service_id,amount_min_bob,amount_max_bob)
  VALUES(sid,item_id,20,30);
  PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.service_price
   WHERE branch_id=sid AND service_id=item_id AND active AND amount_min_bob<amount_max_bob),
   'rango de precio para tipo '||test_type);
  PERFORM pg_temp.expect_error(format(
   'UPDATE app.service_price SET amount_max_bob=10 WHERE branch_id=%L AND service_id=%L AND active',sid,item_id),
   'check constraint');
  PERFORM pg_temp.expect_error(format(
   'INSERT INTO app.service_price(branch_id,service_id,amount_min_bob,amount_max_bob) VALUES(%L,%L,1,2)',sid,item_id),
   'unique constraint');
  INSERT INTO catalog.service(code,name,item_type)
  VALUES('no_price_'||test_type,'Sin precio '||test_type,test_type) RETURNING id INTO no_price_id;
  INSERT INTO app.branch_service(branch_id,service_id) VALUES(sid,no_price_id);
  PERFORM pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM app.service_price
   WHERE branch_id=sid AND service_id=no_price_id AND active),'precio opcional para tipo '||test_type);
 END LOOP;
 INSERT INTO catalog.service(code,name) VALUES('default_service','Servicio predeterminado') RETURNING id INTO unassigned_item_id;
 PERFORM pg_temp.assert_true((SELECT item_type='S' FROM catalog.service WHERE id=unassigned_item_id),'tipo S predeterminado');
 PERFORM pg_temp.expect_error(format(
  'INSERT INTO app.service_price(branch_id,service_id,amount_min_bob,amount_max_bob) VALUES(%L,%L,1,2)',sid,unassigned_item_id),
  'foreign key');
 PERFORM pg_temp.expect_error(
  'INSERT INTO catalog.service(code,name,item_type) VALUES(''invalid_type'',''Tipo invalido'',''X'')','check constraint');
 PERFORM pg_temp.expect_error(
  'INSERT INTO catalog.service(code,name,item_type) VALUES(''null_type'',''Tipo nulo'',NULL)','not-null constraint');
 PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.audit_log WHERE entity_schema='catalog'
  AND entity_table='service' AND operation='I' AND new_value->>'item_type'='P'),'auditoria de productos');

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
 INSERT INTO app.holiday(name,holiday_date) VALUES('Feriado prueba','2026-11-10');
 PERFORM pg_temp.assert_true(NOT app.is_open_at(sid,'2026-11-10 01:00:00-04'),
  'feriado cierra todo el dia');
 UPDATE app.branch_hour_override SET active=true,is_closed=false,is_24_hours=true,
  opens_at='00:00',closes_at='00:00' WHERE branch_id=sid;
 PERFORM pg_temp.assert_true(app.is_open_at(sid,'2026-11-10 01:00:00-04'),
  'excepcion 24 horas prevalece sobre feriado');
 UPDATE app.branch_hour_override SET active=false WHERE branch_id=sid;
 UPDATE app.holiday SET active=false WHERE name='Feriado prueba';
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
 PERFORM pg_temp.assert_true(app.toggle_favorite(uid,sid),'favorito on');
 PERFORM pg_temp.assert_true(NOT app.toggle_favorite(uid,sid),'favorito off');

 PERFORM pg_temp.assert_true(
  EXISTS(SELECT 1 FROM app.find_branches(NULL,p,5000) f WHERE f.branch_id=sid
   AND ST_Equals(f.location::geometry,p::geometry) AND f.branch_status='active'),
  'busqueda devuelve ubicacion y estados');
 PERFORM pg_temp.assert_true(
  EXISTS(SELECT 1 FROM app.find_import_duplicates('FARMACIA PRUEBA') f WHERE f.branch_id=sid),
  'normalizacion de duplicados');
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

 INSERT INTO app.ad_placement(branch_id,slot,starts_at,ends_at,amount_bob,status)
 VALUES(sid,'map_highlight',now()-interval '1 hour',now()+interval '1 hour',10,'active')
 RETURNING id INTO adid;
 PERFORM pg_temp.assert_true(NOT EXISTS(SELECT 1 FROM app.active_ads() WHERE ad_id=adid),
  'pago requerido por defecto');
 INSERT INTO catalog.parameter_value(code,value) VALUES('ad.require_paid','false');
 PERFORM pg_temp.assert_true(EXISTS(SELECT 1 FROM app.active_ads() WHERE ad_id=adid),
  'parametro permite publicidad no pagada');
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
 PERFORM pg_temp.assert_true(EXISTS(
  SELECT 1 FROM app.audit_log WHERE entity_table='daily_metric' AND operation='D'),
  'auditoria de DELETE al recomputar metricas');
 PERFORM pg_temp.assert_true(EXISTS(
  SELECT 1 FROM app.audit_log WHERE entity_table='branch_photo' AND operation='I'
  AND new_value ? 'photo_data'),'auditoria completa de fotos');
 PERFORM pg_temp.assert_true(NOT EXISTS(
  SELECT 1 FROM app.audit_log WHERE transaction_id=pg_current_xact_id() AND actor_user_id IS DISTINCT FROM uid),
  'actor de la transaccion');

 PERFORM app.soft_delete_branch(sid,uid);
 SET CONSTRAINTS ALL IMMEDIATE;
 PERFORM pg_temp.assert_true(
  EXISTS(SELECT 1 FROM app.business WHERE id=bid AND deleted_at IS NULL)
  AND NOT EXISTS(SELECT 1 FROM app.branch WHERE business_id=bid AND deleted_at IS NULL),
  'negocio vigente sin sucursales');
 PERFORM app.soft_delete_business(bid,uid);
 PERFORM pg_temp.assert_true(NOT EXISTS(
  SELECT 1 FROM app.published_branch WHERE branch_id=sid),'baja logica');
 RAISE NOTICE 'Pruebas de servicios/productos, precios, estados, horarios, busqueda, configuracion, publicacion, fotos y auditoria: OK';
END $$;

ROLLBACK;
