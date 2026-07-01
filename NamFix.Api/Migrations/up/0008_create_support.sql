-- Support/helpdesk: tickets, their threaded messages and file attachments, plus presence (Users
-- LastSeenUtc) and a Notifications.TicketId link so the existing notification bell can point at a
-- ticket the same way it points at a booking. Modeled on the Bookings tables in 0007.

IF OBJECT_ID('dbo.SupportTickets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupportTickets
    (
        Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SupportTickets PRIMARY KEY,
        RequesterUserId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SupportTickets_Users REFERENCES dbo.Users (Id),
        Subject          NVARCHAR(200)    NOT NULL,
        Category         INT              NOT NULL,   -- SupportCategory enum
        Priority         INT              NOT NULL,   -- SupportPriority enum
        Status           INT              NOT NULL,   -- TicketStatus enum
        CreatedAtUtc     DATETIME2(3)     NOT NULL CONSTRAINT DF_SupportTickets_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc     DATETIME2(3)     NOT NULL CONSTRAINT DF_SupportTickets_Updated DEFAULT (SYSUTCDATETIME()),
        LastMessageAtUtc DATETIME2(3)     NOT NULL CONSTRAINT DF_SupportTickets_LastMsg DEFAULT (SYSUTCDATETIME()),
        ClosedAtUtc      DATETIME2(3)     NULL
    );
    CREATE INDEX IX_SupportTickets_Requester ON dbo.SupportTickets (RequesterUserId);
    CREATE INDEX IX_SupportTickets_Status ON dbo.SupportTickets (Status, LastMessageAtUtc);
END;

IF OBJECT_ID('dbo.SupportMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupportMessages
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SupportMessages PRIMARY KEY,
        TicketId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SupportMessages_Tickets REFERENCES dbo.SupportTickets (Id),
        SenderUserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SupportMessages_Users REFERENCES dbo.Users (Id),
        Body         NVARCHAR(4000)   NOT NULL,
        IsSystem     BIT              NOT NULL CONSTRAINT DF_SupportMessages_IsSystem DEFAULT (0),
        CreatedAtUtc DATETIME2(3)     NOT NULL CONSTRAINT DF_SupportMessages_Created DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_SupportMessages_Ticket ON dbo.SupportMessages (TicketId, CreatedAtUtc);
END;

IF OBJECT_ID('dbo.SupportAttachments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupportAttachments
    (
        Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SupportAttachments PRIMARY KEY,
        TicketId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SupportAttachments_Tickets REFERENCES dbo.SupportTickets (Id),
        MessageId        UNIQUEIDENTIFIER NULL CONSTRAINT FK_SupportAttachments_Messages REFERENCES dbo.SupportMessages (Id),
        UploadedByUserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SupportAttachments_Users REFERENCES dbo.Users (Id),
        FileName         NVARCHAR(260)    NOT NULL,
        ContentType      NVARCHAR(100)    NOT NULL,
        Content          VARBINARY(MAX)   NOT NULL,
        SizeBytes        BIGINT           NOT NULL,
        CreatedAtUtc     DATETIME2(3)     NOT NULL CONSTRAINT DF_SupportAttachments_Created DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_SupportAttachments_Ticket ON dbo.SupportAttachments (TicketId);
    CREATE INDEX IX_SupportAttachments_Message ON dbo.SupportAttachments (MessageId);
END;

-- Presence: last time a user held an authenticated realtime connection.
IF COL_LENGTH('dbo.Users', 'LastSeenUtc') IS NULL
    ALTER TABLE dbo.Users ADD LastSeenUtc DATETIME2(3) NULL;

-- Let a notification point at a support ticket (parallel to BookingId).
IF COL_LENGTH('dbo.Notifications', 'TicketId') IS NULL
    ALTER TABLE dbo.Notifications ADD TicketId UNIQUEIDENTIFIER NULL
        CONSTRAINT FK_Notifications_Tickets REFERENCES dbo.SupportTickets (Id);
