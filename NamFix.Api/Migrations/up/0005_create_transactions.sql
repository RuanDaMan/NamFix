-- Platform transactions + the configurable commission rules that drive revenue.
IF OBJECT_ID('dbo.Transactions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Transactions
    (
        Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Transactions PRIMARY KEY,
        ProviderId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Transactions_Providers REFERENCES dbo.Providers (Id),
        ClientUserId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Transactions_Users REFERENCES dbo.Users (Id),
        CategoryId       INT              NULL CONSTRAINT FK_Transactions_Categories REFERENCES dbo.Categories (Id),
        GrossAmount      DECIMAL(18,2)    NOT NULL,
        CommissionRate   DECIMAL(5,4)     NOT NULL,   -- e.g. 0.1000 == 10%
        CommissionAmount DECIMAL(18,2)    NOT NULL,
        NetPayoutAmount  DECIMAL(18,2)    NOT NULL,
        Status           INT              NOT NULL,   -- TransactionStatus enum
        Currency         NVARCHAR(3)      NOT NULL CONSTRAINT DF_Transactions_Currency DEFAULT ('NAD'),
        PaymentReference NVARCHAR(100)    NULL,
        CreatedAtUtc     DATETIME2(3)     NOT NULL CONSTRAINT DF_Transactions_Created DEFAULT (SYSUTCDATETIME()),
        HeldAtUtc        DATETIME2(3)     NULL,
        PaidOutAtUtc     DATETIME2(3)     NULL
    );
    CREATE INDEX IX_Transactions_Provider ON dbo.Transactions (ProviderId);
    CREATE INDEX IX_Transactions_Created ON dbo.Transactions (CreatedAtUtc);
    CREATE INDEX IX_Transactions_Status ON dbo.Transactions (Status);
END;

IF OBJECT_ID('dbo.CommissionRules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CommissionRules
    (
        Id         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CommissionRules PRIMARY KEY,
        Scope      INT              NOT NULL,   -- CommissionScope: 0 Platform, 1 Category, 2 Provider
        CategoryId INT              NULL CONSTRAINT FK_CommissionRules_Categories REFERENCES dbo.Categories (Id),
        ProviderId UNIQUEIDENTIFIER NULL CONSTRAINT FK_CommissionRules_Providers REFERENCES dbo.Providers (Id),
        Rate       DECIMAL(5,4)     NOT NULL,
        IsActive   BIT              NOT NULL CONSTRAINT DF_CommissionRules_IsActive DEFAULT (1)
    );
END;
