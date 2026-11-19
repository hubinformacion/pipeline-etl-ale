ALTER TABLE etl.libcal_bookings
    DROP COLUMN IF EXISTS booking_month,
    DROP COLUMN IF EXISTS "booking_month#";
