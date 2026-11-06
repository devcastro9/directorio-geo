-- Ejecutar DESPUES de 01_schema.sql. Las pruebas dejan la BD sin datos de prueba (ROLLBACK).
BEGIN;

DO $$
DECLARE bid uuid; sid uuid; uid uuid:='00000000-0000-0000-0000-000000000001'; p geometry;
BEGIN
 INSERT INTO app.owner_identity(user_id,verified_at,verification_method) VALUES(uid,now(),'admin');
 bid:=app.create_business_with_branch(uid,'Comercio prueba','Casa central','Av. de prueba 123',-68.1193,-16.4897);
 IF NOT EXISTS (SELECT 1 FROM app.business WHERE id=bid AND publication_status='published') THEN
  RAISE EXCEPTION 'Fallo publicacion automatica'; END IF;
 SELECT id INTO sid FROM app.branch WHERE business_id=bid;
 IF sid IS NULL THEN RAISE EXCEPTION 'Falta sucursal'; END IF;
    INSERT INTO app.review(branch_id,user_id,rating,body) VALUES(sid,uid,5,'Muy bueno'),(sid,uid,4,'Bien');
 IF (SELECT count(*) FROM app.review WHERE branch_id=sid)<>2 THEN RAISE EXCEPTION 'Fallo resenas multiples'; END IF;
    IF NOT app.toggle_favorite(uid,sid) THEN RAISE EXCEPTION 'Fallo favorito on'; END IF;
    IF app.toggle_favorite(uid,sid) THEN RAISE EXCEPTION 'Fallo favorito off'; END IF;
 INSERT INTO app.event(event_type,ip_address,user_agent,branch_id,occurred_at)
 VALUES('branch_view','192.0.2.10','SmokeTest/1.0',sid,now()-interval '8 days');
 IF app.archive_events(now()-interval '7 days')<1 THEN RAISE EXCEPTION 'Fallo archive_events'; END IF;
 IF NOT EXISTS (SELECT 1 FROM app.event_hist WHERE branch_id=sid) THEN RAISE EXCEPTION 'Historico vacio'; END IF;
 p:=ST_SetSRID(ST_MakePoint(-68.1193,-16.4897),4326);
 IF NOT EXISTS (SELECT 1 FROM app.find_branches(NULL,p,5000) f WHERE f.branch_id=sid) THEN
  RAISE EXCEPTION 'No aparece en busqueda espacial'; END IF;
    PERFORM app.soft_delete_business(bid,uid);
 IF EXISTS(SELECT 1 FROM app.published_branch WHERE branch_id=sid) THEN
  RAISE EXCEPTION 'Baja logica no surte efecto'; END IF;
 RAISE NOTICE 'Smoke tests de creacion, consulta, resenas, favoritos, historico y baja logica: OK';
END $$;

ROLLBACK;