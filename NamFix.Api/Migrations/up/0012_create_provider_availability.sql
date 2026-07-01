-- Provider availability calendar. Recurring weekly hours plus one-off blocked date ranges. This
-- layers real scheduling on top of the existing Providers.Availability enum (the cheap "available
-- now" toggle that still powers the search filter). Booked slots are derived from JobRequests with a
-- ConfirmedStartUtc/ConfirmedEndUtc, so they are not duplicated here.
IF OBJECT_ID('dbo.ProviderAvailabilityRules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProviderAvailabilityRules
    (
        Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProviderAvailabilityRules PRIMARY KEY,
        ProviderId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ProviderAvailabilityRules_Providers REFERENCES dbo.Providers (Id),
        [DayOfWeek] TINYINT          NOT NULL,   -- 0=Sunday .. 6=Saturday (matches System.DayOfWeek)
        StartTime   TIME             NOT NULL,
        EndTime     TIME             NOT NULL
    );
    CREATE INDEX IX_ProviderAvailabilityRules_Provider ON dbo.ProviderAvailabilityRules (ProviderId);
END;

IF OBJECT_ID('dbo.ProviderTimeOff', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProviderTimeOff
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProviderTimeOff PRIMARY KEY,
        ProviderId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ProviderTimeOff_Providers REFERENCES dbo.Providers (Id),
        StartUtc     DATETIME2(3)     NOT NULL,
        EndUtc       DATETIME2(3)     NOT NULL,
        Reason       NVARCHAR(200)    NULL,
        CreatedAtUtc DATETIME2(3)     NOT NULL CONSTRAINT DF_ProviderTimeOff_Created DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_ProviderTimeOff_Provider ON dbo.ProviderTimeOff (ProviderId, StartUtc);
END;
