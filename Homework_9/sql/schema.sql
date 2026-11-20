CREATE TABLE IF NOT EXISTS users (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name varchar(80) NOT NULL,
    groupname varchar(80) NOT NULL,
    age integer NOT NULL,
    gender boolean NOT NULL
);
