-- ============================================
-- Scorecard Database Setup Script
-- Safe to re-run (drops and recreates)
-- ============================================
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- Drop in reverse dependency order
IF OBJECT_ID(N'dbo.GameTeamStats', N'U') IS NOT NULL DROP TABLE [dbo].[GameTeamStats];
IF OBJECT_ID(N'dbo.Game', N'U') IS NOT NULL DROP TABLE [dbo].[Game];
IF OBJECT_ID(N'dbo.Team', N'U') IS NOT NULL DROP TABLE [dbo].[Team];
IF OBJECT_ID(N'dbo.Streamer', N'U') IS NOT NULL DROP TABLE [dbo].[Streamer];
IF OBJECT_ID(N'dbo.Discount', N'U') IS NOT NULL DROP TABLE [dbo].[Discount];
IF OBJECT_ID(N'dbo.SubscriptionPlan', N'U') IS NOT NULL DROP TABLE [dbo].[SubscriptionPlan];
IF OBJECT_ID(N'dbo.Sport', N'U') IS NOT NULL DROP TABLE [dbo].[Sport];
GO

-- ============================================
-- 1. Sport
-- ============================================
CREATE TABLE [dbo].[Sport]
(
    [SportId]       INT             NOT NULL    IDENTITY(1,1),
    [SportName]     NVARCHAR(50)    NOT NULL,
    [SportCode]     VARCHAR(10)     NOT NULL,
    [HalvesCount]   INT             NOT NULL    DEFAULT 2,
    [PeriodName]    NVARCHAR(20)    NOT NULL    DEFAULT N'Half',
    [HasCards]      BIT             NOT NULL    DEFAULT 0,
    [HasTimer]      BIT             NOT NULL    DEFAULT 1,
    [TimerDirection] VARCHAR(4)     NOT NULL    DEFAULT 'UP',
    [DefaultPeriodLengthSeconds] INT NOT NULL   DEFAULT 2700,
    [IsActive]      BIT             NOT NULL    DEFAULT 1,
    [CreatedDateUtc] DATETIME2(7)   NOT NULL    DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Sport] PRIMARY KEY CLUSTERED ([SportId]),
    CONSTRAINT [UQ_Sport_SportCode] UNIQUE ([SportCode]),
    CONSTRAINT [CK_Sport_TimerDirection] CHECK ([TimerDirection] IN ('UP', 'DOWN'))
);
GO

-- ============================================
-- 2. SubscriptionPlan
-- ============================================
CREATE TABLE [dbo].[SubscriptionPlan]
(
    [SubscriptionPlanId] INT            NOT NULL    IDENTITY(1,1),
    [PlanName]           NVARCHAR(50)   NOT NULL,
    [PlanCode]           VARCHAR(20)    NOT NULL,
    [PriceAmount]        DECIMAL(10,2)  NOT NULL,
    [BillingIntervalMonths] INT         NOT NULL,
    [DiscountPercent]    DECIMAL(5,2)   NOT NULL    DEFAULT 0,
    [IsActive]           BIT            NOT NULL    DEFAULT 1,
    [CreatedDateUtc]     DATETIME2(7)   NOT NULL    DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_SubscriptionPlan] PRIMARY KEY CLUSTERED ([SubscriptionPlanId]),
    CONSTRAINT [UQ_SubscriptionPlan_PlanCode] UNIQUE ([PlanCode]),
    CONSTRAINT [CK_SubscriptionPlan_Price] CHECK ([PriceAmount] >= 0),
    CONSTRAINT [CK_SubscriptionPlan_Discount] CHECK ([DiscountPercent] >= 0 AND [DiscountPercent] <= 100)
);
GO

-- ============================================
-- 3. Discount
-- ============================================
CREATE TABLE [dbo].[Discount]
(
    [DiscountId]        INT             NOT NULL    IDENTITY(1,1),
    [CouponCode]        VARCHAR(50)     NOT NULL,
    [Description]       NVARCHAR(200)   NULL,
    [DiscountPercent]   DECIMAL(5,2)    NULL,
    [DiscountAmount]    DECIMAL(10,2)   NULL,
    [MaxRedemptions]    INT             NULL,
    [CurrentRedemptions] INT            NOT NULL    DEFAULT 0,
    [ValidFromUtc]      DATETIME2(7)    NOT NULL    DEFAULT SYSUTCDATETIME(),
    [ValidToUtc]        DATETIME2(7)    NULL,
    [IsOneTimeUse]      BIT             NOT NULL    DEFAULT 1,
    [IsActive]          BIT             NOT NULL    DEFAULT 1,
    [CreatedDateUtc]    DATETIME2(7)    NOT NULL    DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Discount] PRIMARY KEY CLUSTERED ([DiscountId]),
    CONSTRAINT [UQ_Discount_CouponCode] UNIQUE ([CouponCode]),
    CONSTRAINT [CK_Discount_Type] CHECK (
        ([DiscountPercent] IS NOT NULL AND [DiscountAmount] IS NULL) OR
        ([DiscountPercent] IS NULL AND [DiscountAmount] IS NOT NULL)
    ),
    CONSTRAINT [CK_Discount_Percent] CHECK ([DiscountPercent] IS NULL OR ([DiscountPercent] > 0 AND [DiscountPercent] <= 100)),
    CONSTRAINT [CK_Discount_Amount] CHECK ([DiscountAmount] IS NULL OR [DiscountAmount] > 0)
);
GO

-- ============================================
-- 4. Streamer
-- ============================================
CREATE TABLE [dbo].[Streamer]
(
    [StreamerId]        INT             NOT NULL    IDENTITY(1,1),
    [StreamKey]         UNIQUEIDENTIFIER NOT NULL   DEFAULT NEWID(),
    [StreamToken]       UNIQUEIDENTIFIER NOT NULL   DEFAULT NEWID(),
    [DisplayName]       NVARCHAR(100)   NOT NULL,
    [EmailAddress]      NVARCHAR(256)   NOT NULL,
    [PasswordHash]      NVARCHAR(512)   NOT NULL,
    [SubscriptionPlanId] INT            NULL,
    [DiscountId]        INT             NULL,
    [SubscriptionStartUtc] DATETIME2(7) NULL,
    [SubscriptionEndUtc]   DATETIME2(7) NULL,
    [IsPilot]           BIT             NOT NULL    DEFAULT 0,
    [IsDemoMode]        BIT             NOT NULL    DEFAULT 0,
    [IsActive]          BIT             NOT NULL    DEFAULT 1,
    [IsBlocked]         BIT             NOT NULL    DEFAULT 0,
    [CreatedDateUtc]    DATETIME2(7)    NOT NULL    DEFAULT SYSUTCDATETIME(),
    [ModifiedDateUtc]   DATETIME2(7)    NOT NULL    DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Streamer] PRIMARY KEY CLUSTERED ([StreamerId]),
    CONSTRAINT [UQ_Streamer_StreamKey] UNIQUE ([StreamKey]),
    CONSTRAINT [UQ_Streamer_StreamToken] UNIQUE ([StreamToken]),
    CONSTRAINT [UQ_Streamer_EmailAddress] UNIQUE ([EmailAddress]),
    CONSTRAINT [FK_Streamer_SubscriptionPlan] FOREIGN KEY ([SubscriptionPlanId])
        REFERENCES [dbo].[SubscriptionPlan]([SubscriptionPlanId]),
    CONSTRAINT [FK_Streamer_Discount] FOREIGN KEY ([DiscountId])
        REFERENCES [dbo].[Discount]([DiscountId])
);
GO

-- ============================================
-- 5. Team
-- ============================================
CREATE TABLE [dbo].[Team]
(
    [TeamId]            INT             NOT NULL    IDENTITY(1,1),
    [StreamerId]        INT             NOT NULL,
    [TeamName]          NVARCHAR(100)   NOT NULL,
    [ShortName]         NVARCHAR(20)    NULL,
    [JerseyColor]       VARCHAR(7)      NULL,
    [NumberColor]       VARCHAR(7)      NULL,
    [LogoUrl]           NVARCHAR(500)   NULL,
    [SportId]           INT             NOT NULL,
    [IsDefault]         BIT             NOT NULL    DEFAULT 0,
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

-- ============================================
-- 6. Game
-- ============================================
CREATE TABLE [dbo].[Game]
(
    [GameId]            INT             NOT NULL    IDENTITY(1,1),
    [StreamerId]        INT             NOT NULL,
    [SportId]           INT             NOT NULL,
    [HomeTeamId]        INT             NOT NULL,
    [AwayTeamId]        INT             NOT NULL,
    [GameDateUtc]       DATETIME2(7)    NOT NULL    DEFAULT SYSUTCDATETIME(),
    [Venue]             NVARCHAR(200)   NULL,
    [TimerStartedAtUtc]     DATETIME2(7)    NULL,
    [ElapsedSecondsAtPause] INT             NOT NULL DEFAULT 0,
    [TimerIsRunning]        BIT             NOT NULL DEFAULT 0,
    [TimerDirection]        VARCHAR(4)      NOT NULL DEFAULT 'UP',
    [TimerSetSeconds]       INT             NOT NULL DEFAULT 0,
    [CurrentPeriod]     INT             NOT NULL    DEFAULT 1,
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
    CONSTRAINT [CK_Game_TimerDirection] CHECK ([TimerDirection] IN ('UP', 'DOWN'))
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Game_ActivePerStreamer]
    ON [dbo].[Game]([StreamerId])
    WHERE [IsActive] = 1 AND [GameStatus] <> 'FULLTIME';
GO

-- ============================================
-- 7. GameTeamStats
-- ============================================
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

-- ============================================
-- SEED DATA
-- ============================================
SET IDENTITY_INSERT [dbo].[Sport] ON;
INSERT INTO [dbo].[Sport] ([SportId], [SportName], [SportCode], [HalvesCount], [PeriodName], [HasCards], [HasTimer], [TimerDirection], [DefaultPeriodLengthSeconds])
VALUES (1, N'Soccer', 'SOC', 2, N'Half', 1, 1, 'UP', 2700);
SET IDENTITY_INSERT [dbo].[Sport] OFF;
GO

SET IDENTITY_INSERT [dbo].[SubscriptionPlan] ON;
INSERT INTO [dbo].[SubscriptionPlan] ([SubscriptionPlanId], [PlanName], [PlanCode], [PriceAmount], [BillingIntervalMonths], [DiscountPercent])
VALUES
    (1, N'Monthly', 'MONTHLY', 9.99, 1, 0),
    (2, N'Yearly',  'YEARLY',  99.99, 12, 16.67);
SET IDENTITY_INSERT [dbo].[SubscriptionPlan] OFF;
GO

SET IDENTITY_INSERT [dbo].[Discount] ON;
INSERT INTO [dbo].[Discount] ([DiscountId], [CouponCode], [Description], [DiscountPercent], [MaxRedemptions], [IsOneTimeUse])
VALUES (1, 'PILOT2025', N'Pilot program - free access', 100.00, 10, 1);
SET IDENTITY_INSERT [dbo].[Discount] OFF;
GO

PRINT '✅ Scorecard database setup complete!';
PRINT 'Use the signup page to create pilot accounts (use coupon PILOT2025).';
GO
