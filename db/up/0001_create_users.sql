-- Users and refresh tokens. Identity is custom/token-based with a Dapper-backed store.
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        Email        NVARCHAR(256)    NOT NULL,
        PhoneNumber  NVARCHAR(32)     NULL,
        FullName     NVARCHAR(200)    NOT NULL,
        Role         INT              NOT NULL,            -- UserRole enum
        PasswordHash NVARCHAR(400)    NOT NULL,
        IsActive     BIT              NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
        CreatedAtUtc DATETIME2(3)     NOT NULL CONSTRAINT DF_Users_Created DEFAULT (SYSUTCDATETIME())
    );
    CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users (Email);
END;

IF OBJECT_ID('dbo.RefreshTokens', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
        UserId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_RefreshTokens_Users REFERENCES dbo.Users (Id),
        Token        NVARCHAR(200)    NOT NULL,
        ExpiresAtUtc DATETIME2(3)     NOT NULL,
        CreatedAtUtc DATETIME2(3)     NOT NULL,
        RevokedAtUtc DATETIME2(3)     NULL
    );
    CREATE UNIQUE INDEX UX_RefreshTokens_Token ON dbo.RefreshTokens (Token);
END;
