-- Run-every-time view (CREATE OR ALTER so Grate can re-run it safely).
-- Flattened, search-friendly projection of active providers for reporting / ad-hoc queries.
CREATE OR ALTER VIEW dbo.vw_ProviderDirectory
AS
SELECT
    p.Id,
    p.BusinessName,
    p.Status,
    p.Availability,
    p.IsVerified,
    p.IsEmergencyCallout,
    p.RatingAverage,
    p.RatingCount,
    p.Latitude,
    p.Longitude,
    c.Name AS CategoryName,
    t.Name AS PrimaryTownName,
    t.Region AS Region
FROM dbo.Providers p
LEFT JOIN dbo.Categories c ON c.Id = p.PrimaryCategoryId
LEFT JOIN dbo.Towns t ON t.Id = p.PrimaryTownId;
