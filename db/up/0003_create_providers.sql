-- Providers and their many-to-many links to towns and tags.
-- SearchKeywords is a denormalized blob (name + category + tags) that feeds the full-text index.
IF OBJECT_ID('dbo.Providers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Providers
    (
        Id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Providers PRIMARY KEY,
        UserId             UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Providers_Users REFERENCES dbo.Users (Id),
        BusinessName       NVARCHAR(200)    NOT NULL,
        Description        NVARCHAR(MAX)    NULL,
        PrimaryCategoryId  INT              NOT NULL CONSTRAINT FK_Providers_Categories REFERENCES dbo.Categories (Id),
        Status             INT              NOT NULL CONSTRAINT DF_Providers_Status DEFAULT (0), -- ProviderStatus
        Availability       INT              NOT NULL CONSTRAINT DF_Providers_Avail DEFAULT (1),  -- AvailabilityStatus
        IsVerified         BIT              NOT NULL CONSTRAINT DF_Providers_Verified DEFAULT (0),
        IsEmergencyCallout BIT              NOT NULL CONSTRAINT DF_Providers_Emergency DEFAULT (0),
        Latitude           FLOAT            NULL,
        Longitude          FLOAT            NULL,
        PrimaryTownId      INT              NULL CONSTRAINT FK_Providers_Towns REFERENCES dbo.Towns (Id),
        RatingAverage      DECIMAL(3,2)     NULL,
        RatingCount        INT              NOT NULL CONSTRAINT DF_Providers_RatingCount DEFAULT (0),
        SearchKeywords     NVARCHAR(MAX)    NULL,
        CreatedAtUtc       DATETIME2(3)     NOT NULL CONSTRAINT DF_Providers_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc       DATETIME2(3)     NOT NULL CONSTRAINT DF_Providers_Updated DEFAULT (SYSUTCDATETIME())
    );
    CREATE UNIQUE INDEX UX_Providers_UserId ON dbo.Providers (UserId);
    CREATE INDEX IX_Providers_Category ON dbo.Providers (PrimaryCategoryId);
    CREATE INDEX IX_Providers_Status ON dbo.Providers (Status);
END;

IF OBJECT_ID('dbo.ProviderTowns', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProviderTowns
    (
        ProviderId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ProviderTowns_Providers REFERENCES dbo.Providers (Id),
        TownId     INT              NOT NULL CONSTRAINT FK_ProviderTowns_Towns REFERENCES dbo.Towns (Id),
        CONSTRAINT PK_ProviderTowns PRIMARY KEY (ProviderId, TownId)
    );
END;

IF OBJECT_ID('dbo.ProviderTags', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProviderTags
    (
        ProviderId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ProviderTags_Providers REFERENCES dbo.Providers (Id),
        TagId      INT              NOT NULL CONSTRAINT FK_ProviderTags_Tags REFERENCES dbo.Tags (Id),
        CONSTRAINT PK_ProviderTags PRIMARY KEY (ProviderId, TagId)
    );
END;
