-- Admin-editable platform tunables as a simple key/value store (mirrors how commission rates are
-- admin-configurable). Seeds the free-cancellation window used by the JobRequest cancel/no-show logic.
IF OBJECT_ID('dbo.PlatformSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlatformSettings
    (
        [Key]        NVARCHAR(100) NOT NULL CONSTRAINT PK_PlatformSettings PRIMARY KEY,
        [Value]      NVARCHAR(400) NOT NULL,
        UpdatedAtUtc DATETIME2(3)  NOT NULL CONSTRAINT DF_PlatformSettings_Updated DEFAULT (SYSUTCDATETIME())
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.PlatformSettings WHERE [Key] = 'FreeCancellationWindowHours')
    INSERT INTO dbo.PlatformSettings ([Key], [Value]) VALUES ('FreeCancellationWindowHours', '24');
