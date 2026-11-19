CREATE SCHEMA IF NOT EXISTS etl;

CREATE TABLE IF NOT EXISTS etl.libcal_bookings (
    extraction_date date NOT NULL,
    row_number bigint NOT NULL CHECK (row_number > 0),
    loaded_at timestamp with time zone NOT NULL,
    source_row_id text NOT NULL,
    booking_id text NOT NULL,
    category_id text NOT NULL,
    booking_category text NOT NULL,
    campus_id text NOT NULL,
    booking_campus text NOT NULL,
    starts_at timestamp with time zone NOT NULL,
    ends_at timestamp with time zone NOT NULL,
    user_email text,
    user_name text,
    user_lastname text,
    booking_resource text,
    raw_payload jsonb NOT NULL,
    PRIMARY KEY (extraction_date, row_number),
    CHECK (ends_at > starts_at),
    CHECK ((starts_at AT TIME ZONE 'America/Lima')::date <= extraction_date
       AND (ends_at AT TIME ZONE 'America/Lima') > extraction_date::timestamp)
);
