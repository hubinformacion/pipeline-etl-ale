-- PostgreSQL computes these values for existing rows and when importing new rows.
ALTER TABLE etl.libcal_bookings
    ADD COLUMN IF NOT EXISTS booking_month text GENERATED ALWAYS AS (
        (ARRAY['enero','febrero','marzo','abril','mayo','junio',
               'julio','agosto','septiembre','octubre','noviembre','diciembre'])
            [EXTRACT(MONTH FROM starts_at AT TIME ZONE 'America/Lima')::int]
    ) STORED,
    ADD COLUMN IF NOT EXISTS "booking_month#" integer GENERATED ALWAYS AS (
        EXTRACT(MONTH FROM starts_at AT TIME ZONE 'America/Lima')::int
    ) STORED,
    ADD COLUMN IF NOT EXISTS booking_account_email text GENERATED ALWAYS AS (user_email) STORED,
    ADD COLUMN IF NOT EXISTS booking_resource text GENERATED ALWAYS AS (resource_name) STORED,
    ADD COLUMN IF NOT EXISTS booking_category text GENERATED ALWAYS AS (category_name) STORED,
    ADD COLUMN IF NOT EXISTS booking_campus text GENERATED ALWAYS AS (campus_name) STORED,
    ADD COLUMN IF NOT EXISTS booking_date date GENERATED ALWAYS AS (
        (starts_at AT TIME ZONE 'America/Lima')::date
    ) STORED,
    ADD COLUMN IF NOT EXISTS booking_start time GENERATED ALWAYS AS (
        (starts_at AT TIME ZONE 'America/Lima')::time
    ) STORED,
    ADD COLUMN IF NOT EXISTS booking_end time GENERATED ALWAYS AS (
        (ends_at AT TIME ZONE 'America/Lima')::time
    ) STORED,
    ADD COLUMN IF NOT EXISTS booking_duration numeric GENERATED ALWAYS AS (
        EXTRACT(EPOCH FROM (ends_at - starts_at)) / 60
    ) STORED,
    ADD COLUMN IF NOT EXISTS booking_hour integer GENERATED ALWAYS AS (
        EXTRACT(HOUR FROM starts_at AT TIME ZONE 'America/Lima')::int
    ) STORED,
    ADD COLUMN IF NOT EXISTS booking_status text GENERATED ALWAYS AS (
        CASE
            WHEN lower(btrim(reservation_status)) ~ '^cancell?(ed|eb)($|[[:space:]]+by[[:space:]]+)' THEN 'Cancelado'
            ELSE CASE lower(btrim(reservation_status))
                WHEN 'confirmed' THEN 'Confirmado'
                WHEN 'tentative' THEN 'Tentativo'
                WHEN 'mediated tentative' THEN 'Tentativo (mediación)'
                WHEN 'denied' THEN 'Denegado'
                WHEN 'mediated denied' THEN 'Denegado (mediación)'
                ELSE reservation_status
            END
        END
    ) STORED,
    ADD COLUMN IF NOT EXISTS booking_confirmation text GENERATED ALWAYS AS (
        CASE lower(btrim(check_in_status))
            WHEN 'in' THEN 'Asistió'
            WHEN 'out' THEN 'Asistió'
            WHEN '-' THEN 'Sin registro de asistencia'
            ELSE check_in_status
        END
    ) STORED,
    ADD COLUMN IF NOT EXISTS booking_form_answer_1 text GENERATED ALWAYS AS (
        NULLIF(btrim(form_answer_1), '')
    ) STORED,
    ADD COLUMN IF NOT EXISTS booking_form_answer_2 text GENERATED ALWAYS AS (
        NULLIF(btrim(form_answer_2), '')
    ) STORED,
    ADD COLUMN IF NOT EXISTS booking_phone text GENERATED ALWAYS AS (
        NULLIF(btrim(raw_payload ->> 'q25458'), '')
    ) STORED;

DROP VIEW IF EXISTS etl.libcal_report;
