-- Tie a review to the job it came from, so a Paid job can advance to Reviewed and a job can only be
-- reviewed once. Nullable because historical/ad-hoc reviews may not reference a job.
IF COL_LENGTH('dbo.Reviews', 'JobRequestId') IS NULL
    ALTER TABLE dbo.Reviews ADD
        JobRequestId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Reviews_JobRequests REFERENCES dbo.JobRequests (Id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reviews_JobRequest' AND object_id = OBJECT_ID('dbo.Reviews'))
    CREATE INDEX IX_Reviews_JobRequest ON dbo.Reviews (JobRequestId);
