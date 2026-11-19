CREATE TABLE etl.load_status (
    dataset text NOT NULL,
    extraction_date date NOT NULL,
    row_count bigint NOT NULL CHECK (row_count >= 0),
    loaded_at timestamptz NOT NULL,
    PRIMARY KEY (dataset, extraction_date)
);
