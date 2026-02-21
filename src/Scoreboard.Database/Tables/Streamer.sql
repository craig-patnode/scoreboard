CREATE TABLE [dbo].[Streamer]
(
	[StreamerId]        INT             NOT NULL    IDENTITY(1,1),
	[StreamKey]         UNIQUEIDENTIFIER NOT NULL   DEFAULT NEWID(),  -- Public key for OBS URLs
	[StreamToken]       UNIQUEIDENTIFIER NOT NULL   DEFAULT NEWID(),  -- Secret token for X-Stream-Token header
	[DisplayName]       NVARCHAR(100)   NOT NULL,
	[EmailAddress]      NVARCHAR(256)   NOT NULL,
	[PasswordHash]      NVARCHAR(512)   NOT NULL,   -- Hashed password for auth
	[SubscriptionPlanId] INT            NULL,
	[DiscountId]        INT             NULL,       -- One-time coupon applied at signup
	[SubscriptionStartUtc] DATETIME2(7) NULL,
	[SubscriptionEndUtc]   DATETIME2(7) NULL,
	[IsPilot]           BIT             NOT NULL    DEFAULT 0,  -- Bypass payment for pilot users
	[IsDemoMode]        BIT             NOT NULL    DEFAULT 0,
	[IsActive]          BIT             NOT NULL    DEFAULT 1,
	[IsBlocked]         BIT             NOT NULL    DEFAULT 0,  -- Security: quick block
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
