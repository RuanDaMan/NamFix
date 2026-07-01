-- Provider responses to a job request. One row per invited provider: a Direct quote request creates
-- one row; a Broadcast ("get matched" / urgent) creates one per matched/targeted provider, all seeded
-- as Invited at post time. The row moves through Interested/Quoted/Declined and finally Accepted or
-- Rejected once the client picks a quote. This is the multi-provider fan-out for the single JobRequest.
IF OBJECT_ID('dbo.JobRequestResponses', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobRequestResponses
    (
        Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_JobRequestResponses PRIMARY KEY,
        JobRequestId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_JobRequestResponses_JobRequests REFERENCES dbo.JobRequests (Id),
        ProviderId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_JobRequestResponses_Providers REFERENCES dbo.Providers (Id),
        ProviderUserId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_JobRequestResponses_ProviderUser REFERENCES dbo.Users (Id),
        Status           INT              NOT NULL,   -- JobResponseStatus enum
        QuoteAmount      DECIMAL(18,2)    NULL,
        QuoteNote        NVARCHAR(1000)   NULL,
        QuoteExpiresUtc  DATETIME2(3)     NULL,
        ProposedStartUtc DATETIME2(3)     NULL,
        CreatedAtUtc     DATETIME2(3)     NOT NULL CONSTRAINT DF_JobRequestResponses_Created DEFAULT (SYSUTCDATETIME()),
        RespondedAtUtc   DATETIME2(3)     NULL,
        CONSTRAINT UX_JobRequestResponses_Job_Provider UNIQUE (JobRequestId, ProviderId)
    );
    CREATE INDEX IX_JobRequestResponses_Job ON dbo.JobRequestResponses (JobRequestId);
    CREATE INDEX IX_JobRequestResponses_Provider ON dbo.JobRequestResponses (ProviderUserId, Status);
END;
