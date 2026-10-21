-- ============================================================================
-- directorio-geo - esquema PostgreSQL/PostGIS (version corregida para MVP)
-- PostgreSQL 18 + PostGIS 3.6
-- ============================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS unaccent WITH SCHEMA public;
CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;
CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE SCHEMA IF NOT EXISTS core;
CREATE SCHEMA IF NOT EXISTS catalog;
CREATE SCHEMA IF NOT EXISTS geo;
CREATE SCHEMA IF NOT EXISTS search;
CREATE SCHEMA IF NOT EXISTS analytics;
CREATE SCHEMA IF NOT EXISTS commercial;
CREATE SCHEMA IF NOT EXISTS audit;

-- ----------------------------------------------------------------------------
-- Utilidades comunes
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.touch_updated_at() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
	NEW.updated_at := clock_timestamp();
	RETURN NEW;
END;
$$;

-- ----------------------------------------------------------------------------
-- Auditoria
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS audit.change_log (
	change_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	schema_name text NOT NULL,
	table_name text NOT NULL,
	row_id text NOT NULL,
	operation char(1) NOT NULL CHECK (operation IN ('I','U','D')),
	changed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	actor text NOT NULL DEFAULT current_user,
	transaction_id xid8 NOT NULL DEFAULT pg_current_xact_id(),
	old_data jsonb,
	new_data jsonb
);
CREATE INDEX IF NOT EXISTS change_log_table_row_idx
	ON audit.change_log (schema_name, table_name, row_id, changed_at DESC);

-- Uso: EXECUTE FUNCTION audit.capture_change('nombre_columna_pk')  (por defecto 'id')
CREATE OR REPLACE FUNCTION audit.capture_change() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, audit AS $$
DECLARE
	before_data jsonb;
	after_data jsonb;
	key_col text := COALESCE(TG_ARGV[0], 'id');
	key_value text;
BEGIN
	before_data := CASE WHEN TG_OP IN ('UPDATE','DELETE') THEN to_jsonb(OLD) END;
	after_data := CASE WHEN TG_OP IN ('INSERT','UPDATE') THEN to_jsonb(NEW) END;
	key_value := COALESCE(after_data->>key_col, before_data->>key_col, 'unknown');
	INSERT INTO audit.change_log(schema_name, table_name, row_id, operation, old_data, new_data)
	VALUES (TG_TABLE_SCHEMA, TG_TABLE_NAME, key_value, substr(TG_OP, 1, 1), before_data, after_data);
	RETURN COALESCE(NEW, OLD);
END;
$$;
REVOKE ALL ON FUNCTION audit.capture_change() FROM PUBLIC;

-- ----------------------------------------------------------------------------
-- Catalogos
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS catalog.catalog_item (
	id uuid PRIMARY KEY DEFAULT uuidv7(),
	catalog_code text NOT NULL,
	code text NOT NULL,
	parent_id uuid REFERENCES catalog.catalog_item(id),   -- jerarquia (categorias)
	name text NOT NULL,
	description text,
	metadata jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(metadata) = 'object'),
	sort_order integer NOT NULL DEFAULT 0,
	active boolean NOT NULL DEFAULT true,
	valid_from timestamptz,
	valid_to timestamptz,
	created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to > valid_from),
	UNIQUE (catalog_code, code)
);
CREATE INDEX IF NOT EXISTS catalog_item_active_idx
	ON catalog.catalog_item(catalog_code, sort_order, name) WHERE active;
CREATE INDEX IF NOT EXISTS catalog_item_parent_idx ON catalog.catalog_item(parent_id);
CREATE OR REPLACE TRIGGER catalog_item_touch BEFORE UPDATE ON catalog.catalog_item
	FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();

-- Valida que un item pertenezca al catalogo esperado.
-- Uso: EXECUTE FUNCTION catalog.enforce_catalog('columna','catalog_code', ...)
CREATE OR REPLACE FUNCTION catalog.enforce_catalog() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, catalog AS $$
DECLARE
	i integer := 0;
	v text;
	j jsonb := to_jsonb(NEW);
BEGIN
	WHILE i < TG_NARGS LOOP
		v := j->>TG_ARGV[i];
		IF v IS NOT NULL AND NOT EXISTS (
			SELECT 1 FROM catalog.catalog_item ci
			 WHERE ci.id = v::uuid AND ci.catalog_code = TG_ARGV[i + 1]) THEN
			RAISE EXCEPTION 'El item % de %.% (columna %) no pertenece al catalogo "%"',
				v, TG_TABLE_SCHEMA, TG_TABLE_NAME, TG_ARGV[i], TG_ARGV[i + 1]
				USING ERRCODE = 'foreign_key_violation';
		END IF;
		i := i + 2;
	END LOOP;
	RETURN NEW;
END;
$$;

-- Integridad de jerarquias de catalogo: mismo catalogo y sin ciclos.
CREATE OR REPLACE FUNCTION catalog.validate_parent() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog AS $$
DECLARE v_catalog text;
BEGIN
 IF NEW.parent_id IS NULL THEN RETURN NEW; END IF;
 IF NEW.parent_id = NEW.id THEN RAISE EXCEPTION 'Un item no puede ser su propio padre'; END IF;
 SELECT catalog_code INTO v_catalog FROM catalog.catalog_item WHERE id = NEW.parent_id;
 IF v_catalog IS DISTINCT FROM NEW.catalog_code THEN
   RAISE EXCEPTION 'El padre debe pertenecer al mismo catalogo';
 END IF;
 IF EXISTS (
   WITH RECURSIVE ancestors(id, parent_id) AS (
     SELECT id, parent_id FROM catalog.catalog_item WHERE id = NEW.parent_id
     UNION ALL
     SELECT p.id, p.parent_id FROM catalog.catalog_item p
       JOIN ancestors a ON p.id = a.parent_id
   ) CYCLE id SET is_cycle USING cycle_path
   SELECT 1 FROM ancestors WHERE id = NEW.id OR is_cycle
 ) THEN RAISE EXCEPTION 'Ciclo en jerarquia de catalogo'; END IF;
 RETURN NEW;
END;
$$;
CREATE OR REPLACE TRIGGER catalog_item_parent_check
 BEFORE INSERT OR UPDATE OF parent_id, catalog_code ON catalog.catalog_item
 FOR EACH ROW EXECUTE FUNCTION catalog.validate_parent();

-- ----------------------------------------------------------------------------
-- Parametros
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS catalog.parameter_definitions (
	id uuid PRIMARY KEY DEFAULT uuidv7(),
	code text NOT NULL UNIQUE,
	description text NOT NULL,
	data_type text NOT NULL CHECK (data_type IN ('boolean','integer','numeric','text','jsonb','date','timestamptz')),
	default_value jsonb,
	min_value numeric,
	max_value numeric,
	allowed_values jsonb,
	unit text,
	scope_type text NOT NULL DEFAULT 'global' CHECK (scope_type IN ('global','department','municipality','category','business','branch')),
	sensitive boolean NOT NULL DEFAULT false,
	active boolean NOT NULL DEFAULT true,
	validation jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(validation) = 'object'),
	created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	CHECK (min_value IS NULL OR max_value IS NULL OR min_value <= max_value)
);
CREATE TABLE IF NOT EXISTS catalog.parameter_scopes (
	id uuid PRIMARY KEY DEFAULT uuidv7(),
	parameter_id uuid NOT NULL REFERENCES catalog.parameter_definitions(id) ON DELETE CASCADE,
	scope_key text NOT NULL,
	precedence smallint NOT NULL DEFAULT 0,
	UNIQUE (parameter_id, scope_key)
);
CREATE TABLE IF NOT EXISTS catalog.parameter_values (
	id uuid PRIMARY KEY DEFAULT uuidv7(),
	parameter_id uuid NOT NULL REFERENCES catalog.parameter_definitions(id) ON DELETE CASCADE,
	scope_id uuid REFERENCES catalog.parameter_scopes(id) ON DELETE CASCADE,
	value jsonb NOT NULL,
	valid_from timestamptz NOT NULL DEFAULT clock_timestamp(),
	valid_to timestamptz,
	active boolean NOT NULL DEFAULT true,
	created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	CHECK (valid_to IS NULL OR valid_to > valid_from),
	UNIQUE NULLS NOT DISTINCT (parameter_id, scope_id, valid_from)
);
CREATE TABLE IF NOT EXISTS catalog.parameter_history (
	history_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	parameter_value_id uuid NOT NULL,
	parameter_id uuid NOT NULL REFERENCES catalog.parameter_definitions(id),
	scope_id uuid REFERENCES catalog.parameter_scopes(id),
	old_value jsonb,
	new_value jsonb NOT NULL,
	changed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	changed_by text NOT NULL DEFAULT current_user
);
CREATE INDEX IF NOT EXISTS parameter_values_lookup_idx
	ON catalog.parameter_values(parameter_id, active, valid_from DESC);

CREATE OR REPLACE TRIGGER parameter_definitions_touch BEFORE UPDATE ON catalog.parameter_definitions
	FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();
CREATE OR REPLACE TRIGGER parameter_values_touch BEFORE UPDATE ON catalog.parameter_values
	FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();

CREATE OR REPLACE FUNCTION catalog.log_parameter_value() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
	INSERT INTO catalog.parameter_history(parameter_value_id, parameter_id, scope_id, old_value, new_value)
	VALUES (NEW.id, NEW.parameter_id, NEW.scope_id,
	        CASE WHEN TG_OP = 'UPDATE' THEN OLD.value END, NEW.value);
	RETURN NEW;
END;
$$;
-- Validar coherencia del valor JSON con la definicion y su alcance.
CREATE OR REPLACE FUNCTION catalog.validate_parameter_value() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog AS $$
DECLARE d catalog.parameter_definitions%ROWTYPE; v_kind text; v_num numeric;
BEGIN
 SELECT * INTO STRICT d FROM catalog.parameter_definitions WHERE id = NEW.parameter_id;
 IF NEW.scope_id IS NOT NULL AND NOT EXISTS (
   SELECT 1 FROM catalog.parameter_scopes ps
   WHERE ps.id = NEW.scope_id AND ps.parameter_id = NEW.parameter_id
 ) THEN RAISE EXCEPTION 'El alcance no pertenece al parametro'; END IF;
 v_kind := jsonb_typeof(NEW.value);
 IF (d.data_type = 'boolean' AND v_kind <> 'boolean')
 OR (d.data_type IN ('integer','numeric') AND v_kind <> 'number')
 OR (d.data_type IN ('text','date','timestamptz') AND v_kind <> 'string') THEN
   RAISE EXCEPTION 'Tipo JSON incompatible con parametro % (%)', d.code, d.data_type;
 END IF;
 IF d.data_type IN ('integer','numeric') THEN
   v_num := (NEW.value #>> '{}')::numeric;
   IF d.data_type = 'integer' AND v_num <> trunc(v_num) THEN
     RAISE EXCEPTION 'Parametro % exige entero', d.code;
   END IF;
   IF (d.min_value IS NOT NULL AND v_num < d.min_value)
      OR (d.max_value IS NOT NULL AND v_num > d.max_value) THEN
     RAISE EXCEPTION 'Parametro % fuera de rango', d.code;
   END IF;
 ELSIF d.data_type = 'date' THEN
   PERFORM (NEW.value #>> '{}')::date;
 ELSIF d.data_type = 'timestamptz' THEN
   PERFORM (NEW.value #>> '{}')::timestamptz;
 END IF;
 IF d.allowed_values IS NOT NULL AND
    (jsonb_typeof(d.allowed_values) <> 'array' OR NOT (d.allowed_values @> jsonb_build_array(NEW.value))) THEN
   RAISE EXCEPTION 'Valor no permitido para parametro %', d.code;
 END IF;
 RETURN NEW;
END;
$$;
CREATE OR REPLACE TRIGGER parameter_values_validate
 BEFORE INSERT OR UPDATE ON catalog.parameter_values
 FOR EACH ROW EXECUTE FUNCTION catalog.validate_parameter_value();

-- Evitar intervalos vigentes solapados, incluso para valores globales (scope_id NULL).
ALTER TABLE catalog.parameter_values ADD CONSTRAINT parameter_values_no_overlap
 EXCLUDE USING gist (
   parameter_id WITH =,
   (coalesce(scope_id, '00000000-0000-0000-0000-000000000000'::uuid)) WITH =,
   tstzrange(valid_from, valid_to, '[)') WITH &&
 ) WHERE (active);

CREATE OR REPLACE TRIGGER parameter_values_history
	AFTER INSERT OR UPDATE OF value ON catalog.parameter_values
	FOR EACH ROW EXECUTE FUNCTION catalog.log_parameter_value();

-- Sin scope: solo valores globales. Con scope: el valor del scope si existe, si no el global.
CREATE OR REPLACE FUNCTION catalog.get_parameter(p_code text, p_scope_key text DEFAULT NULL)
RETURNS jsonb LANGUAGE sql STABLE AS $$
	SELECT COALESCE(
		(SELECT pv.value
		   FROM catalog.parameter_values pv
		   JOIN catalog.parameter_definitions pd ON pd.id = pv.parameter_id
		   LEFT JOIN catalog.parameter_scopes ps ON ps.id = pv.scope_id
		  WHERE pd.code = p_code AND pd.active AND pv.active
		    AND (pv.scope_id IS NULL OR ps.scope_key = p_scope_key)
		    AND now() >= pv.valid_from
		    AND now() < COALESCE(pv.valid_to, 'infinity'::timestamptz)
		  ORDER BY (pv.scope_id IS NOT NULL) DESC, COALESCE(ps.precedence, 0) DESC, pv.valid_from DESC
		  LIMIT 1),
		(SELECT default_value FROM catalog.parameter_definitions WHERE code = p_code AND active)
	);
$$;
CREATE OR REPLACE FUNCTION catalog.get_parameter_numeric(p_code text, p_scope_key text DEFAULT NULL)
RETURNS numeric LANGUAGE sql STABLE AS $$ SELECT (catalog.get_parameter(p_code, p_scope_key) #>> '{}')::numeric; $$;
CREATE OR REPLACE FUNCTION catalog.get_parameter_boolean(p_code text, p_scope_key text DEFAULT NULL)
RETURNS boolean LANGUAGE sql STABLE AS $$ SELECT (catalog.get_parameter(p_code, p_scope_key) #>> '{}')::boolean; $$;

-- ----------------------------------------------------------------------------
-- Territorio
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS geo.territory (
	id uuid PRIMARY KEY DEFAULT uuidv7(),
	parent_id uuid REFERENCES geo.territory(id),
	territory_type text NOT NULL CHECK (territory_type IN ('department','province','municipality','locality','city','zone','neighborhood')),
	official_code text,
	name text NOT NULL,
	source text,
	source_version text,
	boundary geometry(MultiPolygon,4326),
	active boolean NOT NULL DEFAULT true,
	UNIQUE (territory_type, official_code)
);
CREATE INDEX IF NOT EXISTS territory_parent_idx ON geo.territory(parent_id);
CREATE INDEX IF NOT EXISTS territory_boundary_gist ON geo.territory USING gist(boundary);

-- ----------------------------------------------------------------------------
-- Negocios y sucursales
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS core.business (
	id uuid PRIMARY KEY DEFAULT uuidv7(),
	slug text UNIQUE CHECK (slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
	legal_name text,
	trade_name text NOT NULL CHECK (length(trim(trade_name)) > 0),
	description text,
	tax_id text UNIQUE,                       -- se normaliza por trigger
	business_status_id uuid REFERENCES catalog.catalog_item(id),
	publication_status_id uuid REFERENCES catalog.catalog_item(id),
	verified_at timestamptz,
	verification_source text,
	verification_expires_at timestamptz,
	source text,                              -- procedencia (osm, importador, manual...)
	source_id text,
	active boolean NOT NULL DEFAULT true,
	created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	UNIQUE (source, source_id)
);

CREATE OR REPLACE FUNCTION core.normalize_business() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
	NEW.tax_id := nullif(upper(regexp_replace(COALESCE(NEW.tax_id, ''), '[^A-Za-z0-9]', '', 'g')), '');
	RETURN NEW;
END;
$$;

CREATE TABLE IF NOT EXISTS core.branch (
	id uuid PRIMARY KEY DEFAULT uuidv7(),
	business_id uuid NOT NULL REFERENCES core.business(id) ON DELETE RESTRICT,
	branch_code text,
	slug text UNIQUE CHECK (slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
	name text NOT NULL,
	address_line text NOT NULL,
	territory_id uuid REFERENCES geo.territory(id),
	location geometry(Point,4326) NOT NULL CHECK (ST_IsValid(location)),
	geog geography(Point,4326) GENERATED ALWAYS AS (location::geography) STORED,
	location_source text,                     -- gps, geocoder, osm, manual
	location_accuracy_m numeric(8,2) CHECK (location_accuracy_m IS NULL OR location_accuracy_m >= 0),
	status_id uuid REFERENCES catalog.catalog_item(id),
	is_primary boolean NOT NULL DEFAULT false,
	emergency boolean NOT NULL DEFAULT false,
	home_service boolean NOT NULL DEFAULT false,
	accessibility jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(accessibility) = 'object'),
	source text,
	source_id text,
	active boolean NOT NULL DEFAULT true,
	created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	UNIQUE (business_id, branch_code),
	UNIQUE (source, source_id)
);
CREATE UNIQUE INDEX IF NOT EXISTS one_primary_branch_idx ON core.branch(business_id) WHERE is_primary AND active;
CREATE INDEX IF NOT EXISTS branch_location_gist ON core.branch USING gist(location);
CREATE INDEX IF NOT EXISTS branch_geog_gist ON core.branch USING gist(geog);
CREATE INDEX IF NOT EXISTS branch_business_idx ON core.branch(business_id, active);
CREATE INDEX IF NOT EXISTS branch_territory_idx ON core.branch(territory_id);

-- Asigna el territorio mas especifico que contiene el punto (si no se informo uno).
CREATE OR REPLACE FUNCTION core.assign_territory() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
	IF NEW.territory_id IS NULL OR
      (TG_OP = 'UPDATE' AND NEW.location IS DISTINCT FROM OLD.location
       AND NEW.territory_id IS NOT DISTINCT FROM OLD.territory_id) THEN
		NEW.territory_id := NULL;
		SELECT t.id INTO NEW.territory_id
		  FROM geo.territory t
		 WHERE t.active AND t.boundary IS NOT NULL AND ST_Covers(t.boundary, NEW.location)
		 ORDER BY array_position(ARRAY['neighborhood','zone','locality','city','municipality','province','department'], t.territory_type)
		 LIMIT 1;
	END IF;
	RETURN NEW;
END;
$$;

CREATE OR REPLACE TRIGGER business_normalize BEFORE INSERT OR UPDATE ON core.business
	FOR EACH ROW EXECUTE FUNCTION core.normalize_business();
CREATE OR REPLACE TRIGGER business_touch BEFORE UPDATE ON core.business
	FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();
CREATE OR REPLACE TRIGGER business_catalogs BEFORE INSERT OR UPDATE ON core.business
	FOR EACH ROW EXECUTE FUNCTION catalog.enforce_catalog(
		'business_status_id','business_status','publication_status_id','publication_status');
CREATE OR REPLACE TRIGGER business_audit AFTER INSERT OR UPDATE OR DELETE ON core.business
	FOR EACH ROW EXECUTE FUNCTION audit.capture_change('id');

CREATE OR REPLACE TRIGGER branch_territory BEFORE INSERT OR UPDATE OF location, territory_id ON core.branch
	FOR EACH ROW EXECUTE FUNCTION core.assign_territory();
CREATE OR REPLACE TRIGGER branch_touch BEFORE UPDATE ON core.branch
	FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();
CREATE OR REPLACE TRIGGER branch_catalogs BEFORE INSERT OR UPDATE ON core.branch
	FOR EACH ROW EXECUTE FUNCTION catalog.enforce_catalog('status_id','branch_status');
CREATE OR REPLACE TRIGGER branch_audit AFTER INSERT OR UPDATE OR DELETE ON core.branch
	FOR EACH ROW EXECUTE FUNCTION audit.capture_change('id');

-- Las categorias se asignan por sucursal (un unico nivel de verdad).
CREATE TABLE IF NOT EXISTS core.branch_category (
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	category_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	PRIMARY KEY (branch_id, category_id)
);
CREATE INDEX IF NOT EXISTS branch_category_category_idx ON core.branch_category(category_id);
CREATE OR REPLACE TRIGGER branch_category_catalogs BEFORE INSERT OR UPDATE ON core.branch_category
	FOR EACH ROW EXECUTE FUNCTION catalog.enforce_catalog('category_id','category');

CREATE TABLE IF NOT EXISTS core.branch_service (
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	service_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	description text,
	active boolean NOT NULL DEFAULT true,
	PRIMARY KEY (branch_id, service_id)
);
CREATE INDEX IF NOT EXISTS branch_service_service_idx ON core.branch_service(service_id);
CREATE OR REPLACE TRIGGER branch_service_catalogs BEFORE INSERT OR UPDATE ON core.branch_service
	FOR EACH ROW EXECUTE FUNCTION catalog.enforce_catalog('service_id','service');

CREATE TABLE IF NOT EXISTS core.branch_feature (
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	feature_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	PRIMARY KEY (branch_id, feature_id)
);
CREATE OR REPLACE TRIGGER branch_feature_catalogs BEFORE INSERT OR UPDATE ON core.branch_feature
	FOR EACH ROW EXECUTE FUNCTION catalog.enforce_catalog('feature_id','feature');

CREATE TABLE IF NOT EXISTS core.contact (
	id uuid PRIMARY KEY DEFAULT uuidv7(),
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	contact_type_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	value text NOT NULL CHECK (length(trim(value)) > 0),
	label text,
	is_primary boolean NOT NULL DEFAULT false,
	active boolean NOT NULL DEFAULT true
);
CREATE INDEX IF NOT EXISTS contact_branch_idx ON core.contact(branch_id);
CREATE UNIQUE INDEX IF NOT EXISTS contact_one_primary_idx
	ON core.contact(branch_id, contact_type_id) WHERE is_primary AND active;
CREATE OR REPLACE TRIGGER contact_catalogs BEFORE INSERT OR UPDATE ON core.contact
	FOR EACH ROW EXECUTE FUNCTION catalog.enforce_catalog('contact_type_id','contact_type');

CREATE TABLE IF NOT EXISTS core.payment_method (
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	payment_method_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	PRIMARY KEY (branch_id, payment_method_id)
);
CREATE OR REPLACE TRIGGER payment_method_catalogs BEFORE INSERT OR UPDATE ON core.payment_method
	FOR EACH ROW EXECUTE FUNCTION catalog.enforce_catalog('payment_method_id','payment_method');

-- Auditoria de datos operativos: contactos y relaciones (el JSON conserva la clave compuesta).
CREATE OR REPLACE TRIGGER branch_category_audit AFTER INSERT OR UPDATE OR DELETE ON core.branch_category
 FOR EACH ROW EXECUTE FUNCTION audit.capture_change('branch_id');
CREATE OR REPLACE TRIGGER branch_service_audit AFTER INSERT OR UPDATE OR DELETE ON core.branch_service
 FOR EACH ROW EXECUTE FUNCTION audit.capture_change('branch_id');
CREATE OR REPLACE TRIGGER branch_feature_audit AFTER INSERT OR UPDATE OR DELETE ON core.branch_feature
 FOR EACH ROW EXECUTE FUNCTION audit.capture_change('branch_id');
CREATE OR REPLACE TRIGGER contact_audit AFTER INSERT OR UPDATE OR DELETE ON core.contact
 FOR EACH ROW EXECUTE FUNCTION audit.capture_change('id');
CREATE OR REPLACE TRIGGER payment_method_audit AFTER INSERT OR UPDATE OR DELETE ON core.payment_method
 FOR EACH ROW EXECUTE FUNCTION audit.capture_change('branch_id');

-- ----------------------------------------------------------------------------
-- Horarios (weekday: 0 = domingo ... 6 = sabado; closes_at <= opens_at cruza medianoche)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS core.opening_hour (
	id uuid PRIMARY KEY DEFAULT uuidv7(),
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	weekday smallint NOT NULL CHECK (weekday BETWEEN 0 AND 6),
	opens_at time NOT NULL,
	closes_at time NOT NULL,
	-- Semana de referencia circular (2024-01-07 = domingo). Un turno nocturno del sabado se parte
	-- en dos tramos para que tambien se compare contra los turnos del domingo.
	span tsmultirange GENERATED ALWAYS AS (
		CASE WHEN weekday = 6 AND closes_at <= opens_at THEN
			tsmultirange(
				tsrange((timestamp '2024-01-07' + weekday * interval '1 day') + opens_at,
				        timestamp '2024-01-14', '[)'),
				tsrange(timestamp '2024-01-07', timestamp '2024-01-07' + closes_at, '[)'))
		ELSE
			tsmultirange(tsrange(
				(timestamp '2024-01-07' + weekday * interval '1 day') + opens_at,
				(timestamp '2024-01-07' + weekday * interval '1 day') + closes_at
					+ CASE WHEN closes_at <= opens_at THEN interval '1 day' ELSE interval '0' END,
				'[)'))
		END
	) STORED,
	CHECK (opens_at <> closes_at),
	EXCLUDE USING gist (branch_id WITH =, span WITH &&)
);
CREATE INDEX IF NOT EXISTS opening_hour_branch_day_idx ON core.opening_hour(branch_id, weekday);

CREATE TABLE IF NOT EXISTS core.exceptional_hour (
	id uuid PRIMARY KEY DEFAULT uuidv7(),
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	service_date date NOT NULL,
	opens_at time,
	closes_at time,
	closed boolean NOT NULL DEFAULT false,
	reason text,
	CHECK ((closed AND opens_at IS NULL AND closes_at IS NULL) OR
	       (NOT closed AND opens_at IS NOT NULL AND closes_at IS NOT NULL AND opens_at <> closes_at)),
	UNIQUE (branch_id, service_date)
);

CREATE OR REPLACE TRIGGER opening_hour_audit AFTER INSERT OR UPDATE OR DELETE ON core.opening_hour
 FOR EACH ROW EXECUTE FUNCTION audit.capture_change('id');
CREATE OR REPLACE TRIGGER exceptional_hour_audit AFTER INSERT OR UPDATE OR DELETE ON core.exceptional_hour
 FOR EACH ROW EXECUTE FUNCTION audit.capture_change('id');

-- "Abierto ahora" (hora local de Bolivia).
-- Regla: una excepcion de fecha D reemplaza el horario regular del dia D. Un turno nocturno
-- que empieza el dia D-1 sigue abierto hasta su cierre en D, segun el horario vigente de D-1.
CREATE OR REPLACE FUNCTION core.is_open_now(p_branch_id uuid, p_at timestamptz DEFAULT clock_timestamp())
RETURNS boolean LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = pg_catalog AS $$
DECLARE
	local_ts timestamp := p_at AT TIME ZONE 'America/La_Paz';
	t time := local_ts::time;
	d date := local_ts::date;
	dow integer := extract(dow FROM local_ts)::integer;
	prev_dow integer := (extract(dow FROM local_ts)::integer + 6) % 7;
	ex core.exceptional_hour%ROWTYPE;
	has_ex boolean;
BEGIN
	-- 1) Tramo nocturno heredado del dia anterior
	SELECT * INTO ex FROM core.exceptional_hour WHERE branch_id = p_branch_id AND service_date = d - 1;
	has_ex := FOUND;
	IF has_ex THEN
		IF NOT ex.closed AND ex.closes_at <= ex.opens_at AND t < ex.closes_at THEN
			RETURN true;
		END IF;
	ELSIF EXISTS (
		SELECT 1 FROM core.opening_hour h
		 WHERE h.branch_id = p_branch_id AND h.weekday = prev_dow
		   AND h.closes_at <= h.opens_at AND t < h.closes_at) THEN
		RETURN true;
	END IF;

	-- 2) Turno que empieza hoy
	SELECT * INTO ex FROM core.exceptional_hour WHERE branch_id = p_branch_id AND service_date = d;
	IF FOUND THEN
		IF ex.closed THEN RETURN false; END IF;
		RETURN CASE WHEN ex.closes_at > ex.opens_at
		            THEN t >= ex.opens_at AND t < ex.closes_at
		            ELSE t >= ex.opens_at END;
	END IF;
	RETURN EXISTS (
		SELECT 1 FROM core.opening_hour h
		 WHERE h.branch_id = p_branch_id AND h.weekday = dow
		   AND ((h.closes_at > h.opens_at AND t >= h.opens_at AND t < h.closes_at)
		     OR (h.closes_at <= h.opens_at AND t >= h.opens_at)));
END;
$$;

-- ----------------------------------------------------------------------------
-- Precios (moneda unica: BOB). Precio fijo: amount_min = amount_max.
-- Un solo precio vigente por sucursal y servicio; el historial queda en audit.change_log.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS core.price (
	id uuid PRIMARY KEY DEFAULT uuidv7(),
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	service_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
    CONSTRAINT price_branch_service_fk FOREIGN KEY (branch_id, service_id)
      REFERENCES core.branch_service(branch_id, service_id) ON DELETE CASCADE,
	amount_min numeric(18,2) NOT NULL CHECK (amount_min >= 0),
	amount_max numeric(18,2) NOT NULL CHECK (amount_max >= 0),
	active boolean NOT NULL DEFAULT true,
	created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	CONSTRAINT price_range_ck CHECK (amount_max >= amount_min)
);
CREATE UNIQUE INDEX IF NOT EXISTS price_one_active_idx ON core.price(branch_id, service_id) WHERE active;
CREATE INDEX IF NOT EXISTS price_service_idx ON core.price(service_id);
CREATE OR REPLACE TRIGGER price_touch BEFORE UPDATE ON core.price
	FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();
CREATE OR REPLACE TRIGGER price_catalogs BEFORE INSERT OR UPDATE ON core.price
	FOR EACH ROW EXECUTE FUNCTION catalog.enforce_catalog('service_id','service');
CREATE OR REPLACE TRIGGER price_audit AFTER INSERT OR UPDATE OR DELETE ON core.price
	FOR EACH ROW EXECUTE FUNCTION audit.capture_change('id');

-- ----------------------------------------------------------------------------
-- Campanas comerciales
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS commercial.campaign (
	id uuid PRIMARY KEY DEFAULT uuidv7(),
	name text NOT NULL,
	campaign_type_id uuid REFERENCES catalog.catalog_item(id),
	starts_at timestamptz NOT NULL,
	ends_at timestamptz NOT NULL,
	priority integer NOT NULL DEFAULT 0,
	status_id uuid REFERENCES catalog.catalog_item(id),
	budget numeric(14,2) CHECK (budget IS NULL OR budget >= 0),
	impression_limit bigint CHECK (impression_limit IS NULL OR impression_limit >= 0),
	click_limit bigint CHECK (click_limit IS NULL OR click_limit >= 0),
	eligibility jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(eligibility) = 'object'),
	created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	CHECK (ends_at > starts_at)
);
CREATE TABLE IF NOT EXISTS commercial.campaign_business (
	campaign_id uuid NOT NULL REFERENCES commercial.campaign(id) ON DELETE CASCADE,
	business_id uuid NOT NULL REFERENCES core.business(id) ON DELETE CASCADE,
	PRIMARY KEY (campaign_id, business_id)
);
CREATE TABLE IF NOT EXISTS commercial.campaign_branch (
	campaign_id uuid NOT NULL REFERENCES commercial.campaign(id) ON DELETE CASCADE,
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	PRIMARY KEY (campaign_id, branch_id)
);
CREATE TABLE IF NOT EXISTS commercial.campaign_category (
	campaign_id uuid NOT NULL REFERENCES commercial.campaign(id) ON DELETE CASCADE,
	category_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	PRIMARY KEY (campaign_id, category_id)
);
CREATE OR REPLACE TRIGGER campaign_category_catalogs BEFORE INSERT OR UPDATE ON commercial.campaign_category
 FOR EACH ROW EXECUTE FUNCTION catalog.enforce_catalog('category_id','category');

CREATE TABLE IF NOT EXISTS commercial.campaign_territory (
	campaign_id uuid NOT NULL REFERENCES commercial.campaign(id) ON DELETE CASCADE,
	territory_id uuid NOT NULL REFERENCES geo.territory(id),
	PRIMARY KEY (campaign_id, territory_id)
);
CREATE TABLE IF NOT EXISTS commercial.campaign_keyword (
	campaign_id uuid NOT NULL REFERENCES commercial.campaign(id) ON DELETE CASCADE,
	keyword text NOT NULL,
	PRIMARY KEY (campaign_id, keyword)
);
CREATE OR REPLACE TRIGGER campaign_touch BEFORE UPDATE ON commercial.campaign
	FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();
CREATE OR REPLACE TRIGGER campaign_catalogs BEFORE INSERT OR UPDATE ON commercial.campaign
	FOR EACH ROW EXECUTE FUNCTION catalog.enforce_catalog(
		'campaign_type_id','campaign_type','status_id','campaign_status');
CREATE OR REPLACE TRIGGER campaign_audit AFTER INSERT OR UPDATE OR DELETE ON commercial.campaign
	FOR EACH ROW EXECUTE FUNCTION audit.capture_change('id');

-- ----------------------------------------------------------------------------
-- Analitica
-- ----------------------------------------------------------------------------
-- Sin FKs hacia negocio/sucursal/categoria/territorio: tabla de alto volumen de escritura.
CREATE TABLE IF NOT EXISTS analytics.event (
	id uuid PRIMARY KEY DEFAULT uuidv7(),
	event_type_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	business_id uuid,
	branch_id uuid,
	category_id uuid,
	territory_id uuid,
	session_key text,
	anonymous_key text,
	query_text text,
	filters jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(filters) = 'object'),
	metadata jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(metadata) = 'object')
);
CREATE INDEX IF NOT EXISTS event_type_date_idx ON analytics.event(event_type_id, occurred_at);
CREATE INDEX IF NOT EXISTS event_branch_date_idx ON analytics.event(branch_id, occurred_at) WHERE branch_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS event_occurred_brin ON analytics.event USING brin (occurred_at);

CREATE TABLE IF NOT EXISTS analytics.daily_metric (
	metric_date date NOT NULL,
	event_type_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	business_id uuid,
	branch_id uuid,
	category_id uuid,
	territory_id uuid,
	total_count bigint NOT NULL DEFAULT 0 CHECK (total_count >= 0),
	CONSTRAINT daily_metric_grain_uk
		UNIQUE NULLS NOT DISTINCT (metric_date, event_type_id, business_id, branch_id, category_id, territory_id)
);
CREATE INDEX IF NOT EXISTS daily_metric_branch_date_idx ON analytics.daily_metric(branch_id, metric_date);

CREATE TABLE IF NOT EXISTS analytics.monthly_metric (
	metric_month date NOT NULL CHECK (metric_month = date_trunc('month', metric_month)::date),
	event_type_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	business_id uuid,
	branch_id uuid,
	category_id uuid,
	territory_id uuid,
	total_count bigint NOT NULL DEFAULT 0 CHECK (total_count >= 0),
	CONSTRAINT monthly_metric_grain_uk
		UNIQUE NULLS NOT DISTINCT (metric_month, event_type_id, business_id, branch_id, category_id, territory_id)
);

-- Dia local de America/La_Paz; idempotente (borra y reinserta el dia).
CREATE OR REPLACE FUNCTION analytics.consolidate_daily(p_date date) RETURNS void
LANGUAGE plpgsql AS $$
DECLARE
	v_from timestamptz := p_date::timestamp AT TIME ZONE 'America/La_Paz';
	v_to timestamptz := (p_date + 1)::timestamp AT TIME ZONE 'America/La_Paz';
BEGIN
	DELETE FROM analytics.daily_metric WHERE metric_date = p_date;
	INSERT INTO analytics.daily_metric(metric_date, event_type_id, business_id, branch_id,
	                                   category_id, territory_id, total_count)
	SELECT p_date, event_type_id, business_id, branch_id, category_id, territory_id, count(*)
	  FROM analytics.event
	 WHERE occurred_at >= v_from AND occurred_at < v_to
	 GROUP BY event_type_id, business_id, branch_id, category_id, territory_id
	ON CONFLICT (metric_date, event_type_id, business_id, branch_id, category_id, territory_id)
	DO UPDATE SET total_count = EXCLUDED.total_count;
END;
$$;

CREATE OR REPLACE FUNCTION analytics.consolidate_monthly(p_month date) RETURNS void
LANGUAGE plpgsql AS $$
DECLARE
	v_month date := date_trunc('month', p_month)::date;
	v_next date := (date_trunc('month', p_month) + interval '1 month')::date;
BEGIN
	DELETE FROM analytics.monthly_metric WHERE metric_month = v_month;
	INSERT INTO analytics.monthly_metric(metric_month, event_type_id, business_id, branch_id,
	                                     category_id, territory_id, total_count)
	SELECT v_month, event_type_id, business_id, branch_id, category_id, territory_id, sum(total_count)
	  FROM analytics.daily_metric
	 WHERE metric_date >= v_month AND metric_date < v_next
	 GROUP BY event_type_id, business_id, branch_id, category_id, territory_id
	ON CONFLICT (metric_month, event_type_id, business_id, branch_id, category_id, territory_id)
	DO UPDATE SET total_count = EXCLUDED.total_count;
END;
$$;

-- ----------------------------------------------------------------------------
-- Busqueda
-- ----------------------------------------------------------------------------
-- unaccent no es IMMUTABLE: se fija el diccionario y se declara IMMUTABLE (patron estandar).
CREATE OR REPLACE FUNCTION search.immutable_unaccent(value text) RETURNS text
LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT AS
$$ SELECT public.unaccent('public.unaccent'::regdictionary, value) $$;

CREATE OR REPLACE FUNCTION search.normalize_text(value text) RETURNS text
LANGUAGE sql IMMUTABLE PARALLEL SAFE AS
$$ SELECT lower(search.immutable_unaccent(coalesce(value, ''))) $$;

-- Contenido PUBLICO: solo sucursales publicadas (negocio y sucursal activos,
-- publication_status = 'published', ni negocio ni sucursal cerrados definitivamente).
-- Estas vistas se ejecutan con los privilegios de su propietario: el rol directorio_public
-- no necesita (ni tiene) acceso a las tablas core.*.
CREATE OR REPLACE VIEW search.published_branch AS
SELECT br.id AS branch_id, b.id AS business_id, b.trade_name, b.description,
       br.name AS branch_name, br.address_line, br.location, br.geog, br.territory_id,
       b.verified_at,
       (b.verified_at IS NOT NULL
        AND (b.verification_expires_at IS NULL OR b.verification_expires_at > now())) AS is_verified,
       br.emergency, br.home_service,
       b.slug AS business_slug, br.slug AS branch_slug
  FROM core.branch br
  JOIN core.business b ON b.id = br.business_id
  JOIN catalog.catalog_item pub ON pub.id = b.publication_status_id
       AND pub.catalog_code = 'publication_status' AND pub.code = 'published'
  LEFT JOIN catalog.catalog_item bst ON bst.id = b.business_status_id
  LEFT JOIN catalog.catalog_item brs ON brs.id = br.status_id
 WHERE b.active AND br.active
   AND bst.code IS DISTINCT FROM 'closed_permanently'
   AND brs.code IS DISTINCT FROM 'closed_permanently';

CREATE OR REPLACE VIEW search.published_category AS
SELECT bc.branch_id, ci.id AS category_id, ci.code, ci.name, ci.parent_id
  FROM core.branch_category bc
  JOIN search.published_branch pb ON pb.branch_id = bc.branch_id
  JOIN catalog.catalog_item ci ON ci.id = bc.category_id AND ci.active;

CREATE OR REPLACE VIEW search.published_service AS
SELECT bs.branch_id, ci.id AS service_id, ci.code, ci.name, bs.description
  FROM core.branch_service bs
  JOIN search.published_branch pb ON pb.branch_id = bs.branch_id
  JOIN catalog.catalog_item ci ON ci.id = bs.service_id AND ci.active
 WHERE bs.active;

CREATE OR REPLACE VIEW search.published_price AS
SELECT p.branch_id, p.service_id, p.amount_min, p.amount_max
  FROM core.price p
  JOIN search.published_branch pb ON pb.branch_id = p.branch_id
 WHERE p.active;

CREATE OR REPLACE VIEW search.published_contact AS
SELECT c.id AS contact_id, c.branch_id, ct.code AS contact_type, c.value, c.label, c.is_primary
  FROM core.contact c
  JOIN search.published_branch pb ON pb.branch_id = c.branch_id
  JOIN catalog.catalog_item ct ON ct.id = c.contact_type_id
 WHERE c.active;

CREATE OR REPLACE VIEW search.published_opening_hour AS
SELECT h.branch_id, h.weekday, h.opens_at, h.closes_at
  FROM core.opening_hour h
  JOIN search.published_branch pb ON pb.branch_id = h.branch_id;

CREATE OR REPLACE VIEW search.published_exceptional_hour AS
SELECT e.branch_id, e.service_date, e.opens_at, e.closes_at, e.closed, e.reason
  FROM core.exceptional_hour e
  JOIN search.published_branch pb ON pb.branch_id = e.branch_id
 WHERE e.service_date >= (now() AT TIME ZONE 'America/La_Paz')::date - 1;

-- Documento de busqueda precalculado: solo datos publicados.
-- Refrescar despues de publicar, despublicar o editar; ejecutar desde backend.
-- Si se necesita ocultacion inmediata, despublicar filtra find_branches por published_branch
-- aun antes del refresco, pero la vista materializada debe permanecer privada.
CREATE MATERIALIZED VIEW IF NOT EXISTS search.branch_document AS
SELECT d.branch_id,
       d.name_norm,
       setweight(to_tsvector('simple', d.name_norm), 'A')
       || setweight(to_tsvector('simple', d.tags_norm), 'B')
       || setweight(to_tsvector('simple', d.rest_norm), 'D') AS tsv
  FROM (
	SELECT pb.branch_id,
           search.normalize_text(concat_ws(' ', pb.trade_name, pb.branch_name)) AS name_norm,
           search.normalize_text(concat_ws(' ', cats.names, svcs.names)) AS tags_norm,
           search.normalize_text(concat_ws(' ', pb.description, pb.address_line)) AS rest_norm
      FROM search.published_branch pb
	  LEFT JOIN LATERAL (
		SELECT string_agg(ci.name, ' ') AS names
		  FROM core.branch_category bc JOIN catalog.catalog_item ci ON ci.id = bc.category_id
		 WHERE bc.branch_id = pb.branch_id AND ci.active) cats ON true
	  LEFT JOIN LATERAL (
		SELECT string_agg(ci.name, ' ') AS names
		  FROM core.branch_service bs JOIN catalog.catalog_item ci ON ci.id = bs.service_id
		 WHERE bs.branch_id = pb.branch_id AND bs.active AND ci.active) svcs ON true
  ) d;
CREATE UNIQUE INDEX IF NOT EXISTS branch_document_pk ON search.branch_document(branch_id);
CREATE INDEX IF NOT EXISTS branch_document_tsv_gin ON search.branch_document USING gin(tsv);
CREATE INDEX IF NOT EXISTS branch_document_name_trgm ON search.branch_document USING gin(name_norm public.gin_trgm_ops);

CREATE OR REPLACE FUNCTION search.refresh_documents() RETURNS void
LANGUAGE sql AS $$ REFRESH MATERIALIZED VIEW CONCURRENTLY search.branch_document; $$;

CREATE OR REPLACE FUNCTION search.find_branches(
	p_query text DEFAULT NULL, p_location geometry DEFAULT NULL, p_radius_m integer DEFAULT NULL,
	p_category_id uuid DEFAULT NULL, p_service_id uuid DEFAULT NULL,
	p_verified_only boolean DEFAULT false, p_limit integer DEFAULT 50, p_offset integer DEFAULT 0)
RETURNS TABLE(branch_id uuid, business_id uuid, trade_name text, branch_name text,
              distance_m double precision, text_rank real, score double precision)
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = pg_catalog, public AS $$
#variable_conflict use_column
DECLARE
	v_q text := nullif(trim(coalesce(p_query, '')), '');
	v_norm text;
	v_tsq tsquery;
	v_geog geography;
	w_text numeric := COALESCE(catalog.get_parameter_numeric('search.weight.text'), 1);
	w_dist numeric := COALESCE(catalog.get_parameter_numeric('search.weight.distance'), 1);
	w_ver numeric := COALESCE(catalog.get_parameter_numeric('search.weight.verified'), 0);
BEGIN
    IF p_limit IS NULL OR p_limit < 1 OR p_limit > 100 THEN
      RAISE EXCEPTION 'p_limit debe estar entre 1 y 100';
    END IF;
    IF p_offset IS NULL OR p_offset < 0 OR p_offset > 10000 THEN
      RAISE EXCEPTION 'p_offset debe estar entre 0 y 10000';
    END IF;
    IF p_radius_m IS NOT NULL AND (p_radius_m < 1 OR p_radius_m > 100000) THEN
      RAISE EXCEPTION 'p_radius_m debe estar entre 1 y 100000';
    END IF;
    IF length(coalesce(p_query,'')) > 200 THEN
      RAISE EXCEPTION 'p_query excede 200 caracteres';
    END IF;
	IF v_q IS NOT NULL THEN
		v_norm := search.normalize_text(v_q);
		v_tsq := websearch_to_tsquery('simple', v_norm);
	END IF;
    IF p_location IS NOT NULL AND (ST_GeometryType(p_location) <> 'ST_Point' OR ST_IsEmpty(p_location)) THEN
      RAISE EXCEPTION 'p_location debe ser un punto no vacio';
    END IF;
	IF p_location IS NOT NULL THEN
		v_geog := (CASE WHEN ST_SRID(p_location) = 0 THEN ST_SetSRID(p_location, 4326)
		                ELSE ST_Transform(p_location, 4326) END)::geography;
	END IF;

	RETURN QUERY
	WITH candidates AS (
		SELECT pb.branch_id, pb.business_id, pb.trade_name, pb.branch_name, pb.is_verified,
		       CASE WHEN v_geog IS NULL THEN NULL ELSE ST_Distance(pb.geog, v_geog) END AS distance_m,
		       CASE WHEN v_q IS NULL THEN 0::real
		            ELSE greatest(ts_rank_cd(doc.tsv, v_tsq), similarity(doc.name_norm, v_norm))::real END AS text_rank
		  FROM search.published_branch pb
		  JOIN search.branch_document doc ON doc.branch_id = pb.branch_id
		 WHERE (v_geog IS NULL OR p_radius_m IS NULL OR ST_DWithin(pb.geog, v_geog, p_radius_m))
		   AND (v_q IS NULL OR doc.tsv @@ v_tsq OR doc.name_norm % v_norm)
		   AND (NOT p_verified_only OR pb.is_verified)
		   AND (p_category_id IS NULL OR EXISTS (
		          SELECT 1 FROM core.branch_category x
		           WHERE x.branch_id = pb.branch_id AND x.category_id = p_category_id))
		   AND (p_service_id IS NULL OR EXISTS (
		          SELECT 1 FROM core.branch_service x
		           WHERE x.branch_id = pb.branch_id AND x.service_id = p_service_id AND x.active))
	)
	SELECT c.branch_id, c.business_id, c.trade_name, c.branch_name, c.distance_m, c.text_rank,
	       (c.text_rank * w_text
	        + CASE WHEN c.distance_m IS NULL THEN 0 ELSE 1 / (1 + c.distance_m / 1000) END * w_dist
	        + CASE WHEN c.is_verified THEN w_ver ELSE 0 END)::double precision AS score
	  FROM candidates c
	 ORDER BY score DESC, c.distance_m NULLS LAST, c.branch_id
	 LIMIT greatest(p_limit, 1) OFFSET greatest(p_offset, 0);
END;
$$;

-- Demanda insatisfecha: busquedas sin resultados (requiere event_type 'search_no_results').
CREATE OR REPLACE VIEW analytics.unsatisfied_demand AS
SELECT (e.occurred_at AT TIME ZONE 'America/La_Paz')::date AS demand_date, e.query_text,
       e.territory_id, count(*) AS searches_without_results
  FROM analytics.event e JOIN catalog.catalog_item et ON et.id = e.event_type_id
 WHERE et.catalog_code = 'event_type' AND et.code = 'search_no_results'
   AND nullif(trim(e.query_text), '') IS NOT NULL
 GROUP BY 1, e.query_text, e.territory_id;

-- ----------------------------------------------------------------------------
-- Semillas minimas (sin datos de negocio; solo lo que el esquema necesita para funcionar)
-- ----------------------------------------------------------------------------
INSERT INTO catalog.catalog_item(catalog_code, code, name, sort_order) VALUES
	('publication_status','draft','Borrador',10),
	('publication_status','pending_review','Pendiente de revision',20),
	('publication_status','published','Publicado',30),
	('publication_status','rejected','Rechazado',40),
	('publication_status','archived','Archivado',50),
	('business_status','active','Activo',10),
	('business_status','closed_temporarily','Cerrado temporalmente',20),
	('business_status','closed_permanently','Cerrado definitivamente',30),
	('branch_status','active','Activa',10),
	('branch_status','closed_temporarily','Cerrada temporalmente',20),
	('branch_status','closed_permanently','Cerrada definitivamente',30),
	('contact_type','phone','Telefono',10),
	('contact_type','whatsapp','WhatsApp',20),
	('contact_type','email','Correo electronico',30),
	('contact_type','website','Sitio web',40),
	('contact_type','facebook','Facebook',50),
	('contact_type','instagram','Instagram',60),
	('event_type','search','Busqueda',10),
	('event_type','search_no_results','Busqueda sin resultados',20),
	('event_type','branch_view','Vista de sucursal',30),
	('event_type','contact_click','Clic en contacto',40),
	('event_type','directions_click','Clic en como llegar',50)
ON CONFLICT (catalog_code, code) DO NOTHING;

INSERT INTO catalog.parameter_definitions(code, description, data_type, default_value, min_value) VALUES
	('search.weight.text','Peso del rank textual en la busqueda','numeric','1'::jsonb,0),
	('search.weight.distance','Peso de la cercania en la busqueda','numeric','1'::jsonb,0),
	('search.weight.verified','Bonus por negocio verificado vigente','numeric','0.2'::jsonb,0)
ON CONFLICT (code) DO NOTHING;

COMMIT;