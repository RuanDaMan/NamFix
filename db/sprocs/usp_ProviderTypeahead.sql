-- Run-every-time stored procedure (CREATE OR ALTER) powering fast search autocomplete.
-- Uses CONTAINS with a prefix term for typeahead over the provider name + keywords.
CREATE OR ALTER PROCEDURE dbo.usp_ProviderTypeahead
    @Term NVARCHAR(100),
    @Take INT = 8
AS
BEGIN
    SET NOCOUNT ON;

    IF (@Term IS NULL OR LEN(@Term) < 2)
        RETURN;

    DECLARE @Pattern NVARCHAR(120) = N'"' + @Term + N'*"';

    SELECT TOP (@Take)
        p.Id,
        p.BusinessName,
        c.Name AS CategoryName
    FROM dbo.Providers p
    LEFT JOIN dbo.Categories c ON c.Id = p.PrimaryCategoryId
    WHERE p.Status = 1  -- Active
      AND CONTAINS((p.BusinessName, p.SearchKeywords), @Pattern)
    ORDER BY p.IsVerified DESC, p.RatingAverage DESC;
END;
