-- Email subsystem tables: single-use password-reset tokens, per-user email-category subscriptions,
-- and the stored copy of mailbox messages fetched over POP3 for the admin inbox.
-- Enums (Category) are stored as INT per the DB convention. Hangfire manages its own [HangFire]
-- schema automatically on first run and is not created here.

IF OBJECT_ID('dbo.PasswordResetTokens', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PasswordResetTokens PRIMARY KEY,
        UserId       UNIQUEIDENTIFIER NOT NULL,
        Token        NVARCHAR(200)    NOT NULL,
        ExpiresAtUtc DATETIME2(3)     NOT NULL,
        CreatedAtUtc DATETIME2(3)     NOT NULL,
        UsedAtUtc    DATETIME2(3)     NULL,
        CONSTRAINT FK_PasswordResetTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
    );
    CREATE UNIQUE INDEX UX_PasswordResetTokens_Token ON dbo.PasswordResetTokens(Token);
    CREATE INDEX IX_PasswordResetTokens_UserId ON dbo.PasswordResetTokens(UserId);
END;

IF OBJECT_ID('dbo.UserEmailPreferences', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserEmailPreferences
    (
        UserId       UNIQUEIDENTIFIER NOT NULL,
        Category     INT              NOT NULL,
        IsSubscribed BIT              NOT NULL CONSTRAINT DF_UserEmailPreferences_IsSubscribed DEFAULT (1),
        CONSTRAINT PK_UserEmailPreferences PRIMARY KEY (UserId, Category),
        CONSTRAINT FK_UserEmailPreferences_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
    );
END;

IF OBJECT_ID('dbo.InboxMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InboxMessages
    (
        Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InboxMessages PRIMARY KEY,
        MessageId     NVARCHAR(450)    NOT NULL,
        FromName      NVARCHAR(256)    NOT NULL CONSTRAINT DF_InboxMessages_FromName DEFAULT (''),
        FromAddress   NVARCHAR(320)    NOT NULL CONSTRAINT DF_InboxMessages_FromAddress DEFAULT (''),
        Subject       NVARCHAR(1000)   NOT NULL CONSTRAINT DF_InboxMessages_Subject DEFAULT (''),
        ReceivedAtUtc DATETIME2(3)     NOT NULL,
        TextBody      NVARCHAR(MAX)    NULL,
        HtmlBody      NVARCHAR(MAX)    NULL,
        FetchedAtUtc  DATETIME2(3)     NOT NULL
    );
    CREATE UNIQUE INDEX UX_InboxMessages_MessageId ON dbo.InboxMessages(MessageId);
    CREATE INDEX IX_InboxMessages_ReceivedAtUtc ON dbo.InboxMessages(ReceivedAtUtc DESC);
END;
