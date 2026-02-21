CREATE TABLE [dbo].[Discount]
(
	[DiscountId]        INT             NOT NULL    IDENTITY(1,1),
	[CouponCode]        VARCHAR(50)     NOT NULL,
	[Description]       NVARCHAR(200)   NULL,
	[DiscountPercent]   DECIMAL(5,2)    NULL,       -- Percentage off (mutually exclusive with Amount)
	[DiscountAmount]    DECIMAL(10,2)   NULL,       -- Fixed dollar amount off
	[MaxRedemptions]    INT             NULL,       -- NULL = unlimited
	[CurrentRedemptions] INT            NOT NULL    DEFAULT 0,
	[ValidFromUtc]      DATETIME2(7)    NOT NULL    DEFAULT SYSUTCDATETIME(),
	[ValidToUtc]        DATETIME2(7)    NULL,       -- NULL = no expiry
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
