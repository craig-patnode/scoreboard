CREATE TABLE [dbo].[Team]
(
    [TeamId]            INT             NOT NULL    IDENTITY(1,1),
    [StreamerId]        INT             NOT NULL,
    [TeamName]          NVARCHAR(100)   NOT NULL,
    [ShortName]         NVARCHAR(20)    NULL,
    [JerseyColor]       VARCHAR(7)      NULL,       -- Hex color e.g., '#8B0000'
    [NumberColor]       VARCHAR(7)      NULL,       -- Hex color for jersey numbers
    [LogoUrl]           NVARCHAR(500)   NULL,       -- URL to team crest/logo thumbnail
    [SportId]           INT             NOT NULL,
    [IsDefault]         BIT             NOT NULL    DEFAULT 0,  -- Default teams created at provisioning
    [IsActive]          BIT             NOT NULL    DEFAULT 1,
    [CreatedDateUtc]    DATETIME2(7)    NOT NULL    DEFAULT SYSUTCDATETIME(),
    [ModifiedDateUtc]   DATETIME2(7)    NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_Team] PRIMARY KEY CLUSTERED ([TeamId]),
    CONSTRAINT [FK_Team_Streamer] FOREIGN KEY ([StreamerId])
        REFERENCES [dbo].[Streamer]([StreamerId]),
    CONSTRAINT [FK_Team_Sport] FOREIGN KEY ([SportId])
        REFERENCES [dbo].[Sport]([SportId])
);
GO
