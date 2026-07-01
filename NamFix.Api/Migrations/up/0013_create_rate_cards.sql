-- Provider rate cards: an optional published price list per job type/category. Shown on the profile
-- and used to prefill the quote form. StartingPrice on Providers (see 0014) is the denormalized min
-- active price used for the search price-range filter.
IF OBJECT_ID('dbo.ProviderRateCards', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProviderRateCards
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProviderRateCards PRIMARY KEY,
        ProviderId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ProviderRateCards_Providers REFERENCES dbo.Providers (Id),
        CategoryId   INT              NULL CONSTRAINT FK_ProviderRateCards_Categories REFERENCES dbo.Categories (Id),
        Title        NVARCHAR(150)    NOT NULL,
        Description  NVARCHAR(500)    NULL,
        Price        DECIMAL(18,2)    NOT NULL,
        Unit         INT              NOT NULL,   -- RateUnit enum
        IsActive     BIT              NOT NULL CONSTRAINT DF_ProviderRateCards_Active DEFAULT (1),
        SortOrder    INT              NOT NULL CONSTRAINT DF_ProviderRateCards_Sort DEFAULT (0),
        CreatedAtUtc DATETIME2(3)     NOT NULL CONSTRAINT DF_ProviderRateCards_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(3)     NOT NULL CONSTRAINT DF_ProviderRateCards_Updated DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_ProviderRateCards_Provider ON dbo.ProviderRateCards (ProviderId, IsActive);
END;
