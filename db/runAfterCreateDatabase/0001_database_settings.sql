-- Runs once, right after Grate creates the database.
-- Full-text search is enabled per-database by default on modern SQL Server; this is a safe no-op
-- placeholder for any database-level settings (collation tweaks, options) you may need later.
PRINT 'NamFix database created.';
