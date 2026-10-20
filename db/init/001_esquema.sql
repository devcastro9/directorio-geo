-- ============================================================================
-- directorio-geo - esquema PostgreSQL/PostGIS
-- PostgreSQL 18 + PostGIS 3.6. Datos de catalogos, territorio y OSM se cargan
-- mediante migraciones/importadores posteriores; este archivo no inventa datos.
-- ============================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS unaccent;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE EXTENSION IF NOT EXISTS pgcrypto;
-- La imagen de base debe incluir pgRouting para habilitar geo.road_*.
CREATE EXTENSION IF NOT EXISTS pgrouting;

CREATE SCHEMA IF NOT EXISTS core;
CREATE SCHEMA IF NOT EXISTS catalog;
CREATE SCHEMA IF NOT EXISTS geo;
CREATE SCHEMA IF NOT EXISTS search;
CREATE SCHEMA IF NOT EXISTS analytics;
CREATE SCHEMA IF NOT EXISTS commercial;
CREATE SCHEMA IF NOT EXISTS audit;

CREATE OR REPLACE FUNCTION public.touch_updated_at() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
	NEW.updated_at := clock_timestamp();
	RETURN NEW;
END;
$$;

CREATE TABLE IF NOT EXISTS audit.change_log (
	change_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	schema_name text NOT NULL,
	table_name text NOT NULL,
	row_id text NOT NULL,
	operation char(1) NOT NULL CHECK (operation IN ('I','U','D')),
	changed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	actor text NOT NULL DEFAULT current_user,
	transaction_id bigint NOT NULL DEFAULT txid_current(),
	old_data jsonb,
	new_data jsonb
);
CREATE INDEX IF NOT EXISTS change_log_table_row_idx
	ON audit.change_log (schema_name, table_name, row_id, changed_at DESC);

CREATE OR REPLACE FUNCTION audit.capture_change() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, public, audit AS $$
DECLARE
	before_data jsonb;
	after_data jsonb;
	key_value text;
BEGIN
	before_data := CASE WHEN TG_OP IN ('UPDATE','DELETE') THEN to_jsonb(OLD) END;
	after_data := CASE WHEN TG_OP IN ('INSERT','UPDATE') THEN to_jsonb(NEW) END;
	key_value := COALESCE((after_data->>'id'), (before_data->>'id'), 'unknown');
	INSERT INTO audit.change_log(schema_name, table_name, row_id, operation,
															 old_data, new_data)
	VALUES (TG_TABLE_SCHEMA, TG_TABLE_NAME, key_value, substr(TG_OP, 1, 1),
					before_data, after_data);
	RETURN COALESCE(NEW, OLD);
END;
$$;

CREATE TABLE IF NOT EXISTS catalog.catalog_item (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
	catalog_code text NOT NULL,
	code text NOT NULL,
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
CREATE INDEX IF NOT EXISTS catalog_item_active_idx ON catalog.catalog_item(catalog_code, sort_order, name) WHERE active;
CREATE TRIGGER catalog_item_touch BEFORE UPDATE ON catalog.catalog_item FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();

CREATE TABLE IF NOT EXISTS catalog.parameter_definitions (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
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
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
	parameter_id uuid NOT NULL REFERENCES catalog.parameter_definitions(id) ON DELETE CASCADE,
	scope_key text NOT NULL,
	precedence smallint NOT NULL DEFAULT 0,
	UNIQUE (parameter_id, scope_key)
);
CREATE TABLE IF NOT EXISTS catalog.parameter_values (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
	parameter_id uuid NOT NULL REFERENCES catalog.parameter_definitions(id) ON DELETE CASCADE,
	scope_id uuid REFERENCES catalog.parameter_scopes(id) ON DELETE CASCADE,
	value jsonb NOT NULL,
	valid_from timestamptz NOT NULL DEFAULT clock_timestamp(),
	valid_to timestamptz,
	active boolean NOT NULL DEFAULT true,
	created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	CHECK (valid_to IS NULL OR valid_to > valid_from),
	UNIQUE (parameter_id, scope_id, valid_from)
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
CREATE INDEX IF NOT EXISTS parameter_values_lookup_idx ON catalog.parameter_values(parameter_id, active, valid_from DESC);
CREATE TRIGGER parameter_definitions_touch BEFORE UPDATE ON catalog.parameter_definitions FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();
CREATE TRIGGER parameter_values_touch BEFORE UPDATE ON catalog.parameter_values FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();

CREATE OR REPLACE FUNCTION catalog.get_parameter(p_code text, p_scope_key text DEFAULT NULL)
RETURNS jsonb LANGUAGE sql STABLE AS $$
	SELECT COALESCE(
		(SELECT pv.value FROM catalog.parameter_values pv
		 JOIN catalog.parameter_definitions pd ON pd.id = pv.parameter_id
		 LEFT JOIN catalog.parameter_scopes ps ON ps.id = pv.scope_id
		 WHERE pd.code = p_code AND pd.active AND pv.active
		   AND (p_scope_key IS NULL OR ps.scope_key = p_scope_key)
		   AND clock_timestamp() BETWEEN pv.valid_from AND COALESCE(pv.valid_to, 'infinity'::timestamptz)
		 ORDER BY COALESCE(ps.precedence, 0) DESC, pv.valid_from DESC LIMIT 1),
		(SELECT default_value FROM catalog.parameter_definitions WHERE code = p_code AND active)
	);
$$;
CREATE OR REPLACE FUNCTION catalog.get_parameter_numeric(p_code text, p_scope_key text DEFAULT NULL)
RETURNS numeric LANGUAGE sql STABLE AS $$ SELECT (catalog.get_parameter(p_code, p_scope_key)#>>'{}')::numeric; $$;
CREATE OR REPLACE FUNCTION catalog.get_parameter_boolean(p_code text, p_scope_key text DEFAULT NULL)
RETURNS boolean LANGUAGE sql STABLE AS $$ SELECT (catalog.get_parameter(p_code, p_scope_key)#>>'{}')::boolean; $$;

CREATE TABLE IF NOT EXISTS geo.territory (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
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

CREATE TABLE IF NOT EXISTS core.business (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
	legal_name text,
	trade_name text NOT NULL CHECK (length(trim(trade_name)) > 0),
	description text,
	tax_id text UNIQUE,
	business_status_id uuid REFERENCES catalog.catalog_item(id),
	publication_status_id uuid REFERENCES catalog.catalog_item(id),
	verified_at timestamptz,
	verification_source text,
	verification_expires_at timestamptz,
	active boolean NOT NULL DEFAULT true,
	created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	updated_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE TABLE IF NOT EXISTS core.branch (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
	business_id uuid NOT NULL REFERENCES core.business(id) ON DELETE CASCADE,
	branch_code text,
	name text NOT NULL,
	address_line text NOT NULL,
	territory_id uuid REFERENCES geo.territory(id),
	location geometry(Point,4326) NOT NULL CHECK (ST_IsValid(location)),
	status_id uuid REFERENCES catalog.catalog_item(id),
	is_primary boolean NOT NULL DEFAULT false,
	emergency boolean NOT NULL DEFAULT false,
	home_service boolean NOT NULL DEFAULT false,
	accessibility jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(accessibility) = 'object'),
	active boolean NOT NULL DEFAULT true,
	created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	UNIQUE (business_id, branch_code),
	CHECK (ST_X(location) BETWEEN -70.5 AND -57.4 AND ST_Y(location) BETWEEN -23 AND -9)
);
CREATE UNIQUE INDEX IF NOT EXISTS one_primary_branch_idx ON core.branch(business_id) WHERE is_primary AND active;
CREATE INDEX IF NOT EXISTS branch_location_gist ON core.branch USING gist(location);
CREATE INDEX IF NOT EXISTS branch_business_idx ON core.branch(business_id, active);
CREATE TRIGGER business_touch BEFORE UPDATE ON core.business FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();
CREATE TRIGGER branch_touch BEFORE UPDATE ON core.branch FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();
CREATE TRIGGER business_audit AFTER INSERT OR UPDATE OR DELETE ON core.business FOR EACH ROW EXECUTE FUNCTION audit.capture_change();
CREATE TRIGGER branch_audit AFTER INSERT OR UPDATE OR DELETE ON core.branch FOR EACH ROW EXECUTE FUNCTION audit.capture_change();

CREATE TABLE IF NOT EXISTS core.business_category (
	business_id uuid NOT NULL REFERENCES core.business(id) ON DELETE CASCADE,
	category_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	PRIMARY KEY (business_id, category_id)
);
CREATE TABLE IF NOT EXISTS core.branch_category (
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	category_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	PRIMARY KEY (branch_id, category_id)
);
CREATE TABLE IF NOT EXISTS core.branch_service (
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	service_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	description text,
	active boolean NOT NULL DEFAULT true,
	PRIMARY KEY (branch_id, service_id)
);
CREATE TABLE IF NOT EXISTS core.branch_feature (
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	feature_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	PRIMARY KEY (branch_id, feature_id)
);
CREATE TABLE IF NOT EXISTS core.contact (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	contact_type_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	value text NOT NULL,
	label text,
	is_primary boolean NOT NULL DEFAULT false,
	active boolean NOT NULL DEFAULT true
);
CREATE TABLE IF NOT EXISTS core.payment_method (
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	payment_method_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	PRIMARY KEY (branch_id, payment_method_id)
);

CREATE TABLE IF NOT EXISTS core.opening_hour (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	weekday smallint NOT NULL CHECK (weekday BETWEEN 0 AND 6),
	opens_at time NOT NULL,
	closes_at time NOT NULL,
	CHECK (opens_at <> closes_at)
);
CREATE INDEX IF NOT EXISTS opening_hour_branch_day_idx ON core.opening_hour(branch_id, weekday);
CREATE TABLE IF NOT EXISTS core.exceptional_hour (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
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
CREATE TABLE IF NOT EXISTS core.price (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	service_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	amount numeric(14,2) NOT NULL CHECK (amount >= 0),
	currency char(3) NOT NULL DEFAULT 'BOB' CHECK (currency = 'BOB'),
	valid_during tstzrange NOT NULL DEFAULT tstzrange(clock_timestamp(), NULL, '[)'),
	active boolean NOT NULL DEFAULT true,
	created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	EXCLUDE USING gist (branch_id WITH =, service_id WITH =, valid_during WITH &&) WHERE (active)
);
CREATE TRIGGER price_audit AFTER INSERT OR UPDATE OR DELETE ON core.price FOR EACH ROW EXECUTE FUNCTION audit.capture_change();

CREATE TABLE IF NOT EXISTS core.professional (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
	first_name text NOT NULL,
	last_name text NOT NULL,
	public_name text,
	biography text,
	verified_at timestamptz,
	active boolean NOT NULL DEFAULT true,
	created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	updated_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE TABLE IF NOT EXISTS core.specialty (
	professional_id uuid NOT NULL REFERENCES core.professional(id) ON DELETE CASCADE,
	specialty_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	is_subspecialty boolean NOT NULL DEFAULT false,
	PRIMARY KEY (professional_id, specialty_id)
);
CREATE TABLE IF NOT EXISTS core.credential (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
	professional_id uuid NOT NULL REFERENCES core.professional(id) ON DELETE CASCADE,
	credential_type_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	registration_number text NOT NULL,
	issuer text,
	valid_from date,
	valid_to date,
	declared_verified boolean NOT NULL DEFAULT false,
	verified_at timestamptz,
	UNIQUE (credential_type_id, registration_number)
);
CREATE TABLE IF NOT EXISTS core.professional_branch (
	professional_id uuid NOT NULL REFERENCES core.professional(id) ON DELETE CASCADE,
	branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
	active boolean NOT NULL DEFAULT true,
	PRIMARY KEY (professional_id, branch_id)
);
CREATE TABLE IF NOT EXISTS core.professional_service (
	professional_id uuid NOT NULL,
	branch_id uuid NOT NULL,
	service_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	PRIMARY KEY (professional_id, branch_id, service_id),
	FOREIGN KEY (professional_id, branch_id) REFERENCES core.professional_branch(professional_id, branch_id)
);

CREATE TABLE IF NOT EXISTS search.synonym (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
	term text NOT NULL,
	synonym text NOT NULL,
	active boolean NOT NULL DEFAULT true,
	UNIQUE (term, synonym)
);
CREATE OR REPLACE FUNCTION search.normalize_text(value text) RETURNS text
LANGUAGE sql IMMUTABLE PARALLEL SAFE AS $$ SELECT lower(unaccent(coalesce(value, ''))); $$;

CREATE TABLE IF NOT EXISTS commercial.campaign (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
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
CREATE TRIGGER campaign_touch BEFORE UPDATE ON commercial.campaign FOR EACH ROW EXECUTE FUNCTION public.touch_updated_at();
CREATE TRIGGER campaign_audit AFTER INSERT OR UPDATE OR DELETE ON commercial.campaign FOR EACH ROW EXECUTE FUNCTION audit.capture_change();

CREATE TABLE IF NOT EXISTS geo.road_network (
	id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
	name text NOT NULL,
	source text NOT NULL,
	source_version text,
	active boolean NOT NULL DEFAULT false,
	imported_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE TABLE IF NOT EXISTS geo.road_vertex (
	id bigint GENERATED ALWAYS AS IDENTITY,
	network_id uuid NOT NULL REFERENCES geo.road_network(id) ON DELETE CASCADE,
	geom geometry(Point,4326) NOT NULL,
	PRIMARY KEY (network_id, id)
);
CREATE TABLE IF NOT EXISTS geo.road_edge (
	id bigint GENERATED ALWAYS AS IDENTITY,
	network_id uuid NOT NULL REFERENCES geo.road_network(id) ON DELETE CASCADE,
	source bigint NOT NULL,
	target bigint NOT NULL,
	geom geometry(LineString,4326) NOT NULL,
	cost double precision NOT NULL CHECK (cost >= 0),
	reverse_cost double precision NOT NULL DEFAULT -1,
	speed_kmh numeric(6,2) CHECK (speed_kmh IS NULL OR speed_kmh > 0),
	mode text NOT NULL DEFAULT 'car',
	restriction jsonb NOT NULL DEFAULT '{}'::jsonb,
	CHECK (source <> target),
	FOREIGN KEY (network_id, source) REFERENCES geo.road_vertex(network_id, id),
	FOREIGN KEY (network_id, target) REFERENCES geo.road_vertex(network_id, id),
	PRIMARY KEY (network_id, id)
);
CREATE INDEX IF NOT EXISTS road_vertex_geom_gist ON geo.road_vertex USING gist(geom);
CREATE INDEX IF NOT EXISTS road_edge_geom_gist ON geo.road_edge USING gist(geom);
CREATE INDEX IF NOT EXISTS road_edge_network_mode_idx ON geo.road_edge(network_id, mode);

CREATE TABLE IF NOT EXISTS analytics.event (
	id uuid DEFAULT gen_random_uuid(),
	event_type_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
	business_id uuid REFERENCES core.business(id),
	branch_id uuid REFERENCES core.branch(id),
	category_id uuid REFERENCES catalog.catalog_item(id),
	territory_id uuid REFERENCES geo.territory(id),
	session_key text,
	anonymous_key text,
	query_text text,
	filters jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(filters) = 'object'),
	metadata jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(metadata) = 'object'),
	primary key (id, occurred_at)
) PARTITION BY RANGE (occurred_at);
CREATE TABLE IF NOT EXISTS analytics.event_default PARTITION OF analytics.event DEFAULT;
CREATE INDEX IF NOT EXISTS event_type_date_idx ON analytics.event(event_type_id, occurred_at);
CREATE INDEX IF NOT EXISTS event_branch_date_idx ON analytics.event(branch_id, occurred_at);
CREATE TABLE IF NOT EXISTS analytics.daily_metric (
	metric_date date NOT NULL,
	event_type_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	business_id uuid,
	branch_id uuid,
	category_id uuid,
	territory_id uuid,
	total_count bigint NOT NULL DEFAULT 0,
	unique_count bigint NOT NULL DEFAULT 0,
	PRIMARY KEY (metric_date, event_type_id, business_id, branch_id, category_id, territory_id)
);
CREATE TABLE IF NOT EXISTS analytics.monthly_metric (
	metric_month date NOT NULL,
	event_type_id uuid NOT NULL REFERENCES catalog.catalog_item(id),
	business_id uuid,
	branch_id uuid,
	category_id uuid,
	territory_id uuid,
	total_count bigint NOT NULL DEFAULT 0,
	unique_count bigint NOT NULL DEFAULT 0,
	PRIMARY KEY (metric_month, event_type_id, business_id, branch_id, category_id, territory_id)
);

CREATE OR REPLACE FUNCTION analytics.consolidate_daily(p_date date) RETURNS void
LANGUAGE sql AS $$
	INSERT INTO analytics.daily_metric(metric_date,event_type_id,business_id,branch_id,category_id,territory_id,total_count,unique_count)
	SELECT p_date, event_type_id, business_id, branch_id, category_id, territory_id,
	       count(*), count(DISTINCT COALESCE(anonymous_key, session_key, id::text))
	  FROM analytics.event WHERE occurred_at >= p_date AND occurred_at < p_date + 1
	 GROUP BY event_type_id,business_id,branch_id,category_id,territory_id
	ON CONFLICT (metric_date,event_type_id,business_id,branch_id,category_id,territory_id)
	DO UPDATE SET total_count = EXCLUDED.total_count, unique_count = EXCLUDED.unique_count;
$$;
CREATE OR REPLACE FUNCTION analytics.consolidate_monthly(p_month date) RETURNS void
LANGUAGE sql AS $$
	INSERT INTO analytics.monthly_metric(metric_month,event_type_id,business_id,branch_id,category_id,territory_id,total_count,unique_count)
	SELECT date_trunc('month', p_month)::date, event_type_id, business_id, branch_id, category_id, territory_id,
	       sum(total_count), sum(unique_count)
	  FROM analytics.daily_metric WHERE metric_date >= date_trunc('month', p_month)::date
	    AND metric_date < (date_trunc('month', p_month) + interval '1 month')::date
	 GROUP BY event_type_id,business_id,branch_id,category_id,territory_id
	ON CONFLICT (metric_month,event_type_id,business_id,branch_id,category_id,territory_id)
	DO UPDATE SET total_count = EXCLUDED.total_count, unique_count = EXCLUDED.unique_count;
$$;

CREATE OR REPLACE VIEW search.published_branch AS
SELECT br.id AS branch_id, b.id AS business_id, b.trade_name, b.description,
       br.name AS branch_name, br.address_line, br.location, br.territory_id,
       b.verified_at, br.emergency, br.home_service
  FROM core.branch br JOIN core.business b ON b.id = br.business_id
 WHERE b.active AND br.active;

CREATE OR REPLACE FUNCTION search.find_branches(
	p_query text DEFAULT NULL, p_location geometry DEFAULT NULL, p_radius_m integer DEFAULT NULL,
	p_category_id uuid DEFAULT NULL, p_service_id uuid DEFAULT NULL,
	p_verified_only boolean DEFAULT false, p_limit integer DEFAULT 50, p_offset integer DEFAULT 0)
RETURNS TABLE(branch_id uuid, business_id uuid, trade_name text, branch_name text,
              distance_m double precision, text_rank real, score double precision)
LANGUAGE sql STABLE AS $$
	WITH branch_text AS (
		SELECT pb.branch_id,
			concat_ws(' ', pb.trade_name, pb.description, pb.branch_name, pb.address_line,
				string_agg(DISTINCT category.name, ' '), string_agg(DISTINCT service.name, ' ')) AS text_value
		FROM search.published_branch pb
		LEFT JOIN core.branch_category branch_category ON branch_category.branch_id = pb.branch_id
		LEFT JOIN catalog.catalog_item category ON category.id = branch_category.category_id
		LEFT JOIN core.branch_service branch_service ON branch_service.branch_id = pb.branch_id AND branch_service.active
		LEFT JOIN catalog.catalog_item service ON service.id = branch_service.service_id
		GROUP BY pb.branch_id, pb.trade_name, pb.description, pb.branch_name, pb.address_line
	), candidates AS (
		SELECT pb.*, CASE WHEN p_location IS NULL THEN NULL ELSE ST_Distance(pb.location::geography, p_location::geography) END AS distance_m,
		  CASE WHEN nullif(trim(p_query), '') IS NULL THEN 0::real
		       ELSE ts_rank_cd(to_tsvector('simple', search.normalize_text(bt.text_value)), plainto_tsquery('simple', search.normalize_text(p_query))) END AS text_rank
		FROM search.published_branch pb JOIN branch_text bt ON bt.branch_id = pb.branch_id
		WHERE (p_location IS NULL OR p_radius_m IS NULL OR ST_DWithin(pb.location::geography, p_location::geography, p_radius_m))
		  AND (p_query IS NULL OR nullif(trim(p_query), '') IS NULL OR to_tsvector('simple', search.normalize_text(bt.text_value)) @@ plainto_tsquery('simple', search.normalize_text(p_query)))
		  AND (p_verified_only = false OR pb.verified_at IS NOT NULL)
		  AND (p_category_id IS NULL OR EXISTS (SELECT 1 FROM core.branch_category x WHERE x.branch_id = pb.branch_id AND x.category_id = p_category_id))
		  AND (p_service_id IS NULL OR EXISTS (SELECT 1 FROM core.branch_service x WHERE x.branch_id = pb.branch_id AND x.service_id = p_service_id AND x.active))
	)
	SELECT branch_id, business_id, trade_name, branch_name, distance_m, text_rank,
	       (text_rank * COALESCE(catalog.get_parameter_numeric('search.weight.text'), 1)
	        + CASE WHEN distance_m IS NULL THEN 0 ELSE 1 / (1 + distance_m / 1000) END
	          * COALESCE(catalog.get_parameter_numeric('search.weight.distance'), 1)
	        + CASE WHEN verified_at IS NULL THEN 0 ELSE COALESCE(catalog.get_parameter_numeric('search.weight.verified'), 0) END) AS score
	  FROM candidates ORDER BY score DESC, distance_m NULLS LAST, branch_id
	 LIMIT greatest(p_limit, 1) OFFSET greatest(p_offset, 0);
$$;

CREATE OR REPLACE VIEW analytics.unsatisfied_demand AS
SELECT date_trunc('day', e.occurred_at)::date AS demand_date, e.query_text,
       e.territory_id, count(*) AS searches_without_results
  FROM analytics.event e JOIN catalog.catalog_item et ON et.id = e.event_type_id
 WHERE et.code = 'search_no_results' AND nullif(trim(e.query_text), '') IS NOT NULL
 GROUP BY 1, e.query_text, e.territory_id;

COMMIT;
