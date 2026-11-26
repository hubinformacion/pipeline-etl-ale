CREATE SCHEMA IF NOT EXISTS etl;

CREATE TABLE IF NOT EXISTS etl.alma_loans (
    extraction_date date NOT NULL,
    phone_number text,
    campus_name text,
    circ_desk_name text,
    loan_date date NOT NULL,
    author text,
    barcode text,
    in_house_loan_indicator text,
    item_id text,
    item_loan_id text,
    loan_status text,
    loan_time text,
    mms_id text,
    title text,
    item_policy text,
    user_name text,
    user_primary_identifier text,
    preferred_email text,
    material_type text,
    CHECK (loan_date = extraction_date)
);

CREATE TABLE IF NOT EXISTS etl.alma_renewals (
    extraction_date date NOT NULL,
    campus_name text,
    author text,
    barcode text,
    item_id text,
    item_loan_id text,
    loan_status text,
    mms_id text,
    title text,
    item_policy text,
    phone_number text,
    renewal_campus_name text,
    renewal_date date NOT NULL,
    user_name text,
    material_type text,
    user_primary_identifier text,
    preferred_email text,
    renewals double precision,
    CHECK (renewal_date = extraction_date)
);

CREATE TABLE IF NOT EXISTS etl.elogim_sessions (
    extraction_date date NOT NULL,
    fecha_sesion timestamp without time zone NOT NULL,
    user_email text NOT NULL,
    identificacion text NOT NULL,
    bd text NOT NULL,
    tipo_consulta text NOT NULL,
    CHECK (fecha_sesion::date = extraction_date)
);

CREATE TABLE IF NOT EXISTS etl.libcal_bookings (
    extraction_date date NOT NULL,
    source_row_id text,
    booking_id text NOT NULL,
    category_id text NOT NULL,
    booking_category text NOT NULL,
    campus_id text NOT NULL,
    booking_campus text NOT NULL,
    booking_starts_at timestamp with time zone NOT NULL,
    booking_ends_at timestamp with time zone NOT NULL,
    user_email text,
    user_name text,
    user_lastname text,
    booking_resource text,
    booking_status text,
    booking_confirmation text,
    booking_phone text,
    booking_participant_2_email text,
    booking_participant_3_email text,
    booking_note text,
    CHECK (booking_ends_at > booking_starts_at),
    CHECK ((booking_starts_at AT TIME ZONE 'America/Lima')::date <= extraction_date
       AND (booking_ends_at AT TIME ZONE 'America/Lima') > extraction_date::timestamp)
);

-- Las cargas reemplazan un día completo y conservan los registros que comparten valores.
CREATE INDEX IF NOT EXISTS alma_loans_extraction_date_idx ON etl.alma_loans (extraction_date);
CREATE INDEX IF NOT EXISTS alma_renewals_extraction_date_idx ON etl.alma_renewals (extraction_date);
CREATE INDEX IF NOT EXISTS elogim_sessions_extraction_date_idx ON etl.elogim_sessions (extraction_date);
CREATE INDEX IF NOT EXISTS libcal_bookings_extraction_date_idx ON etl.libcal_bookings (extraction_date);

CREATE TABLE IF NOT EXISTS etl.load_status (
    dataset text NOT NULL,
    extraction_date date NOT NULL,
    row_count bigint NOT NULL CHECK (row_count >= 0),
    loaded_at timestamptz NOT NULL,
    PRIMARY KEY (dataset, extraction_date)
);
