-- ============================================================================
-- MARKUP (mock) TEST DATA — provider listings across categories, towns and
-- statuses (Active / PendingApproval / Suspended) and availabilities.
-- Idempotent (MERGE on Id). Depends on 0001_test_users.sql.
-- Status:       0 PendingApproval, 1 Active, 2 Suspended, 3 Rejected
-- Availability: 1 Available, 2 Busy, 3 Offline
-- ============================================================================

-- Category lookups (by slug).
DECLARE @carpenter  INT = (SELECT Id FROM dbo.Categories WHERE Slug = 'carpenter');
DECLARE @painter    INT = (SELECT Id FROM dbo.Categories WHERE Slug = 'painter');
DECLARE @builder    INT = (SELECT Id FROM dbo.Categories WHERE Slug = 'builder');
DECLARE @welder     INT = (SELECT Id FROM dbo.Categories WHERE Slug = 'welder');
DECLARE @tiler      INT = (SELECT Id FROM dbo.Categories WHERE Slug = 'tiler');
DECLARE @roofer     INT = (SELECT Id FROM dbo.Categories WHERE Slug = 'roofer');
DECLARE @locksmith  INT = (SELECT Id FROM dbo.Categories WHERE Slug = 'locksmith');
DECLARE @garden     INT = (SELECT Id FROM dbo.Categories WHERE Slug = 'garden-service');
DECLARE @aircon     INT = (SELECT Id FROM dbo.Categories WHERE Slug = 'air-conditioning');
DECLARE @solar      INT = (SELECT Id FROM dbo.Categories WHERE Slug = 'solar-installer');

-- Town lookups (by name).
DECLARE @windhoek INT = (SELECT Id FROM dbo.Towns WHERE Name = 'Windhoek');
DECLARE @swakop   INT = (SELECT Id FROM dbo.Towns WHERE Name = 'Swakopmund');
DECLARE @walvis   INT = (SELECT Id FROM dbo.Towns WHERE Name = 'Walvis Bay');
DECLARE @otjiwa   INT = (SELECT Id FROM dbo.Towns WHERE Name = 'Otjiwarongo');
DECLARE @rundu    INT = (SELECT Id FROM dbo.Towns WHERE Name = 'Rundu');
DECLARE @gobabis  INT = (SELECT Id FROM dbo.Towns WHERE Name = 'Gobabis');
DECLARE @keetmans INT = (SELECT Id FROM dbo.Towns WHERE Name = 'Keetmanshoop');
DECLARE @ondangwa INT = (SELECT Id FROM dbo.Towns WHERE Name = 'Ondangwa');

-- ---- Providers ----
MERGE dbo.Providers AS target
USING (VALUES
    (CAST('e0000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER), CAST('d0000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER),
        N'Kalahari Custom Carpentry', N'Bespoke furniture, built-in cupboards and ceilings.', @carpenter, 1, 1, 1, 0, -22.5609, 17.0658, @windhoek,
        N'Kalahari Custom Carpentry Carpenter furniture ceilings'),
    (CAST('e0000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER), CAST('d0000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER),
        N'Coastal Colours Painting', N'Interior & exterior painting and waterproofing on the coast.', @painter, 1, 2, 1, 0, -22.6792, 14.5272, @swakop,
        N'Coastal Colours Painting Painter painting waterproofing'),
    (CAST('e0000000-0000-0000-0000-000000000003' AS UNIQUEIDENTIFIER), CAST('d0000000-0000-0000-0000-000000000003' AS UNIQUEIDENTIFIER),
        N'Namib Build Co', N'General building, paving and waterproofing. Free quotes.', @builder, 1, 1, 0, 1, -22.9576, 14.5053, @walvis,
        N'Namib Build Co Builder paving waterproofing'),
    (CAST('e0000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER), CAST('d0000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER),
        N'Highveld Welding Works', N'Gates, burglar bars and trailer repairs.', @welder, 1, 3, 1, 0, -20.4642, 16.6478, @otjiwa,
        N'Highveld Welding Works Welder gates burglar bars'),
    (CAST('e0000000-0000-0000-0000-000000000005' AS UNIQUEIDENTIFIER), CAST('d0000000-0000-0000-0000-000000000005' AS UNIQUEIDENTIFIER),
        N'Capital Tiling Pros', N'Floor and wall tiling for homes and offices.', @tiler, 1, 1, 0, 0, -22.5609, 17.0658, @windhoek,
        N'Capital Tiling Pros Tiler tiling'),
    (CAST('e0000000-0000-0000-0000-000000000006' AS UNIQUEIDENTIFIER), CAST('d0000000-0000-0000-0000-000000000006' AS UNIQUEIDENTIFIER),
        N'Kavango Roofing', N'Roof repairs, sheeting and waterproofing. Storm damage callouts.', @roofer, 1, 1, 1, 1, -17.9333, 19.7667, @rundu,
        N'Kavango Roofing Roofer roof repair waterproofing'),
    (CAST('e0000000-0000-0000-0000-000000000007' AS UNIQUEIDENTIFIER), CAST('d0000000-0000-0000-0000-000000000007' AS UNIQUEIDENTIFIER),
        N'SecureKey Locksmiths', N'24/7 lockouts, gates and burglar bars.', @locksmith, 1, 2, 1, 1, -22.5609, 17.0658, @windhoek,
        N'SecureKey Locksmiths Locksmith gates burglar bars after hours'),
    (CAST('e0000000-0000-0000-0000-000000000008' AS UNIQUEIDENTIFIER), CAST('d0000000-0000-0000-0000-000000000008' AS UNIQUEIDENTIFIER),
        N'Green Acacia Garden Service', N'Garden maintenance, irrigation and pool maintenance.', @garden, 0, 1, 0, 0, -22.4500, 18.9667, @gobabis,
        N'Green Acacia Garden Service Garden Service pool maintenance'),
    (CAST('e0000000-0000-0000-0000-000000000009' AS UNIQUEIDENTIFIER), CAST('d0000000-0000-0000-0000-000000000009' AS UNIQUEIDENTIFIER),
        N'Cool Karas Air Conditioning', N'Aircon installation and servicing in the south.', @aircon, 2, 3, 1, 0, -26.5833, 18.1333, @keetmans,
        N'Cool Karas Air Conditioning Air Conditioning aircon installation after hours'),
    (CAST('e0000000-0000-0000-0000-000000000010' AS UNIQUEIDENTIFIER), CAST('d0000000-0000-0000-0000-000000000010' AS UNIQUEIDENTIFIER),
        N'North Solar Solutions', N'Solar systems, inverters and prepaid meters in the north.', @solar, 1, 1, 1, 1, -17.9167, 15.9500, @ondangwa,
        N'North Solar Solutions Solar Installer solar prepaid meters')
) AS src (Id, UserId, BusinessName, Description, PrimaryCategoryId, Status, Availability, IsVerified, IsEmergencyCallout, Latitude, Longitude, PrimaryTownId, SearchKeywords)
ON target.Id = src.Id
WHEN NOT MATCHED THEN
    INSERT (Id, UserId, BusinessName, Description, PrimaryCategoryId, Status, Availability, IsVerified, IsEmergencyCallout, Latitude, Longitude, PrimaryTownId, RatingCount, SearchKeywords)
    VALUES (src.Id, src.UserId, src.BusinessName, src.Description, src.PrimaryCategoryId, src.Status, src.Availability, src.IsVerified, src.IsEmergencyCallout, src.Latitude, src.Longitude, src.PrimaryTownId, 0, src.SearchKeywords);

-- ---- Provider service towns (each serves its primary town; some serve neighbours) ----
MERGE dbo.ProviderTowns AS target
USING (VALUES
    (CAST('e0000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER), @windhoek),
    (CAST('e0000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER), @swakop),
    (CAST('e0000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER), @walvis),
    (CAST('e0000000-0000-0000-0000-000000000003' AS UNIQUEIDENTIFIER), @walvis),
    (CAST('e0000000-0000-0000-0000-000000000003' AS UNIQUEIDENTIFIER), @swakop),
    (CAST('e0000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER), @otjiwa),
    (CAST('e0000000-0000-0000-0000-000000000005' AS UNIQUEIDENTIFIER), @windhoek),
    (CAST('e0000000-0000-0000-0000-000000000006' AS UNIQUEIDENTIFIER), @rundu),
    (CAST('e0000000-0000-0000-0000-000000000007' AS UNIQUEIDENTIFIER), @windhoek),
    (CAST('e0000000-0000-0000-0000-000000000008' AS UNIQUEIDENTIFIER), @gobabis),
    (CAST('e0000000-0000-0000-0000-000000000009' AS UNIQUEIDENTIFIER), @keetmans),
    (CAST('e0000000-0000-0000-0000-000000000010' AS UNIQUEIDENTIFIER), @ondangwa)
) AS src (ProviderId, TownId)
ON target.ProviderId = src.ProviderId AND target.TownId = src.TownId
WHEN NOT MATCHED THEN INSERT (ProviderId, TownId) VALUES (src.ProviderId, src.TownId);

-- ---- Provider tags (link to approved tags by name) ----
;WITH links AS (
    SELECT CAST('e0000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER) AS ProviderId, N'furniture' AS TagName UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER), N'ceilings' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER), N'painting' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER), N'waterproofing' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000003' AS UNIQUEIDENTIFIER), N'paving' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000003' AS UNIQUEIDENTIFIER), N'waterproofing' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER), N'gates' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER), N'burglar bars' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000005' AS UNIQUEIDENTIFIER), N'tiling' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000006' AS UNIQUEIDENTIFIER), N'roof repair' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000006' AS UNIQUEIDENTIFIER), N'waterproofing' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000007' AS UNIQUEIDENTIFIER), N'gates' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000007' AS UNIQUEIDENTIFIER), N'burglar bars' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000007' AS UNIQUEIDENTIFIER), N'after hours' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000008' AS UNIQUEIDENTIFIER), N'pool maintenance' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000009' AS UNIQUEIDENTIFIER), N'aircon installation' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000009' AS UNIQUEIDENTIFIER), N'after hours' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000010' AS UNIQUEIDENTIFIER), N'solar' UNION ALL
    SELECT CAST('e0000000-0000-0000-0000-000000000010' AS UNIQUEIDENTIFIER), N'prepaid meters'
)
MERGE dbo.ProviderTags AS target
USING (SELECT l.ProviderId, t.Id AS TagId FROM links l JOIN dbo.Tags t ON t.Name = l.TagName) AS src
ON target.ProviderId = src.ProviderId AND target.TagId = src.TagId
WHEN NOT MATCHED THEN INSERT (ProviderId, TagId) VALUES (src.ProviderId, src.TagId);
