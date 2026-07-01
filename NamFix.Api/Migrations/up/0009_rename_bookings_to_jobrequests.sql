-- Rename the Bookings tables/columns/constraints to JobRequests.
-- A "Booking" was always the single negotiated-job entity; it now also carries the pre-provider
-- matching/quoting phase, so the naming is generalised to JobRequest. This is a pure rename — no
-- data is lost and stored Status/Type int values are preserved. Every step is guarded so the script
-- is safe to re-run / recover from a partial apply.

-- ---- Tables ----
IF OBJECT_ID('dbo.Bookings', 'U') IS NOT NULL AND OBJECT_ID('dbo.JobRequests', 'U') IS NULL
    EXEC sp_rename 'dbo.Bookings', 'JobRequests';

IF OBJECT_ID('dbo.BookingMessages', 'U') IS NOT NULL AND OBJECT_ID('dbo.JobRequestMessages', 'U') IS NULL
    EXEC sp_rename 'dbo.BookingMessages', 'JobRequestMessages';

IF OBJECT_ID('dbo.BookingAttachments', 'U') IS NOT NULL AND OBJECT_ID('dbo.JobRequestAttachments', 'U') IS NULL
    EXEC sp_rename 'dbo.BookingAttachments', 'JobRequestAttachments';

-- ---- Columns (BookingId -> JobRequestId on children + Notifications) ----
IF OBJECT_ID('dbo.JobRequestMessages', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.JobRequestMessages', 'BookingId') IS NOT NULL
    EXEC sp_rename 'dbo.JobRequestMessages.BookingId', 'JobRequestId', 'COLUMN';

IF OBJECT_ID('dbo.JobRequestAttachments', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.JobRequestAttachments', 'BookingId') IS NOT NULL
    EXEC sp_rename 'dbo.JobRequestAttachments.BookingId', 'JobRequestId', 'COLUMN';

IF COL_LENGTH('dbo.Notifications', 'BookingId') IS NOT NULL
    EXEC sp_rename 'dbo.Notifications.BookingId', 'JobRequestId', 'COLUMN';

-- ---- Primary keys ----
IF OBJECT_ID('PK_Bookings') IS NOT NULL            EXEC sp_rename 'PK_Bookings', 'PK_JobRequests';
IF OBJECT_ID('PK_BookingMessages') IS NOT NULL     EXEC sp_rename 'PK_BookingMessages', 'PK_JobRequestMessages';
IF OBJECT_ID('PK_BookingAttachments') IS NOT NULL  EXEC sp_rename 'PK_BookingAttachments', 'PK_JobRequestAttachments';

-- ---- Foreign keys ----
IF OBJECT_ID('FK_Bookings_Providers') IS NOT NULL      EXEC sp_rename 'FK_Bookings_Providers', 'FK_JobRequests_Providers';
IF OBJECT_ID('FK_Bookings_ProviderUser') IS NOT NULL   EXEC sp_rename 'FK_Bookings_ProviderUser', 'FK_JobRequests_ProviderUser';
IF OBJECT_ID('FK_Bookings_ClientUser') IS NOT NULL     EXEC sp_rename 'FK_Bookings_ClientUser', 'FK_JobRequests_ClientUser';
IF OBJECT_ID('FK_Bookings_Categories') IS NOT NULL     EXEC sp_rename 'FK_Bookings_Categories', 'FK_JobRequests_Categories';
IF OBJECT_ID('FK_Bookings_ProposedBy') IS NOT NULL     EXEC sp_rename 'FK_Bookings_ProposedBy', 'FK_JobRequests_ProposedBy';
IF OBJECT_ID('FK_Bookings_Transactions') IS NOT NULL   EXEC sp_rename 'FK_Bookings_Transactions', 'FK_JobRequests_Transactions';
IF OBJECT_ID('FK_BookingMessages_Bookings') IS NOT NULL    EXEC sp_rename 'FK_BookingMessages_Bookings', 'FK_JobRequestMessages_JobRequests';
IF OBJECT_ID('FK_BookingMessages_Users') IS NOT NULL       EXEC sp_rename 'FK_BookingMessages_Users', 'FK_JobRequestMessages_Users';
IF OBJECT_ID('FK_BookingAttachments_Bookings') IS NOT NULL EXEC sp_rename 'FK_BookingAttachments_Bookings', 'FK_JobRequestAttachments_JobRequests';
IF OBJECT_ID('FK_BookingAttachments_Users') IS NOT NULL    EXEC sp_rename 'FK_BookingAttachments_Users', 'FK_JobRequestAttachments_Users';
IF OBJECT_ID('FK_Notifications_Bookings') IS NOT NULL       EXEC sp_rename 'FK_Notifications_Bookings', 'FK_Notifications_JobRequests';

-- ---- Default constraints ----
IF OBJECT_ID('DF_Bookings_Currency') IS NOT NULL   EXEC sp_rename 'DF_Bookings_Currency', 'DF_JobRequests_Currency';
IF OBJECT_ID('DF_Bookings_Created') IS NOT NULL    EXEC sp_rename 'DF_Bookings_Created', 'DF_JobRequests_Created';
IF OBJECT_ID('DF_Bookings_Updated') IS NOT NULL    EXEC sp_rename 'DF_Bookings_Updated', 'DF_JobRequests_Updated';
IF OBJECT_ID('DF_BookingMessages_Created') IS NOT NULL    EXEC sp_rename 'DF_BookingMessages_Created', 'DF_JobRequestMessages_Created';
IF OBJECT_ID('DF_BookingAttachments_Created') IS NOT NULL EXEC sp_rename 'DF_BookingAttachments_Created', 'DF_JobRequestAttachments_Created';

-- ---- Indexes (rename via the new table name) ----
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bookings_Client' AND object_id = OBJECT_ID('dbo.JobRequests'))
    EXEC sp_rename 'dbo.JobRequests.IX_Bookings_Client', 'IX_JobRequests_Client', 'INDEX';
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bookings_ProviderUser' AND object_id = OBJECT_ID('dbo.JobRequests'))
    EXEC sp_rename 'dbo.JobRequests.IX_Bookings_ProviderUser', 'IX_JobRequests_ProviderUser', 'INDEX';
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bookings_Provider' AND object_id = OBJECT_ID('dbo.JobRequests'))
    EXEC sp_rename 'dbo.JobRequests.IX_Bookings_Provider', 'IX_JobRequests_Provider', 'INDEX';
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bookings_Status' AND object_id = OBJECT_ID('dbo.JobRequests'))
    EXEC sp_rename 'dbo.JobRequests.IX_Bookings_Status', 'IX_JobRequests_Status', 'INDEX';
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BookingMessages_Booking' AND object_id = OBJECT_ID('dbo.JobRequestMessages'))
    EXEC sp_rename 'dbo.JobRequestMessages.IX_BookingMessages_Booking', 'IX_JobRequestMessages_JobRequest', 'INDEX';
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BookingAttachments_Booking' AND object_id = OBJECT_ID('dbo.JobRequestAttachments'))
    EXEC sp_rename 'dbo.JobRequestAttachments.IX_BookingAttachments_Booking', 'IX_JobRequestAttachments_JobRequest', 'INDEX';
