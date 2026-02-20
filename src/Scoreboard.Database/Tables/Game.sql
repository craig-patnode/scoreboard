SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.Game', N'U') IS NOT NULL
    DROP TABLE [dbo].[Game];
GO

CREATE TABLE [dbo].[Game]
(
    [GameId]            INT             NOT NULL    IDENTITY(1,1),
    [StreamerId]        INT             NOT NULL,
    [SportId]           INT             NOT NULL,
    [HomeTeamId]        INT             NOT NULL,
    [AwayTeamId]        INT             NOT NULL,
    [GameDateUtc]       DATETIME2(7)    NOT NULL    DEFAULT SYSUTCDATETIME(),
    [Venue]             NVARCHAR(200)   NULL,

    -- Timer State (persisted for crash recovery)
    [TimerStartedAtUtc]     DATETIME2(7)    NULL,
    [ElapsedSecondsAtPause] INT             NOT NULL DEFAULT 0,
    [TimerIsRunning]        BIT             NOT NULL DEFAULT 0,
    [TimerDirection]        VARCHAR(4)      NOT NULL DEFAULT 'UP',
    [TimerSetSeconds]       INT             NOT NULL DEFAULT 0,

    -- Game State
    [CurrentPeriod]     VARCHAR(4)      NOT NULL    DEFAULT '1H',
    [HalfLengthMinutes] INT             NOT NULL    DEFAULT 45,
    [OtLengthMinutes]   INT             NOT NULL    DEFAULT 5,
    [HomePenaltyKicks]  NVARCHAR(200)   NOT NULL    DEFAULT '[]',
    [AwayPenaltyKicks]  NVARCHAR(200)   NOT NULL    DEFAULT '[]',
    [GameStatus]        VARCHAR(20)     NOT NULL    DEFAULT 'PREGAME',
    [IsActive]          BIT             NOT NULL    DEFAULT 1,

    [CreatedDateUtc]    DATETIME2(7)    NOT NULL    DEFAULT SYSUTCDATETIME(),
    [ModifiedDateUtc]   DATETIME2(7)    NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_Game] PRIMARY KEY CLUSTERED ([GameId]),
    CONSTRAINT [FK_Game_Streamer] FOREIGN KEY ([StreamerId])
        REFERENCES [dbo].[Streamer]([StreamerId]),
    CONSTRAINT [FK_Game_Sport] FOREIGN KEY ([SportId])
        REFERENCES [dbo].[Sport]([SportId]),
    CONSTRAINT [FK_Game_HomeTeam] FOREIGN KEY ([HomeTeamId])
        REFERENCES [dbo].[Team]([TeamId]),
    CONSTRAINT [FK_Game_AwayTeam] FOREIGN KEY ([AwayTeamId])
        REFERENCES [dbo].[Team]([TeamId]),
    CONSTRAINT [CK_Game_DifferentTeams] CHECK ([HomeTeamId] <> [AwayTeamId]),
    CONSTRAINT [CK_Game_Status] CHECK ([GameStatus] IN ('PREGAME', 'LIVE', 'HALFTIME', 'FULLTIME')),
    CONSTRAINT [CK_Game_TimerDirection] CHECK ([TimerDirection] IN ('UP', 'DOWN')),
    CONSTRAINT [CK_Game_CurrentPeriod] CHECK ([CurrentPeriod] IN ('1H', '2H', 'OT1', 'OT2', 'PEN'))
);
GO

-- Filtered index requires QUOTED_IDENTIFIER ON (set above)
CREATE UNIQUE NONCLUSTERED INDEX [UX_Game_ActivePerStreamer]
    ON [dbo].[Game]([StreamerId])
    WHERE [IsActive] = 1 AND [GameStatus] <> 'FULLTIME';
GO
