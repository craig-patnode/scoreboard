CREATE TABLE [dbo].[SubscriptionPlan]
(
    [SubscriptionPlanId] INT            NOT NULL    IDENTITY(1,1),
    [PlanName]           NVARCHAR(50)   NOT NULL,   -- 'Monthly', 'Yearly'
    [PlanCode]           VARCHAR(20)    NOT NULL,   -- 'MONTHLY', 'YEARLY'
    [PriceAmount]        DECIMAL(10,2)  NOT NULL,
    [BillingIntervalMonths] INT         NOT NULL,   -- 1 for monthly, 12 for yearly
    [DiscountPercent]    DECIMAL(5,2)   NOT NULL    DEFAULT 0,  -- e.g., 20.00 for yearly discount
    [IsActive]           BIT            NOT NULL    DEFAULT 1,
    [CreatedDateUtc]     DATETIME2(7)   NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT [PK_SubscriptionPlan] PRIMARY KEY CLUSTERED ([SubscriptionPlanId]),
    CONSTRAINT [UQ_SubscriptionPlan_PlanCode] UNIQUE ([PlanCode]),
    CONSTRAINT [CK_SubscriptionPlan_Price] CHECK ([PriceAmount] >= 0),
    CONSTRAINT [CK_SubscriptionPlan_Discount] CHECK ([DiscountPercent] >= 0 AND [DiscountPercent] <= 100)
);
GO
