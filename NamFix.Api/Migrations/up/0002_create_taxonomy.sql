-- Reference taxonomy: towns, service categories, and moderated service tags.
IF OBJECT_ID('dbo.Towns', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Towns
    (
        Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Towns PRIMARY KEY,
        Name      NVARCHAR(120)     NOT NULL,
        Region    NVARCHAR(120)     NOT NULL,
        Latitude  FLOAT             NOT NULL,
        Longitude FLOAT             NOT NULL,
        IsActive  BIT               NOT NULL CONSTRAINT DF_Towns_IsActive DEFAULT (1)
    );
    CREATE UNIQUE INDEX UX_Towns_Name ON dbo.Towns (Name);
END;

IF OBJECT_ID('dbo.Categories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories
    (
        Id       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
        Name     NVARCHAR(120)     NOT NULL,
        Slug     NVARCHAR(120)     NOT NULL,
        IconName NVARCHAR(60)      NULL,
        IsActive BIT               NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT (1)
    );
    CREATE UNIQUE INDEX UX_Categories_Slug ON dbo.Categories (Slug);
END;

IF OBJECT_ID('dbo.Tags', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tags
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tags PRIMARY KEY,
        Name            NVARCHAR(80)      NOT NULL,
        Status          INT               NOT NULL CONSTRAINT DF_Tags_Status DEFAULT (0), -- TagStatus enum
        CreatedByUserId UNIQUEIDENTIFIER  NULL
    );
    CREATE UNIQUE INDEX UX_Tags_Name ON dbo.Tags (Name);
END;
