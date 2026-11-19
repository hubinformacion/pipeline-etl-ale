DROP VIEW IF EXISTS etl.libcal_report;

-- Upgrade both earlier schemas. The former aliases are generated copies.
DO $$
DECLARE
    previous_name text;
    report_name text;
BEGIN
    -- Once the report fields are materialized, there is no legacy JSON to migrate.
    IF NOT EXISTS (
        SELECT FROM information_schema.columns
        WHERE table_schema = 'etl' AND table_name = 'libcal_bookings'
          AND column_name = 'raw_payload'
    ) THEN
        RETURN;
    END IF;

    FOR previous_name, report_name IN
        SELECT * FROM (VALUES ('category_name', 'booking_category'),
                              ('campus_name', 'booking_campus'),
                              ('resource_name', 'booking_resource')) AS names(old_name, new_name)
    LOOP
        IF EXISTS (
            SELECT FROM information_schema.columns
            WHERE table_schema = 'etl' AND table_name = 'libcal_bookings'
              AND column_name = previous_name
        ) THEN
            EXECUTE format('ALTER TABLE etl.libcal_bookings DROP COLUMN IF EXISTS %I', report_name);
            EXECUTE format('ALTER TABLE etl.libcal_bookings RENAME COLUMN %I TO %I', previous_name, report_name);
        END IF;
    END LOOP;

    IF EXISTS (
        SELECT FROM information_schema.columns
        WHERE table_schema = 'etl' AND table_name = 'libcal_bookings'
          AND column_name = 'reservation_status'
    ) THEN
        ALTER TABLE etl.libcal_bookings
            DROP COLUMN IF EXISTS booking_status,
            DROP COLUMN IF EXISTS booking_confirmation,
            DROP COLUMN IF EXISTS booking_form_answer_1,
            DROP COLUMN IF EXISTS booking_form_answer_2,
            DROP COLUMN reservation_status,
            DROP COLUMN check_in_status,
            DROP COLUMN form_answer_1,
            DROP COLUMN form_answer_2;
    END IF;

    ALTER TABLE etl.libcal_bookings
        DROP COLUMN IF EXISTS booking_account_email,
        DROP COLUMN IF EXISTS account;

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
                WHEN lower(btrim((raw_payload ->> 'status'))) ~ '^cancell?(ed|eb)($|[[:space:]]+by[[:space:]]+)' THEN 'Cancelado'
                ELSE CASE lower(btrim((raw_payload ->> 'status')))
                    WHEN 'confirmed' THEN 'Confirmado'
                    WHEN 'tentative' THEN 'Tentativo'
                    WHEN 'mediated tentative' THEN 'Tentativo (mediación)'
                    WHEN 'denied' THEN 'Denegado'
                    WHEN 'mediated denied' THEN 'Denegado (mediación)'
                    ELSE (raw_payload ->> 'status')
                END
            END
        ) STORED,
        ADD COLUMN IF NOT EXISTS booking_confirmation text GENERATED ALWAYS AS (
            CASE lower(btrim((raw_payload ->> 'check_in_status')))
                WHEN 'in' THEN 'Asistió'
                WHEN 'out' THEN 'Asistió'
                WHEN '-' THEN 'Sin registro de asistencia'
                ELSE (raw_payload ->> 'check_in_status')
            END
        ) STORED,
        ADD COLUMN IF NOT EXISTS booking_form_answer_1 text GENERATED ALWAYS AS (
            NULLIF(btrim((raw_payload ->> 'q25459')), '')
        ) STORED,
        ADD COLUMN IF NOT EXISTS booking_form_answer_2 text GENERATED ALWAYS AS (
            NULLIF(btrim((raw_payload ->> 'q25460')), '')
        ) STORED,
        ADD COLUMN IF NOT EXISTS booking_phone text GENERATED ALWAYS AS (
            NULLIF(btrim(raw_payload ->> 'q25458'), '')
        ) STORED;
END $$;
