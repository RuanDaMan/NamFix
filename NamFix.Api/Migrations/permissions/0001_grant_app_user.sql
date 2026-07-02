-- Run-every-time grants. Adjust the principal name to match your deployment's app login.
-- Guarded so it is a no-op when the database user does not exist (e.g. local Trusted_Connection dev).
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'namfix_app')
BEGIN
    GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO namfix_app;
    GRANT EXECUTE ON SCHEMA::dbo TO namfix_app;

    -- Hangfire keeps its state in its own [HangFire] schema (created on first run). The app login
    -- needs DML + execute there for the background job/inbox-poll workers.
    IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'HangFire')
    BEGIN
        GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[HangFire] TO namfix_app;
        GRANT EXECUTE ON SCHEMA::[HangFire] TO namfix_app;
    END;
END;
