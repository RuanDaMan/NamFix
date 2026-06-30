-- Full-text search over provider name, description, and denormalized keywords (tags + category).
-- NOTE: CREATE FULLTEXT CATALOG/INDEX cannot run inside an explicit transaction, so run Grate
-- WITHOUT the --transaction flag (this is Grate's default). See db/README.md.

IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'NamFixCatalog')
    CREATE FULLTEXT CATALOG NamFixCatalog AS DEFAULT;

IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('dbo.Providers'))
    CREATE FULLTEXT INDEX ON dbo.Providers
    (
        BusinessName   LANGUAGE 1033,
        Description    LANGUAGE 1033,
        SearchKeywords LANGUAGE 1033
    )
    KEY INDEX PK_Providers
    ON NamFixCatalog
    WITH CHANGE_TRACKING AUTO;
