CREATE SCHEMA IF NOT EXISTS etl;

CREATE TABLE IF NOT EXISTS etl.elogim_sessions (
    extraction_date date NOT NULL,
    row_number bigint NOT NULL CHECK (row_number > 0),
    loaded_at timestamp with time zone NOT NULL,
    fecha_sesion timestamp without time zone NOT NULL,
    id_est text NOT NULL,
    identificacion text NOT NULL,
    bd text NOT NULL,
    tipo_consulta text NOT NULL,
    PRIMARY KEY (extraction_date, row_number),
    CHECK (fecha_sesion::date = extraction_date)
);
