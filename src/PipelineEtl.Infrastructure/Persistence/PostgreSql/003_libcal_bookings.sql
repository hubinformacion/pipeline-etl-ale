CREATE SCHEMA IF NOT EXISTS etl;

CREATE TABLE IF NOT EXISTS etl.libcal_bookings (
    extraction_date date NOT NULL,
    row_number bigint NOT NULL CHECK (row_number > 0),
    loaded_at timestamp with time zone NOT NULL,
    source_row_id text NOT NULL,
    booking_id text NOT NULL,
    category_id text NOT NULL,
    category_name text NOT NULL,
    campus_id text NOT NULL,
    campus_name text NOT NULL,
    starts_at timestamp with time zone NOT NULL,
    ends_at timestamp with time zone NOT NULL,
    user_email text,
    user_name text,
    user_lastname text,
    account text,
    resource_name text,
    reservation_status text,
    check_in_status text,
    form_answer_1 text,
    form_answer_2 text,
    raw_payload jsonb NOT NULL,
    PRIMARY KEY (extraction_date, row_number),
    CHECK (ends_at > starts_at),
    CHECK ((starts_at AT TIME ZONE 'America/Lima')::date <= extraction_date
       AND (ends_at AT TIME ZONE 'America/Lima') > extraction_date::timestamp)
);

CREATE OR REPLACE VIEW etl.libcal_report AS
SELECT
    user_email,
    user_name,
    user_lastname,
    (ARRAY['enero','febrero','marzo','abril','mayo','junio',
           'julio','agosto','septiembre','octubre','noviembre','diciembre'])
        [EXTRACT(MONTH FROM local_start)::int] AS booking_month,
    EXTRACT(MONTH FROM local_start)::int AS "booking_month#",
    user_email AS booking_account_email,
    resource_name AS booking_resource,
    category_name AS booking_category,
    campus_name AS booking_campus,
    local_start::date AS booking_date,
    local_start::time AS booking_start,
    (ends_at AT TIME ZONE 'America/Lima')::time AS booking_end,
    EXTRACT(EPOCH FROM (ends_at - starts_at)) / 60 AS booking_duration,
    EXTRACT(HOUR FROM local_start)::int AS booking_hour,
    CASE lower(btrim(reservation_status))
        WHEN 'confirmed' THEN 'Confirmado'
        WHEN 'cancelled' THEN 'Cancelado'
        WHEN 'canceled' THEN 'Cancelado'
        WHEN 'tentative' THEN 'Tentativo'
        WHEN 'mediated tentative' THEN 'Tentativo (mediación)'
        WHEN 'denied' THEN 'Denegado'
        WHEN 'mediated denied' THEN 'Denegado (mediación)'
        ELSE reservation_status
    END AS booking_status,
    CASE lower(btrim(check_in_status))
        WHEN 'in' THEN 'Asistió'
        WHEN 'out' THEN 'Asistió'
        WHEN '-' THEN 'Sin registro de asistencia'
        ELSE check_in_status
    END AS booking_confirmation,
    form_answer_1 AS booking_form_answer_1,
    form_answer_2 AS booking_form_answer_2,
    extraction_date,
    row_number,
    booking_id,
    loaded_at
FROM (
    SELECT b.*, starts_at AT TIME ZONE 'America/Lima' AS local_start
    FROM etl.libcal_bookings b
) bookings;
