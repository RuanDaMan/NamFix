-- Reviews. IsVerified is set when the client had an on-platform transaction with the provider.
IF OBJECT_ID('dbo.Reviews', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Reviews
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Reviews PRIMARY KEY,
        ProviderId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Reviews_Providers REFERENCES dbo.Providers (Id),
        ClientUserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Reviews_Users REFERENCES dbo.Users (Id),
        Rating       INT              NOT NULL CONSTRAINT CK_Reviews_Rating CHECK (Rating BETWEEN 1 AND 5),
        Comment      NVARCHAR(2000)   NULL,
        IsVerified   BIT              NOT NULL CONSTRAINT DF_Reviews_Verified DEFAULT (0),
        CreatedAtUtc DATETIME2(3)     NOT NULL CONSTRAINT DF_Reviews_Created DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_Reviews_Provider ON dbo.Reviews (ProviderId);
END;
