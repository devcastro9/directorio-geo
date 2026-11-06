CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'app') THEN
        CREATE SCHEMA app;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'catalog') THEN
        CREATE SCHEMA catalog;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'geo') THEN
        CREATE SCHEMA geo;
    END IF;
END $EF$;

CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS unaccent;

CREATE TABLE "AspNetRoles" (
    "Id" uuid NOT NULL,
    "Description" character varying(150),
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "Name" character varying(256),
    "NormalizedName" character varying(256),
    "ConcurrencyStamp" text,
    CONSTRAINT "PK_AspNetRoles" PRIMARY KEY ("Id")
);

CREATE TABLE "AspNetUsers" (
    "Id" uuid NOT NULL,
    "FirstName" character varying(150),
    "LastName" character varying(150),
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "LastLoginAt" timestamp with time zone NOT NULL,
    "UserName" character varying(256),
    "NormalizedUserName" character varying(256),
    "Email" character varying(256),
    "NormalizedEmail" character varying(256),
    "EmailConfirmed" boolean NOT NULL,
    "PasswordHash" text,
    "SecurityStamp" text,
    "ConcurrencyStamp" text,
    "PhoneNumber" text,
    "PhoneNumberConfirmed" boolean NOT NULL,
    "TwoFactorEnabled" boolean NOT NULL,
    "LockoutEnd" timestamp with time zone,
    "LockoutEnabled" boolean NOT NULL,
    "AccessFailedCount" integer NOT NULL,
    CONSTRAINT "PK_AspNetUsers" PRIMARY KEY ("Id")
);

CREATE TABLE app.audit_log (
    id bigint GENERATED ALWAYS AS IDENTITY,
    entity_schema character varying(63) NOT NULL,
    entity_table character varying(63) NOT NULL,
    entity_key character varying(200) NOT NULL,
    operation character(1) NOT NULL,
    actor_user_id character varying(200),
    changed_at timestamp with time zone NOT NULL DEFAULT (clock_timestamp()),
    transaction_id xid8 NOT NULL DEFAULT (pg_current_xact_id()),
    old_value jsonb,
    new_value jsonb,
    CONSTRAINT audit_log_pkey PRIMARY KEY (id)
);

CREATE TABLE app.business (
    id uuid NOT NULL DEFAULT (uuidv7()),
    slug character varying(160),
    legal_name character varying(250),
    trade_name character varying(200) NOT NULL,
    tax_id character varying(50),
    description text,
    status character varying(30) NOT NULL DEFAULT ('active'::character varying),
    publication_status character varying(20) NOT NULL DEFAULT ('draft'::character varying),
    price_level smallint,
    source character varying(30) NOT NULL DEFAULT ('manual'::character varying),
    source_key character varying(200),
    created_by_user_id character varying(200),
    updated_by_user_id character varying(200),
    deleted_at timestamp with time zone,
    deleted_by_user_id character varying(200),
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT business_pkey PRIMARY KEY (id)
);

CREATE TABLE catalog.category (
    id uuid NOT NULL DEFAULT (uuidv7()),
    code character varying(50) NOT NULL,
    parent_id uuid,
    name character varying(150) NOT NULL,
    description text,
    sort_order integer NOT NULL,
    active boolean NOT NULL DEFAULT TRUE,
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT category_pkey PRIMARY KEY (id),
    CONSTRAINT category_parent_id_fkey FOREIGN KEY (parent_id) REFERENCES catalog.category (id)
);

CREATE TABLE app.daily_metric (
    metric_date date NOT NULL,
    event_type character varying(50) NOT NULL,
    business_id uuid,
    branch_id uuid,
    total_count bigint NOT NULL
);

CREATE TABLE app.event (
    id uuid NOT NULL DEFAULT (uuidv7()),
    event_type character varying(50) NOT NULL,
    occurred_at timestamp with time zone NOT NULL DEFAULT (clock_timestamp()),
    user_id character varying(200),
    session_key character varying(128),
    ip_address inet NOT NULL,
    user_agent character varying(1000) NOT NULL,
    business_id uuid,
    branch_id uuid,
    category_id uuid,
    territory_id uuid,
    query_text text,
    source_ref text,
    metadata jsonb NOT NULL DEFAULT ('{}'::jsonb),
    CONSTRAINT event_pkey PRIMARY KEY (id)
);

CREATE TABLE app.event_hist (
    id uuid NOT NULL DEFAULT (uuidv7()),
    event_type character varying(50) NOT NULL,
    occurred_at timestamp with time zone NOT NULL DEFAULT (clock_timestamp()),
    user_id character varying(200),
    session_key character varying(128),
    ip_address inet NOT NULL,
    user_agent character varying(1000) NOT NULL,
    business_id uuid,
    branch_id uuid,
    category_id uuid,
    territory_id uuid,
    query_text text,
    source_ref text,
    metadata jsonb NOT NULL DEFAULT ('{}'::jsonb),
    CONSTRAINT event_hist_pkey PRIMARY KEY (id)
);

CREATE TABLE catalog.feature (
    id uuid NOT NULL DEFAULT (uuidv7()),
    code character varying(50) NOT NULL,
    name character varying(150) NOT NULL,
    description text,
    sort_order integer NOT NULL,
    active boolean NOT NULL DEFAULT TRUE,
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT feature_pkey PRIMARY KEY (id)
);

CREATE TABLE app.import_batch (
    id uuid NOT NULL DEFAULT (uuidv7()),
    file_name character varying(255) NOT NULL,
    file_format character varying(10) NOT NULL,
    status character varying(20) NOT NULL DEFAULT ('uploaded'::character varying),
    created_by_user_id character varying(200),
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    completed_at timestamp with time zone,
    total_rows integer NOT NULL,
    notes text,
    CONSTRAINT import_batch_pkey PRIMARY KEY (id)
);

CREATE TABLE app.monthly_metric (
    metric_month date NOT NULL,
    event_type character varying(50) NOT NULL,
    business_id uuid,
    branch_id uuid,
    total_count bigint NOT NULL
);

CREATE TABLE app.owner_identity (
    user_id character varying(200) NOT NULL,
    verified_at timestamp with time zone,
    verification_method character varying(30),
    expires_at timestamp with time zone,
    revoked_at timestamp with time zone,
    verified_by_user_id character varying(200),
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT owner_identity_pkey PRIMARY KEY (user_id)
);

CREATE TABLE catalog.parameter_definition (
    code character varying(100) NOT NULL,
    description text NOT NULL,
    data_type character varying(20) NOT NULL,
    default_value jsonb NOT NULL,
    min_number numeric,
    max_number numeric,
    allowed_values jsonb,
    active boolean NOT NULL DEFAULT TRUE,
    CONSTRAINT parameter_definition_pkey PRIMARY KEY (code)
);

CREATE TABLE catalog.payment_method (
    id uuid NOT NULL DEFAULT (uuidv7()),
    code character varying(50) NOT NULL,
    name character varying(150) NOT NULL,
    description text,
    sort_order integer NOT NULL,
    active boolean NOT NULL DEFAULT TRUE,
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT payment_method_pkey PRIMARY KEY (id)
);

CREATE TABLE app.search_synonym (
    id uuid NOT NULL DEFAULT (uuidv7()),
    term character varying(150) NOT NULL,
    synonym character varying(150) NOT NULL,
    locale character varying(10) NOT NULL DEFAULT ('es-BO'::character varying),
    active boolean NOT NULL DEFAULT TRUE,
    CONSTRAINT search_synonym_pkey PRIMARY KEY (id)
);

CREATE TABLE catalog.service (
    id uuid NOT NULL DEFAULT (uuidv7()),
    code character varying(50) NOT NULL,
    name character varying(150) NOT NULL,
    description text,
    sort_order integer NOT NULL,
    active boolean NOT NULL DEFAULT TRUE,
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT service_pkey PRIMARY KEY (id)
);

CREATE TABLE geo.territory (
    id uuid NOT NULL DEFAULT (uuidv7()),
    parent_id uuid,
    territory_type character varying(20) NOT NULL,
    code_ine character varying(30),
    name character varying(150) NOT NULL,
    source character varying(20) NOT NULL DEFAULT ('INE'::character varying),
    osm_type character varying(30),
    osm_id bigint,
    source_version character varying(100),
    boundary geography(MultiPolygon,4326),
    centroid geography(Point,4326),
    active boolean NOT NULL DEFAULT TRUE,
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT territory_pkey PRIMARY KEY (id),
    CONSTRAINT territory_parent_id_fkey FOREIGN KEY (parent_id) REFERENCES geo.territory (id)
);

CREATE TABLE "AspNetRoleClaims" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "RoleId" uuid NOT NULL,
    "ClaimType" text,
    "ClaimValue" text,
    CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserClaims" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "UserId" uuid NOT NULL,
    "ClaimType" text,
    "ClaimValue" text,
    CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserLogins" (
    "LoginProvider" text NOT NULL,
    "ProviderKey" text NOT NULL,
    "ProviderDisplayName" text,
    "UserId" uuid NOT NULL,
    CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
    CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserRoles" (
    "UserId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserTokens" (
    "UserId" uuid NOT NULL,
    "LoginProvider" text NOT NULL,
    "Name" text NOT NULL,
    "Value" text,
    CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
    CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE app.business_owner (
    id uuid NOT NULL DEFAULT (uuidv7()),
    business_id uuid NOT NULL,
    user_id character varying(200) NOT NULL,
    verified_at timestamp with time zone,
    verified_by_user_id character varying(200),
    revoked_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT business_owner_pkey PRIMARY KEY (id),
    CONSTRAINT business_owner_business_id_fkey FOREIGN KEY (business_id) REFERENCES app.business (id),
    CONSTRAINT business_owner_user_id_fkey FOREIGN KEY (user_id) REFERENCES app.owner_identity (user_id)
);

CREATE TABLE catalog.parameter_value (
    id uuid NOT NULL DEFAULT (uuidv7()),
    code character varying(100) NOT NULL,
    scope_type character varying(20) NOT NULL DEFAULT ('global'::character varying),
    scope_id uuid,
    value jsonb NOT NULL,
    valid_from timestamp with time zone NOT NULL DEFAULT (now()),
    valid_to timestamp with time zone,
    active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT parameter_value_pkey PRIMARY KEY (id),
    CONSTRAINT parameter_value_code_fkey FOREIGN KEY (code) REFERENCES catalog.parameter_definition (code)
);

CREATE TABLE app.branch (
    id uuid NOT NULL DEFAULT (uuidv7()),
    business_id uuid NOT NULL,
    code character varying(50),
    slug character varying(160),
    name character varying(200) NOT NULL,
    address text NOT NULL,
    territory_id uuid,
    location geography(Point,4326) NOT NULL,
    location_source character varying(20) DEFAULT ('manual'::character varying),
    status character varying(30) NOT NULL DEFAULT ('active'::character varying),
    is_primary boolean NOT NULL,
    price_level smallint,
    min_price_bob numeric(14,2),
    max_price_bob numeric(14,2),
    emergency boolean NOT NULL,
    home_service boolean NOT NULL,
    accessibility jsonb NOT NULL DEFAULT ('{}'::jsonb),
    source character varying(30) NOT NULL DEFAULT ('manual'::character varying),
    source_key character varying(200),
    deleted_at timestamp with time zone,
    deleted_by_user_id character varying(200),
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT branch_pkey PRIMARY KEY (id),
    CONSTRAINT branch_business_id_fkey FOREIGN KEY (business_id) REFERENCES app.business (id) ON DELETE RESTRICT,
    CONSTRAINT branch_territory_id_fkey FOREIGN KEY (territory_id) REFERENCES geo.territory (id)
);

CREATE TABLE app.holiday (
    id uuid NOT NULL DEFAULT (uuidv7()),
    name character varying(150) NOT NULL,
    holiday_date date NOT NULL,
    territory_id uuid,
    recurring_annually boolean NOT NULL,
    active boolean NOT NULL DEFAULT TRUE,
    CONSTRAINT holiday_pkey PRIMARY KEY (id),
    CONSTRAINT holiday_territory_id_fkey FOREIGN KEY (territory_id) REFERENCES geo.territory (id)
);

CREATE TABLE app.ad_placement (
    id uuid NOT NULL DEFAULT (uuidv7()),
    business_id uuid,
    branch_id uuid,
    slot character varying(30) NOT NULL,
    starts_at timestamp with time zone NOT NULL,
    ends_at timestamp with time zone NOT NULL,
    amount_bob numeric(14,2) NOT NULL,
    payment_status character varying(20) NOT NULL DEFAULT ('pending'::character varying),
    priority integer NOT NULL,
    status character varying(20) NOT NULL DEFAULT ('draft'::character varying),
    invoice_ref character varying(100),
    created_by_user_id character varying(200),
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT ad_placement_pkey PRIMARY KEY (id),
    CONSTRAINT ad_placement_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES app.branch (id),
    CONSTRAINT ad_placement_business_id_fkey FOREIGN KEY (business_id) REFERENCES app.business (id)
);

CREATE TABLE app.branch_category (
    branch_id uuid NOT NULL,
    category_id uuid NOT NULL,
    active boolean NOT NULL DEFAULT TRUE,
    CONSTRAINT branch_category_pkey PRIMARY KEY (branch_id, category_id),
    CONSTRAINT branch_category_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES app.branch (id),
    CONSTRAINT branch_category_category_id_fkey FOREIGN KEY (category_id) REFERENCES catalog.category (id)
);

CREATE TABLE app.branch_contact (
    id uuid NOT NULL DEFAULT (uuidv7()),
    branch_id uuid NOT NULL,
    contact_type character varying(20) NOT NULL,
    contact_value character varying(500) NOT NULL,
    is_primary boolean NOT NULL,
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT branch_contact_pkey PRIMARY KEY (id),
    CONSTRAINT branch_contact_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES app.branch (id)
);

CREATE TABLE app.branch_feature (
    branch_id uuid NOT NULL,
    feature_id uuid NOT NULL,
    active boolean NOT NULL DEFAULT TRUE,
    CONSTRAINT branch_feature_pkey PRIMARY KEY (branch_id, feature_id),
    CONSTRAINT branch_feature_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES app.branch (id),
    CONSTRAINT branch_feature_feature_id_fkey FOREIGN KEY (feature_id) REFERENCES catalog.feature (id)
);

CREATE TABLE app.branch_hour_override (
    id uuid NOT NULL DEFAULT (uuidv7()),
    branch_id uuid NOT NULL,
    service_date date NOT NULL,
    is_closed boolean NOT NULL,
    opens_at time without time zone,
    closes_at time without time zone,
    reason text,
    CONSTRAINT branch_hour_override_pkey PRIMARY KEY (id),
    CONSTRAINT branch_hour_override_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES app.branch (id)
);

CREATE TABLE app.branch_payment (
    branch_id uuid NOT NULL,
    method_id uuid NOT NULL,
    active boolean NOT NULL DEFAULT TRUE,
    CONSTRAINT branch_payment_pkey PRIMARY KEY (branch_id, method_id),
    CONSTRAINT branch_payment_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES app.branch (id),
    CONSTRAINT branch_payment_method_id_fkey FOREIGN KEY (method_id) REFERENCES catalog.payment_method (id)
);

CREATE TABLE app.branch_photo (
    id uuid NOT NULL DEFAULT (uuidv7()),
    branch_id uuid NOT NULL,
    photo_data bytea NOT NULL,
    mime_type character varying(30) NOT NULL,
    file_size bigint GENERATED ALWAYS AS (octet_length(photo_data)) STORED,
    filename character varying(255),
    alt_text character varying(300),
    is_primary boolean NOT NULL,
    sort_order integer NOT NULL,
    created_by_user_id character varying(200) NOT NULL,
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT branch_photo_pkey PRIMARY KEY (id),
    CONSTRAINT branch_photo_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES app.branch (id)
);

CREATE TABLE app.branch_service (
    branch_id uuid NOT NULL,
    service_id uuid NOT NULL,
    description text,
    active boolean NOT NULL DEFAULT TRUE,
    CONSTRAINT branch_service_pkey PRIMARY KEY (branch_id, service_id),
    CONSTRAINT branch_service_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES app.branch (id),
    CONSTRAINT branch_service_service_id_fkey FOREIGN KEY (service_id) REFERENCES catalog.service (id)
);

CREATE TABLE app.favorite (
    user_id character varying(200) NOT NULL,
    branch_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    deleted_at timestamp with time zone,
    CONSTRAINT favorite_pkey PRIMARY KEY (user_id, branch_id),
    CONSTRAINT favorite_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES app.branch (id)
);

CREATE TABLE app.import_row (
    id bigint GENERATED ALWAYS AS IDENTITY,
    batch_id uuid NOT NULL,
    row_number integer NOT NULL,
    payload jsonb NOT NULL,
    normalized_name character varying(200),
    matched_business_id uuid,
    matched_branch_id uuid,
    decision character varying(20) NOT NULL DEFAULT ('pending'::character varying),
    reason text,
    reviewed_by_user_id character varying(200),
    reviewed_at timestamp with time zone,
    CONSTRAINT import_row_pkey PRIMARY KEY (id),
    CONSTRAINT import_row_batch_id_fkey FOREIGN KEY (batch_id) REFERENCES app.import_batch (id),
    CONSTRAINT import_row_matched_branch_id_fkey FOREIGN KEY (matched_branch_id) REFERENCES app.branch (id),
    CONSTRAINT import_row_matched_business_id_fkey FOREIGN KEY (matched_business_id) REFERENCES app.business (id)
);

CREATE TABLE app.opening_hour (
    id uuid NOT NULL DEFAULT (uuidv7()),
    branch_id uuid NOT NULL,
    weekday smallint NOT NULL,
    opens_at time without time zone NOT NULL,
    closes_at time without time zone NOT NULL,
    active boolean NOT NULL DEFAULT TRUE,
    CONSTRAINT opening_hour_pkey PRIMARY KEY (id),
    CONSTRAINT opening_hour_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES app.branch (id)
);

CREATE TABLE app.review (
    id uuid NOT NULL DEFAULT (uuidv7()),
    branch_id uuid NOT NULL,
    user_id character varying(200) NOT NULL,
    rating smallint NOT NULL,
    title character varying(200),
    body text,
    moderation_status character varying(20) NOT NULL DEFAULT ('visible'::character varying),
    moderation_reason text,
    moderated_at timestamp with time zone,
    moderated_by_user_id character varying(200),
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT review_pkey PRIMARY KEY (id),
    CONSTRAINT review_branch_id_fkey FOREIGN KEY (branch_id) REFERENCES app.branch (id)
);

CREATE TABLE app.service_price (
    id uuid NOT NULL DEFAULT (uuidv7()),
    branch_id uuid NOT NULL,
    service_id uuid NOT NULL,
    amount_min_bob numeric(14,2) NOT NULL,
    amount_max_bob numeric(14,2) NOT NULL,
    active boolean NOT NULL DEFAULT TRUE,
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT service_price_pkey PRIMARY KEY (id),
    CONSTRAINT service_price_branch_id_service_id_fkey FOREIGN KEY (branch_id, service_id) REFERENCES app.branch_service (branch_id, service_id)
);

CREATE TABLE app.review_reply (
    id uuid NOT NULL DEFAULT (uuidv7()),
    review_id uuid NOT NULL,
    owner_user_id character varying(200) NOT NULL,
    reply_text text NOT NULL,
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT review_reply_pkey PRIMARY KEY (id),
    CONSTRAINT review_reply_review_id_fkey FOREIGN KEY (review_id) REFERENCES app.review (id)
);

CREATE INDEX ad_branch_idx ON app.ad_placement (branch_id) WHERE (branch_id IS NOT NULL);

CREATE INDEX ad_business_idx ON app.ad_placement (business_id) WHERE (business_id IS NOT NULL);

CREATE INDEX ad_period_idx ON app.ad_placement (starts_at, ends_at, slot) WHERE (deleted_at IS NULL);

CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");

CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");

CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");

CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");

CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");

CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");

CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");

CREATE INDEX audit_entity_idx ON app.audit_log (entity_table, entity_key, changed_at DESC);

CREATE UNIQUE INDEX branch_business_id_code_key ON app.branch (business_id, code);

CREATE INDEX branch_business_idx ON app.branch (business_id) WHERE (deleted_at IS NULL);

CREATE INDEX branch_location_gix ON app.branch USING gist (location) WHERE (deleted_at IS NULL);

CREATE UNIQUE INDEX branch_primary_one_idx ON app.branch (business_id) WHERE (is_primary AND (deleted_at IS NULL));

CREATE UNIQUE INDEX branch_slug_key ON app.branch (slug);

CREATE UNIQUE INDEX branch_source_source_key_key ON app.branch (source, source_key);

CREATE INDEX branch_territory_idx ON app.branch (territory_id) WHERE (deleted_at IS NULL);

CREATE INDEX branch_category_reverse ON app.branch_category (category_id) WHERE active;

CREATE UNIQUE INDEX contact_primary_idx ON app.branch_contact (branch_id, contact_type) WHERE (is_primary AND (deleted_at IS NULL));

CREATE INDEX "IX_branch_feature_feature_id" ON app.branch_feature (feature_id);

CREATE UNIQUE INDEX branch_hour_override_branch_id_service_date_key ON app.branch_hour_override (branch_id, service_date);

CREATE INDEX "IX_branch_payment_method_id" ON app.branch_payment (method_id);

CREATE INDEX photo_branch_idx ON app.branch_photo (branch_id, sort_order) WHERE (deleted_at IS NULL);

CREATE UNIQUE INDEX photo_primary_one_idx ON app.branch_photo (branch_id) WHERE (is_primary AND (deleted_at IS NULL));

CREATE INDEX "IX_branch_service_service_id" ON app.branch_service (service_id);

CREATE UNIQUE INDEX business_slug_key ON app.business (slug);

CREATE UNIQUE INDEX business_source_source_key_key ON app.business (source, source_key);

CREATE UNIQUE INDEX business_tax_id_key ON app.business (tax_id);

CREATE UNIQUE INDEX business_owner_business_id_user_id_key ON app.business_owner (business_id, user_id);

CREATE INDEX owner_user_idx ON app.business_owner (user_id) WHERE (revoked_at IS NULL);

CREATE UNIQUE INDEX category_code_key ON catalog.category (code);

CREATE INDEX category_parent_idx ON catalog.category (parent_id);

CREATE INDEX category_sort_idx ON catalog.category (sort_order) WHERE active;

CREATE UNIQUE INDEX daily_metric_metric_date_event_type_business_id_branch_id_key ON app.daily_metric (metric_date, event_type, business_id, branch_id);

CREATE INDEX event_branch_time ON app.event (branch_id, occurred_at) WHERE (branch_id IS NOT NULL);

CREATE INDEX event_time_brin ON app.event USING brin (occurred_at);

CREATE INDEX event_type_time ON app.event (event_type, occurred_at);

CREATE INDEX event_hist_branch_id_occurred_at_idx ON app.event_hist (branch_id, occurred_at) WHERE (branch_id IS NOT NULL);

CREATE INDEX event_hist_event_type_occurred_at_idx ON app.event_hist (event_type, occurred_at);

CREATE INDEX event_hist_occurred_at_idx ON app.event_hist USING brin (occurred_at);

CREATE INDEX favorite_branch_idx ON app.favorite (branch_id) WHERE (deleted_at IS NULL);

CREATE UNIQUE INDEX feature_code_key ON catalog.feature (code);

CREATE INDEX feature_sort_idx ON catalog.feature (sort_order) WHERE active;

CREATE INDEX holiday_date_idx ON app.holiday (holiday_date) WHERE active;

CREATE UNIQUE INDEX holiday_holiday_date_territory_id_name_key ON app.holiday (holiday_date, territory_id, name);

CREATE INDEX "IX_holiday_territory_id" ON app.holiday (territory_id);

CREATE UNIQUE INDEX import_row_batch_id_row_number_key ON app.import_row (batch_id, row_number);

CREATE INDEX import_row_decision_idx ON app.import_row (batch_id, decision);

CREATE INDEX "IX_import_row_matched_branch_id" ON app.import_row (matched_branch_id);

CREATE INDEX "IX_import_row_matched_business_id" ON app.import_row (matched_business_id);

CREATE UNIQUE INDEX monthly_metric_metric_month_event_type_business_id_branch_i_key ON app.monthly_metric (metric_month, event_type, business_id, branch_id);

CREATE INDEX opening_hour_idx ON app.opening_hour (branch_id, weekday) WHERE active;

CREATE INDEX parameter_lookup_idx ON catalog.parameter_value (code, scope_type, scope_id, valid_from DESC) WHERE active;

CREATE UNIQUE INDEX payment_method_code_key ON catalog.payment_method (code);

CREATE INDEX payment_method_sort_idx ON catalog.payment_method (sort_order) WHERE active;

CREATE INDEX review_branch_idx ON app.review (branch_id, moderation_status, created_at DESC) WHERE (deleted_at IS NULL);

CREATE INDEX review_user_idx ON app.review (user_id, created_at DESC) WHERE (deleted_at IS NULL);

CREATE UNIQUE INDEX review_reply_review_id_key ON app.review_reply (review_id);

CREATE UNIQUE INDEX search_synonym_term_synonym_locale_key ON app.search_synonym (term, synonym, locale);

CREATE INDEX synonym_term_idx ON app.search_synonym USING gin (term gin_trgm_ops);

CREATE UNIQUE INDEX service_code_key ON catalog.service (code);

CREATE INDEX service_sort_idx ON catalog.service (sort_order) WHERE active;

CREATE UNIQUE INDEX service_price_one_idx ON app.service_price (branch_id, service_id) WHERE active;

CREATE INDEX territory_gix ON geo.territory USING gist (boundary);

CREATE UNIQUE INDEX territory_osm_type_osm_id_key ON geo.territory (osm_type, osm_id);

CREATE INDEX territory_parent_idx ON geo.territory (parent_id);

CREATE UNIQUE INDEX territory_territory_type_code_ine_key ON geo.territory (territory_type, code_ine);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261106010414_InitialIdentity', '10.0.14');

COMMIT;

