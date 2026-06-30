-- Platform-wide default commission rate (10%). Admin can override per category/provider at runtime.
IF NOT EXISTS (SELECT 1 FROM dbo.CommissionRules WHERE Scope = 0 AND CategoryId IS NULL AND ProviderId IS NULL)
    INSERT INTO dbo.CommissionRules (Scope, CategoryId, ProviderId, Rate, IsActive)
    VALUES (0, NULL, NULL, 0.1000, 1);
