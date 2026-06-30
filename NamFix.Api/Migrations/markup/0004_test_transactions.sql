-- ============================================================================
-- MARKUP (mock) TEST DATA — platform transactions across providers, clients,
-- statuses and dates, so revenue/commission dashboards have data.
-- Idempotent (MERGE on Id). Depends on 0001 + 0002.
-- Status:   0 Pending, 1 Held, 2 PaidOut, 3 Refunded, 4 Failed
-- Commission is the platform default 10% (CommissionRate 0.1000).
-- ============================================================================

DECLARE @rate DECIMAL(5,4) = 0.1000;

;WITH raw AS (
    -- ProviderId, ClientUserId, GrossAmount, Status, DaysAgo, PaymentReference
    SELECT * FROM (VALUES
        (CAST('e0000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER), CAST(2500.00 AS DECIMAL(18,2)), 2,  40, N'PAY-TEST-0001'),
        (CAST('e0000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER), CAST(1800.00 AS DECIMAL(18,2)), 2,  28, N'PAY-TEST-0002'),
        (CAST('e0000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000003' AS UNIQUEIDENTIFIER), CAST(3200.00 AS DECIMAL(18,2)), 2,  22, N'PAY-TEST-0003'),
        (CAST('e0000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER), CAST(950.00  AS DECIMAL(18,2)), 1,  6,  N'PAY-TEST-0004'),
        (CAST('e0000000-0000-0000-0000-000000000003' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000005' AS UNIQUEIDENTIFIER), CAST(15400.00 AS DECIMAL(18,2)), 2, 35, N'PAY-TEST-0005'),
        (CAST('e0000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000006' AS UNIQUEIDENTIFIER), CAST(4200.00 AS DECIMAL(18,2)), 2,  18, N'PAY-TEST-0006'),
        (CAST('e0000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER), CAST(2750.00 AS DECIMAL(18,2)), 1,  3,  N'PAY-TEST-0007'),
        (CAST('e0000000-0000-0000-0000-000000000006' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER), CAST(6800.00 AS DECIMAL(18,2)), 2,  12, N'PAY-TEST-0008'),
        (CAST('e0000000-0000-0000-0000-000000000006' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000003' AS UNIQUEIDENTIFIER), CAST(1500.00 AS DECIMAL(18,2)), 3,  9,  N'PAY-TEST-0009'),
        (CAST('e0000000-0000-0000-0000-000000000007' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER), CAST(650.00  AS DECIMAL(18,2)), 2,  5,  N'PAY-TEST-0010'),
        (CAST('e0000000-0000-0000-0000-000000000010' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000005' AS UNIQUEIDENTIFIER), CAST(48500.00 AS DECIMAL(18,2)), 2, 50, N'PAY-TEST-0011'),
        (CAST('e0000000-0000-0000-0000-000000000010' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000006' AS UNIQUEIDENTIFIER), CAST(22000.00 AS DECIMAL(18,2)), 1, 2,  N'PAY-TEST-0012'),
        (CAST('e0000000-0000-0000-0000-000000000005' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER), CAST(3900.00 AS DECIMAL(18,2)), 0,  1,  N'PAY-TEST-0013'),
        (CAST('e0000000-0000-0000-0000-000000000003' AS UNIQUEIDENTIFIER), CAST('c0000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER), CAST(7300.00 AS DECIMAL(18,2)), 4,  15, N'PAY-TEST-0014')
    ) AS v (ProviderId, ClientUserId, GrossAmount, Status, DaysAgo, PaymentReference)
),
calc AS (
    SELECT
        -- Deterministic Id derived from the payment reference keeps the MERGE idempotent.
        CAST(HASHBYTES('MD5', r.PaymentReference) AS UNIQUEIDENTIFIER) AS Id,
        r.ProviderId,
        r.ClientUserId,
        p.PrimaryCategoryId                                            AS CategoryId,
        r.GrossAmount,
        @rate                                                          AS CommissionRate,
        CAST(ROUND(r.GrossAmount * @rate, 2) AS DECIMAL(18,2))         AS CommissionAmount,
        CAST(r.GrossAmount - ROUND(r.GrossAmount * @rate, 2) AS DECIMAL(18,2)) AS NetPayoutAmount,
        r.Status,
        DATEADD(DAY, -r.DaysAgo, SYSUTCDATETIME())                     AS CreatedAtUtc,
        CASE WHEN r.Status IN (1,2) THEN DATEADD(DAY, -r.DaysAgo, SYSUTCDATETIME()) END AS HeldAtUtc,
        CASE WHEN r.Status = 2 THEN DATEADD(DAY, -(r.DaysAgo-1), SYSUTCDATETIME()) END  AS PaidOutAtUtc,
        r.PaymentReference
    FROM raw r
    JOIN dbo.Providers p ON p.Id = r.ProviderId
)
MERGE dbo.Transactions AS target
USING calc AS src
ON target.Id = src.Id
WHEN NOT MATCHED THEN
    INSERT (Id, ProviderId, ClientUserId, CategoryId, GrossAmount, CommissionRate, CommissionAmount, NetPayoutAmount, Status, Currency, PaymentReference, CreatedAtUtc, HeldAtUtc, PaidOutAtUtc)
    VALUES (src.Id, src.ProviderId, src.ClientUserId, src.CategoryId, src.GrossAmount, src.CommissionRate, src.CommissionAmount, src.NetPayoutAmount, src.Status, N'NAD', src.PaymentReference, src.CreatedAtUtc, src.HeldAtUtc, src.PaidOutAtUtc);
