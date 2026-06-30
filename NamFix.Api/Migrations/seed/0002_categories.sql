-- Idempotent seed of service categories.
MERGE dbo.Categories AS target
USING (VALUES
    (N'Plumber',        N'plumber',        N'pipe'),
    (N'Electrician',    N'electrician',    N'bolt'),
    (N'Mechanic',       N'mechanic',       N'wrench'),
    (N'Carpenter',      N'carpenter',      N'hammer'),
    (N'Builder',        N'builder',        N'bricks'),
    (N'Painter',        N'painter',        N'roller'),
    (N'Welder',         N'welder',         N'spark'),
    (N'Tiler',          N'tiler',          N'grid'),
    (N'Roofer',         N'roofer',         N'home'),
    (N'Locksmith',      N'locksmith',      N'lock'),
    (N'Garden Service', N'garden-service', N'leaf'),
    (N'Appliance Repair', N'appliance-repair', N'plug'),
    (N'Air Conditioning', N'air-conditioning', N'snowflake'),
    (N'Solar Installer', N'solar-installer', N'sun')
) AS src (Name, Slug, IconName)
ON target.Slug = src.Slug
WHEN NOT MATCHED THEN
    INSERT (Name, Slug, IconName, IsActive)
    VALUES (src.Name, src.Slug, src.IconName, 1);
