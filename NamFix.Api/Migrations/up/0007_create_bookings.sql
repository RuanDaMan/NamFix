-- Bookings (negotiated jobs) + their chat thread, attachments (invoice files) and notifications.
-- A booking's time is proposed back and forth (Status + ProposedByUserId track whose turn it is);
-- once Completed with an InvoiceAmount the client pays that exact amount, linking a Transaction.
IF OBJECT_ID('dbo.Bookings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bookings
    (
        Id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Bookings PRIMARY KEY,
        ProviderId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Bookings_Providers REFERENCES dbo.Providers (Id),
        ProviderUserId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Bookings_ProviderUser REFERENCES dbo.Users (Id),
        ClientUserId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Bookings_ClientUser REFERENCES dbo.Users (Id),
        CategoryId         INT              NULL CONSTRAINT FK_Bookings_Categories REFERENCES dbo.Categories (Id),
        ServiceDescription NVARCHAR(1000)   NOT NULL,
        Status             INT              NOT NULL,   -- BookingStatus enum
        ProposedStartUtc   DATETIME2(3)     NULL,
        ProposedByUserId   UNIQUEIDENTIFIER NULL CONSTRAINT FK_Bookings_ProposedBy REFERENCES dbo.Users (Id),
        ConfirmedStartUtc  DATETIME2(3)     NULL,
        LocationText       NVARCHAR(400)    NULL,
        LocationLat        FLOAT            NULL,
        LocationLng        FLOAT            NULL,
        InvoiceAmount      DECIMAL(18,2)    NULL,
        InvoiceNotes       NVARCHAR(1000)   NULL,
        Currency           NVARCHAR(3)      NOT NULL CONSTRAINT DF_Bookings_Currency DEFAULT ('NAD'),
        TransactionId      UNIQUEIDENTIFIER NULL CONSTRAINT FK_Bookings_Transactions REFERENCES dbo.Transactions (Id),
        CreatedAtUtc       DATETIME2(3)     NOT NULL CONSTRAINT DF_Bookings_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc       DATETIME2(3)     NOT NULL CONSTRAINT DF_Bookings_Updated DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_Bookings_Client ON dbo.Bookings (ClientUserId);
    CREATE INDEX IX_Bookings_ProviderUser ON dbo.Bookings (ProviderUserId);
    CREATE INDEX IX_Bookings_Provider ON dbo.Bookings (ProviderId);
    CREATE INDEX IX_Bookings_Status ON dbo.Bookings (Status);
END;

IF OBJECT_ID('dbo.BookingMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BookingMessages
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BookingMessages PRIMARY KEY,
        BookingId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_BookingMessages_Bookings REFERENCES dbo.Bookings (Id),
        SenderUserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_BookingMessages_Users REFERENCES dbo.Users (Id),
        Body         NVARCHAR(2000)   NOT NULL,
        CreatedAtUtc DATETIME2(3)     NOT NULL CONSTRAINT DF_BookingMessages_Created DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_BookingMessages_Booking ON dbo.BookingMessages (BookingId, CreatedAtUtc);
END;

IF OBJECT_ID('dbo.BookingAttachments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BookingAttachments
    (
        Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BookingAttachments PRIMARY KEY,
        BookingId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_BookingAttachments_Bookings REFERENCES dbo.Bookings (Id),
        UploadedByUserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_BookingAttachments_Users REFERENCES dbo.Users (Id),
        FileName         NVARCHAR(260)    NOT NULL,
        ContentType      NVARCHAR(100)    NOT NULL,
        Content          VARBINARY(MAX)   NOT NULL,
        CreatedAtUtc     DATETIME2(3)     NOT NULL CONSTRAINT DF_BookingAttachments_Created DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_BookingAttachments_Booking ON dbo.BookingAttachments (BookingId);
END;

IF OBJECT_ID('dbo.Notifications', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notifications
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
        UserId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Notifications_Users REFERENCES dbo.Users (Id),
        BookingId    UNIQUEIDENTIFIER NULL CONSTRAINT FK_Notifications_Bookings REFERENCES dbo.Bookings (Id),
        Type         INT              NOT NULL,   -- NotificationType enum
        Message      NVARCHAR(400)    NOT NULL,
        IsRead       BIT              NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT (0),
        CreatedAtUtc DATETIME2(3)     NOT NULL CONSTRAINT DF_Notifications_Created DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_Notifications_User ON dbo.Notifications (UserId, IsRead, CreatedAtUtc);
END;
