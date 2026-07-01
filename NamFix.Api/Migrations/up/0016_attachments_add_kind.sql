-- Distinguish attachment kinds so the quote-request job photos can reuse the existing attachments
-- table/endpoints. Kind 0 = Invoice (single, replaced), Kind 1 = JobPhoto (multiple allowed).
IF COL_LENGTH('dbo.JobRequestAttachments', 'Kind') IS NULL
    ALTER TABLE dbo.JobRequestAttachments ADD
        Kind INT NOT NULL CONSTRAINT DF_JobRequestAttachments_Kind DEFAULT (0);   -- AttachmentKind enum
