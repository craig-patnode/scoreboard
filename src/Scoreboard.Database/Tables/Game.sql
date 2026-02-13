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
    [TimerStartedAtUtc]     DATETIME2(7)    NULL,   -- When timer was last started
    [ElapsedSecondsAtPause] INT             NOT NULL DEFAULT 0,  -- Accumulated seconds when paused
    [TimerIsRunning]        BIT             NOT NULL DEFAULT 0,
    [TimerDirection]        VARCHAR(4)      NOT NULL DEFAULT 'UP',  -- 'UP' or 'DOWN'
    [TimerSetSeconds]       INT             NOT NULL DEFAULT 0,    -- For countdown: total seconds set

    -- Game State
    [CurrentPeriod]     INT             NOT NULL    DEFAULT 1,     -- 1 = 1st half, 2 = 2nd half, etc.
    [GameStatus]        VARCHAR(20)     NOT NULL    DEFAULT 'PREGAME',  -- PREGAME, LIVE, HALFTIME, FULLTIME
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
    CONSTRAINT [CK_Game_TimerDirection] CHECK ([TimerDirection] IN ('UP', 'DOWN'))
);
GO

-- Index: Only one active game per streamer at a time
CREATE UNIQUE NONCLUSTERED INDEX [UX_Game_ActivePerStreamer]
    ON [dbo].[Game]([StreamerId])
    WHERE [IsActive] = 1 AND [GameStatus] <> 'FULLTIME';
GO
