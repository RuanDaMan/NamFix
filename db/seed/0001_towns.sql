-- Idempotent seed of major Namibian towns. Admin-extensible at runtime.
MERGE dbo.Towns AS target
USING (VALUES
    (N'Windhoek',       N'Khomas',        -22.5609, 17.0658),
    (N'Rehoboth',       N'Hardap',        -23.3167, 17.0833),
    (N'Okahandja',      N'Otjozondjupa',  -21.9833, 16.9167),
    (N'Swakopmund',     N'Erongo',        -22.6792, 14.5272),
    (N'Walvis Bay',     N'Erongo',        -22.9576, 14.5053),
    (N'Henties Bay',    N'Erongo',        -22.1167, 14.2833),
    (N'Omaruru',        N'Erongo',        -21.4333, 15.9333),
    (N'Karibib',        N'Erongo',        -21.9333, 15.8500),
    (N'Usakos',         N'Erongo',        -21.9833, 15.5833),
    (N'Otjiwarongo',    N'Otjozondjupa',  -20.4642, 16.6478),
    (N'Tsumeb',         N'Oshikoto',      -19.2333, 17.7167),
    (N'Grootfontein',   N'Otjozondjupa',  -19.5667, 18.1167),
    (N'Oshakati',       N'Oshana',        -17.7833, 15.7000),
    (N'Ongwediva',      N'Oshana',        -17.7833, 15.7667),
    (N'Ondangwa',       N'Oshana',        -17.9167, 15.9500),
    (N'Outapi',         N'Omusati',       -17.5000, 14.9833),
    (N'Eenhana',        N'Ohangwena',     -17.4667, 16.3333),
    (N'Rundu',          N'Kavango East',  -17.9333, 19.7667),
    (N'Katima Mulilo',  N'Zambezi',       -17.5000, 24.2667),
    (N'Gobabis',        N'Omaheke',       -22.4500, 18.9667),
    (N'Mariental',      N'Hardap',        -24.6333, 17.9667),
    (N'Keetmanshoop',   N'//Karas',       -26.5833, 18.1333),
    (N'Lüderitz',       N'//Karas',       -26.6481, 15.1594),
    (N'Outjo',          N'Kunene',        -20.1167, 16.1500),
    (N'Khorixas',       N'Kunene',        -20.3667, 14.9667),
    (N'Opuwo',          N'Kunene',        -18.0607, 13.8400)
) AS src (Name, Region, Latitude, Longitude)
ON target.Name = src.Name
WHEN NOT MATCHED THEN
    INSERT (Name, Region, Latitude, Longitude, IsActive)
    VALUES (src.Name, src.Region, src.Latitude, src.Longitude, 1);
