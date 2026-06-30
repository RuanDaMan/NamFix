-- Idempotent seed of starter service tags, pre-approved (Status = 1).
MERGE dbo.Tags AS target
USING (VALUES
    (N'geyser repair'), (N'solar'), (N'after hours'), (N'borehole'),
    (N'wiring'), (N'prepaid meters'), (N'engine repair'), (N'brakes'),
    (N'furniture'), (N'ceilings'), (N'waterproofing'), (N'paving'),
    (N'gates'), (N'burglar bars'), (N'aircon installation'), (N'tiling'),
    (N'painting'), (N'roof repair'), (N'pool maintenance'), (N'emergency callout')
) AS src (Name)
ON target.Name = src.Name
WHEN NOT MATCHED THEN
    INSERT (Name, Status, CreatedByUserId)
    VALUES (src.Name, 1, NULL);
