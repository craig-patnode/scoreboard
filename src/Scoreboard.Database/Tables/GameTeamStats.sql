CREATE TABLE [dbo].[GameTeamStats]
(
    [GameTeamStatsId]   INT         NOT NULL    IDENTITY(1,1),
    [GameId]            INT         NOT NULL,
    [TeamId]            INT         NOT NULL,
    [IsHome]            BIT         NOT NULL    DEFAULT 0,
    [Score]             INT         NOT NULL    DEFAULT 0,
    [Yellowboards]       INT         NOT NULL    DEFAULT 0,
    [Redboards]          INT         NOT NULL    DEFAULT 0,
    [ModifiedDateUtc]   DATETIME2(7) NOT NULL   DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_GameTeamStats] PRIMARY KEY CLUSTERED ([GameTeamStatsId]),
    CONSTRAINT [FK_GameTeamStats_Game] FOREIGN KEY ([GameId])
        REFERENCES [dbo].[Game]([GameId]),
    CONSTRAINT [FK_GameTeamStats_Team] FOREIGN KEY ([TeamId])
        REFERENCES [dbo].[Team]([TeamId]),
    CONSTRAINT [UQ_GameTeamStats_GameTeam] UNIQUE ([GameId], [TeamId]),
    CONSTRAINT [CK_GameTeamStats_Score] CHECK ([Score] >= 0),
    CONSTRAINT [CK_GameTeamStats_Yellowboards] CHECK ([Yellowboards] >= 0 AND [Yellowboards] <= 3),
    CONSTRAINT [CK_GameTeamStats_Redboards] CHECK ([Redboards] >= 0 AND [Redboards] <= 3)
);
GO
