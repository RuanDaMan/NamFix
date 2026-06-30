-- Sample accounts and provider listings for a working demo.
-- All sample accounts use the password:  Password123!
-- Hashes are PBKDF2-SHA256 in the app's {iterations}.{salt}.{key} format.

-- ---- Users (admin + three providers) ----
MERGE dbo.Users AS target
USING (VALUES
    (CAST('11111111-1111-1111-1111-111111111111' AS UNIQUEIDENTIFIER), N'admin@namfix.na', N'+264811110000', N'NamFix Admin',        3, N'100000.JgJJQ9CPP46JcZVtzLVJ2g==.A8b2KkibCYlqsCjlGAsR0+4IZ+PY9mN3NwO+OGkNdk0='),
    (CAST('22222222-2222-2222-2222-222222222222' AS UNIQUEIDENTIFIER), N'aqua@namfix.na',  N'+264811112222', N'Johannes Amupolo',     2, N'100000.0YfpITybRnldvMXyQbV0BQ==.KJihR/JGKoUpLpcl7zQbh9wKMmbHANzTAW/ua9vBpjc='),
    (CAST('33333333-3333-3333-3333-333333333333' AS UNIQUEIDENTIFIER), N'spark@namfix.na', N'+264811113333', N'Maria Shikongo',       2, N'100000.e38Kj6aVdqlC5pPWEv0rVg==.2r7/Y/ja/fnS1/u/+VrMx0MuRyKwDCC0naat4fmoObs='),
    (CAST('44444444-4444-4444-4444-444444444444' AS UNIQUEIDENTIFIER), N'auto@namfix.na',  N'+264811114444', N'Petrus Hangula',       2, N'100000.4oWzc/4YP7Qj8d2EP0eCvg==.KYylkMo7SMePKVEycXgJ6RmWNDWVrAtfMdB6ejrR+fk=')
) AS src (Id, Email, PhoneNumber, FullName, Role, PasswordHash)
ON target.Id = src.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Email, PhoneNumber, FullName, Role, PasswordHash, IsActive)
    VALUES (src.Id, src.Email, src.PhoneNumber, src.FullName, src.Role, src.PasswordHash, 1);

-- ---- Providers ----
DECLARE @plumber INT = (SELECT Id FROM dbo.Categories WHERE Slug = 'plumber');
DECLARE @electrician INT = (SELECT Id FROM dbo.Categories WHERE Slug = 'electrician');
DECLARE @mechanic INT = (SELECT Id FROM dbo.Categories WHERE Slug = 'mechanic');
DECLARE @windhoek INT = (SELECT Id FROM dbo.Towns WHERE Name = 'Windhoek');
DECLARE @swakop INT = (SELECT Id FROM dbo.Towns WHERE Name = 'Swakopmund');
DECLARE @walvis INT = (SELECT Id FROM dbo.Towns WHERE Name = 'Walvis Bay');
DECLARE @oshakati INT = (SELECT Id FROM dbo.Towns WHERE Name = 'Oshakati');

MERGE dbo.Providers AS target
USING (VALUES
    (CAST('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' AS UNIQUEIDENTIFIER), CAST('22222222-2222-2222-2222-222222222222' AS UNIQUEIDENTIFIER),
        N'AquaFix Plumbing', N'Reliable plumbing for homes & businesses. Geysers, leaks, drains.', @plumber, 1, 1, 1, 1, -22.5700, 17.0836, @windhoek,
        N'AquaFix Plumbing Plumber geyser repair after hours emergency callout borehole'),
    (CAST('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' AS UNIQUEIDENTIFIER), CAST('33333333-3333-3333-3333-333333333333' AS UNIQUEIDENTIFIER),
        N'Coastal Sparks Electrical', N'Certified electrician on the coast. Wiring, prepaid meters, solar.', @electrician, 1, 1, 1, 0, -22.6792, 14.5272, @swakop,
        N'Coastal Sparks Electrical Electrician wiring prepaid meters solar'),
    (CAST('cccccccc-cccc-cccc-cccc-cccccccccccc' AS UNIQUEIDENTIFIER), CAST('44444444-4444-4444-4444-444444444444' AS UNIQUEIDENTIFIER),
        N'Desert Auto Care', N'Trusted mechanic in the north. Engines, brakes, 24/7 breakdowns.', @mechanic, 1, 1, 0, 1, -17.7833, 15.7000, @oshakati,
        N'Desert Auto Care Mechanic engine repair brakes emergency callout')
) AS src (Id, UserId, BusinessName, Description, PrimaryCategoryId, Status, Availability, IsVerified, IsEmergencyCallout, Latitude, Longitude, PrimaryTownId, SearchKeywords)
ON target.Id = src.Id
WHEN NOT MATCHED THEN
    INSERT (Id, UserId, BusinessName, Description, PrimaryCategoryId, Status, Availability, IsVerified, IsEmergencyCallout, Latitude, Longitude, PrimaryTownId, RatingCount, SearchKeywords)
    VALUES (src.Id, src.UserId, src.BusinessName, src.Description, src.PrimaryCategoryId, src.Status, src.Availability, src.IsVerified, src.IsEmergencyCallout, src.Latitude, src.Longitude, src.PrimaryTownId, 0, src.SearchKeywords);

-- ---- Provider service towns ----
MERGE dbo.ProviderTowns AS target
USING (VALUES
    (CAST('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' AS UNIQUEIDENTIFIER), @windhoek),
    (CAST('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' AS UNIQUEIDENTIFIER), @swakop),
    (CAST('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' AS UNIQUEIDENTIFIER), @walvis),
    (CAST('cccccccc-cccc-cccc-cccc-cccccccccccc' AS UNIQUEIDENTIFIER), @oshakati)
) AS src (ProviderId, TownId)
ON target.ProviderId = src.ProviderId AND target.TownId = src.TownId
WHEN NOT MATCHED THEN INSERT (ProviderId, TownId) VALUES (src.ProviderId, src.TownId);

-- ---- Provider tags (link to approved tags by name) ----
;WITH links AS (
    SELECT CAST('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' AS UNIQUEIDENTIFIER) AS ProviderId, N'geyser repair' AS TagName UNION ALL
    SELECT CAST('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' AS UNIQUEIDENTIFIER), N'after hours' UNION ALL
    SELECT CAST('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' AS UNIQUEIDENTIFIER), N'borehole' UNION ALL
    SELECT CAST('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' AS UNIQUEIDENTIFIER), N'wiring' UNION ALL
    SELECT CAST('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' AS UNIQUEIDENTIFIER), N'solar' UNION ALL
    SELECT CAST('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' AS UNIQUEIDENTIFIER), N'prepaid meters' UNION ALL
    SELECT CAST('cccccccc-cccc-cccc-cccc-cccccccccccc' AS UNIQUEIDENTIFIER), N'engine repair' UNION ALL
    SELECT CAST('cccccccc-cccc-cccc-cccc-cccccccccccc' AS UNIQUEIDENTIFIER), N'brakes' UNION ALL
    SELECT CAST('cccccccc-cccc-cccc-cccc-cccccccccccc' AS UNIQUEIDENTIFIER), N'emergency callout'
)
MERGE dbo.ProviderTags AS target
USING (SELECT l.ProviderId, t.Id AS TagId FROM links l JOIN dbo.Tags t ON t.Name = l.TagName) AS src
ON target.ProviderId = src.ProviderId AND target.TagId = src.TagId
WHEN NOT MATCHED THEN INSERT (ProviderId, TagId) VALUES (src.ProviderId, src.TagId);
