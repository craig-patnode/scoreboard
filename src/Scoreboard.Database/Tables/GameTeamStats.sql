CREATE TABLE [dbo].[GameTeamStats]
(
    [GameTeamStatsId]   INT         NOT NULL    IDENTITY(1,1),
    [GameId]            INT         NOT NULL,
    [TeamId]            INT         NOT NULL,
    [IsHome]            BIT         NOT NULL    DEFAULT 0,
    [Score]             INT         NOT NULL    DEFAULT 0,
    [YellowCards]       INT         NOT NULL    DEFAULT 0,
    [RedCards]          INT         NOT NULL    DEFAULT 0,
    [ModifiedDateUtc]   DATETIME2(7) NOT NULL   DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_GameTeamStats] PRIMARY KEY CLUSTERED ([GameTeamStatsId]),
    CONSTRAINT [FK_GameTeamStats_Game] FOREIGN KEY ([GameId])
        REFERENCES [dbo].[Game]([GameId]),
    CONSTRAINT [FK_GameTeamStats_Team] FOREIGN KEY ([TeamId])
        REFERENCES [dbo].[Team]([TeamId]),
    CONSTRAINT [UQ_GameTeamStats_GameTeam] UNIQUE ([GameId], [TeamId]),
    CONSTRAINT [CK_GameTeamStats_Score] CHECK ([Score] >= 0),
    CONSTRAINT [CK_GameTeamStats_YellowCards] CHECK ([YellowCards] >= 0 AND [YellowCards] <= 3),
    CONSTRAINT [CK_GameTeamStats_RedCards] CHECK ([RedCards] >= 0 AND [RedCards] <= 3)
);
GO
