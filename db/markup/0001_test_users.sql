-- ============================================================================
-- MARKUP (mock) TEST DATA — test users (clients + extra providers).
-- Idempotent (MERGE on Id). Re-running refreshes without duplicating.
-- NOT part of the normal deploy (db/seed). Applied only via deploymarkup.ps1.
--
-- All test accounts use password:  Password123!
-- The hash below is the app's PBKDF2-SHA256 {iterations}.{salt}.{key} format,
-- reused from a known-good Password123! hash (the stored value is self-contained,
-- so every account verifies against Password123!).
-- ============================================================================

DECLARE @pwd NVARCHAR(400) = N'100000.JgJJQ9CPP46JcZVtzLVJ2g==.A8b2KkibCYlqsCjlGAsR0+4IZ+PY9mN3NwO+OGkNdk0=';

-- ---- Client users (Role = 1) ----
MERGE dbo.Users AS target
USING (VALUES
    (CAST('c0000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER), N'lena.client@test.namfix.na',   N'+264811220001', N'Lena Beukes',     1),
    (CAST('c0000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER), N'david.client@test.namfix.na',  N'+264811220002', N'David Nakale',    1),
    (CAST('c0000000-0000-0000-0000-000000000003' AS UNIQUEIDENTIFIER), N'sara.client@test.namfix.na',   N'+264811220003', N'Sara Iipinge',    1),
    (CAST('c0000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER), N'martin.client@test.namfix.na', N'+264811220004', N'Martin Gaeb',     1),
    (CAST('c0000000-0000-0000-0000-000000000005' AS UNIQUEIDENTIFIER), N'ndapewa.client@test.namfix.na',N'+264811220005', N'Ndapewa Amukwa',  1),
    (CAST('c0000000-0000-0000-0000-000000000006' AS UNIQUEIDENTIFIER), N'wilhelm.client@test.namfix.na',N'+264811220006', N'Wilhelm Diergaardt',1)
) AS src (Id, Email, PhoneNumber, FullName, Role)
ON target.Id = src.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Email, PhoneNumber, FullName, Role, PasswordHash, IsActive)
    VALUES (src.Id, src.Email, src.PhoneNumber, src.FullName, src.Role, @pwd, 1);

-- ---- Provider users (Role = 2) ----
MERGE dbo.Users AS target
USING (VALUES
    (CAST('d0000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER), N'carpentry@test.namfix.na', N'+264811330001', N'Theo Visagie',    2),
    (CAST('d0000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER), N'painter@test.namfix.na',   N'+264811330002', N'Selma Kandjala',  2),
    (CAST('d0000000-0000-0000-0000-000000000003' AS UNIQUEIDENTIFIER), N'builder@test.namfix.na',   N'+264811330003', N'Frans Eises',     2),
    (CAST('d0000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER), N'welder@test.namfix.na',    N'+264811330004', N'Lukas Tjihero',   2),
    (CAST('d0000000-0000-0000-0000-000000000005' AS UNIQUEIDENTIFIER), N'tiler@test.namfix.na',     N'+264811330005', N'Anna Nghikembua', 2),
    (CAST('d0000000-0000-0000-0000-000000000006' AS UNIQUEIDENTIFIER), N'roofer@test.namfix.na',    N'+264811330006', N'Gerson Haingura', 2),
    (CAST('d0000000-0000-0000-0000-000000000007' AS UNIQUEIDENTIFIER), N'locksmith@test.namfix.na', N'+264811330007', N'Helena Garoes',   2),
    (CAST('d0000000-0000-0000-0000-000000000008' AS UNIQUEIDENTIFIER), N'garden@test.namfix.na',    N'+264811330008', N'Tobias Kavari',   2),
    (CAST('d0000000-0000-0000-0000-000000000009' AS UNIQUEIDENTIFIER), N'aircon@test.namfix.na',    N'+264811330009', N'Ester Swartbooi', 2),
    (CAST('d0000000-0000-0000-0000-000000000010' AS UNIQUEIDENTIFIER), N'solar@test.namfix.na',     N'+264811330010', N'Paulus Shilongo', 2)
) AS src (Id, Email, PhoneNumber, FullName, Role)
ON target.Id = src.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Email, PhoneNumber, FullName, Role, PasswordHash, IsActive)
    VALUES (src.Id, src.Email, src.PhoneNumber, src.FullName, src.Role, @pwd, 1);
