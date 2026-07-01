-- Extend JobRequests to cover the pre-provider matching/quoting phase and the booking-stage fields.
-- ProviderId/ProviderUserId become NULL: a broadcast ("get matched" / urgent) job has no chosen
-- provider until the client accepts a quote. Direct booking requests still set them immediately.

IF OBJECT_ID('dbo.JobRequests', 'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.JobRequests') AND name = 'ProviderId' AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.JobRequests ALTER COLUMN ProviderId     UNIQUEIDENTIFIER NULL;
    ALTER TABLE dbo.JobRequests ALTER COLUMN ProviderUserId UNIQUEIDENTIFIER NULL;
END;

IF COL_LENGTH('dbo.JobRequests', 'TownId') IS NULL
BEGIN
    ALTER TABLE dbo.JobRequests ADD
        TownId              INT              NULL CONSTRAINT FK_JobRequests_Towns REFERENCES dbo.Towns (Id),
        TargetMode          INT              NOT NULL CONSTRAINT DF_JobRequests_TargetMode DEFAULT (0),  -- JobTargetMode
        Urgency             INT              NOT NULL CONSTRAINT DF_JobRequests_Urgency DEFAULT (0),     -- JobUrgency
        QuoteExpiresUtc     DATETIME2(3)     NULL,
        ConfirmedEndUtc     DATETIME2(3)     NULL,
        DurationMinutes     INT              NULL,
        CancelledByUserId   UNIQUEIDENTIFIER NULL CONSTRAINT FK_JobRequests_CancelledBy REFERENCES dbo.Users (Id),
        CancelledAtUtc      DATETIME2(3)     NULL,
        WasLateCancellation BIT              NOT NULL CONSTRAINT DF_JobRequests_LateCancel DEFAULT (0),
        NoShowByUserId      UNIQUEIDENTIFIER NULL CONSTRAINT FK_JobRequests_NoShowBy REFERENCES dbo.Users (Id);
END;

-- Matching query index: open broadcast jobs by town + category.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobRequests_Match' AND object_id = OBJECT_ID('dbo.JobRequests'))
    CREATE INDEX IX_JobRequests_Match ON dbo.JobRequests (Status, TargetMode, TownId, CategoryId);
