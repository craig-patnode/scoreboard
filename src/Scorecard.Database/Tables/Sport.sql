CREATE TABLE [dbo].[Sport]
(
    [SportId]       INT             NOT NULL    IDENTITY(1,1),
    [SportName]     NVARCHAR(50)    NOT NULL,
    [SportCode]     VARCHAR(10)     NOT NULL,   -- e.g., 'SOC', 'BBL', 'FTB'
    [HalvesCount]   INT             NOT NULL    DEFAULT 2,
    [PeriodName]    NVARCHAR(20)    NOT NULL    DEFAULT N'Half',  -- 'Half', 'Inning', 'Quarter'
    [Hasboards]      BIT             NOT NULL    DEFAULT 0,
    [HasTimer]      BIT             NOT NULL    DEFAULT 1,
    [TimerDirection] VARCHAR(4)     NOT NULL    DEFAULT 'UP',     -- 'UP' or 'DOWN'
    [DefaultPeriodLengthSeconds] INT NOT NULL   DEFAULT 2700,     -- 45 min for soccer
    [IsActive]      BIT             NOT NULL    DEFAULT 1,
    [CreatedDateUtc] DATETIME2(7)   NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_Sport] PRIMARY KEY CLUSTERED ([SportId]),
    CONSTRAINT [UQ_Sport_SportCode] UNIQUE ([SportCode]),
    CONSTRAINT [CK_Sport_TimerDirection] CHECK ([TimerDirection] IN ('UP', 'DOWN'))
);
GO
