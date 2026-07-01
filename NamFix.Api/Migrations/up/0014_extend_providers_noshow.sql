-- Extended search signals on Providers + no-show / late-cancellation counters on both sides.
--   YearsExperience / AvgResponseMinutes / StartingPrice -> extended search filters & response-time badge.
--   NoShowCount / LateCancellationCount -> reliability flags maintained by the JobRequest lifecycle.
-- AvgResponseMinutes and StartingPrice are denormalized: maintained by the service layer (on quote
-- response and on rate-card save respectively) to keep search queries lean.
IF COL_LENGTH('dbo.Providers', 'YearsExperience') IS NULL
BEGIN
    ALTER TABLE dbo.Providers ADD
        YearsExperience       INT           NULL,
        AvgResponseMinutes    INT           NULL,
        StartingPrice         DECIMAL(18,2) NULL,
        NoShowCount           INT           NOT NULL CONSTRAINT DF_Providers_NoShow DEFAULT (0),
        LateCancellationCount INT           NOT NULL CONSTRAINT DF_Providers_LateCancel DEFAULT (0);
END;

IF COL_LENGTH('dbo.Users', 'NoShowCount') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD
        NoShowCount           INT NOT NULL CONSTRAINT DF_Users_NoShow DEFAULT (0),
        LateCancellationCount INT NOT NULL CONSTRAINT DF_Users_LateCancel DEFAULT (0);
END;
