-- Run-every-time grants. Adjust the principal name to match your deployment's app login.
-- Guarded so it is a no-op when the database user does not exist (e.g. local Trusted_Connection dev).
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'namfix_app')
BEGIN
    GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO namfix_app;
    GRANT EXECUTE ON SCHEMA::dbo TO namfix_app;
END;
