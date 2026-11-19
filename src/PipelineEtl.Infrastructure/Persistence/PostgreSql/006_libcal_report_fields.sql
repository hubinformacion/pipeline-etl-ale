DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM information_schema.columns
        WHERE table_schema = 'etl' AND table_name = 'libcal_bookings'
          AND column_name = 'raw_payload'
    ) THEN
        RETURN;
    END IF;

    -- Keep the values already calculated from the original answers before dropping JSON.
    ALTER TABLE etl.libcal_bookings
        ALTER COLUMN booking_status DROP EXPRESSION,
        ALTER COLUMN booking_confirmation DROP EXPRESSION,
        ALTER COLUMN booking_phone DROP EXPRESSION,
        ALTER COLUMN booking_form_answer_1 DROP EXPRESSION,
        ALTER COLUMN booking_form_answer_2 DROP EXPRESSION;

    ALTER TABLE etl.libcal_bookings RENAME COLUMN booking_form_answer_1 TO booking_participant_2_email;
    ALTER TABLE etl.libcal_bookings RENAME COLUMN booking_form_answer_2 TO booking_participant_3_email;

    UPDATE etl.libcal_bookings
    SET booking_participant_2_email = NULL, booking_participant_3_email = NULL
    WHERE booking_category <> 'Espacios grupales'
      AND (booking_participant_2_email IS NOT NULL OR booking_participant_3_email IS NOT NULL);

    ALTER TABLE etl.libcal_bookings
        ADD COLUMN booking_account_email text GENERATED ALWAYS AS (user_email) STORED,
        DROP COLUMN raw_payload;
END $$;
