-- One PostgreSQL server, one database per service (each service owns its schema and migrates it at startup).
CREATE DATABASE "IdentityDb";
CREATE DATABASE linkedin_users;
CREATE DATABASE linkedin_posts;
CREATE DATABASE requirements;
