-- ============================================
-- Seed Data for Pilot Launch
-- ============================================

-- Sport: Soccer
SET IDENTITY_INSERT [dbo].[Sport] ON;
INSERT INTO [dbo].[Sport] ([SportId], [SportName], [SportCode], [HalvesCount], [PeriodName], [HasCards], [HasTimer], [TimerDirection], [DefaultPeriodLengthSeconds])
VALUES (1, N'Soccer', 'SOC', 2, N'Half', 1, 1, 'UP', 2700);
SET IDENTITY_INSERT [dbo].[Sport] OFF;
GO

-- Subscription Plans
SET IDENTITY_INSERT [dbo].[SubscriptionPlan] ON;
INSERT INTO [dbo].[SubscriptionPlan] ([SubscriptionPlanId], [PlanName], [PlanCode], [PriceAmount], [BillingIntervalMonths], [DiscountPercent])
VALUES
	(1, N'Monthly', 'MONTHLY', 9.99, 1, 0),
	(2, N'Yearly',  'YEARLY',  99.99, 12, 16.67);  -- ~$8.33/mo vs $9.99/mo
SET IDENTITY_INSERT [dbo].[SubscriptionPlan] OFF;
GO

-- Pilot Discount (100% off for pilot users)
SET IDENTITY_INSERT [dbo].[Discount] ON;
INSERT INTO [dbo].[Discount] ([DiscountId], [CouponCode], [Description], [DiscountPercent], [MaxRedemptions], [IsOneTimeUse])
VALUES (1, 'PILOT2026', N'Pilot program - free access', 100.00, 10, 1);
SET IDENTITY_INSERT [dbo].[Discount] OFF;
GO

-- Pilot Streamers: Craig & Dave (passwords will be hashed at app level, placeholder here)
SET IDENTITY_INSERT [dbo].[Streamer] ON;
INSERT INTO [dbo].[Streamer] ([StreamerId], [StreamKey], [StreamToken], [DisplayName], [EmailAddress], [PasswordHash], [SubscriptionPlanId], [DiscountId], [IsPilot], [IsActive])
VALUES
	(1, 'A1B2C3D4-E5F6-4A5B-8C9D-0E1F2A3B4C5D', 'F1E2D3C4-B5A6-4D5E-8F9A-0B1C2D3E4F5A', N'Craig Patnode', 'craig@scorecard.live', 'PLACEHOLDER_HASH', 2, 1, 1, 1),
	(2, 'B2C3D4E5-F6A7-4B5C-9D0E-1F2A3B4C5D6E', 'A2B3C4D5-E6F7-4C5D-0E1F-2A3B4C5D6E7F', N'Dave Brown',    'dave@scorecard.live',  'PLACEHOLDER_HASH', 2, 1, 1, 1);
SET IDENTITY_INSERT [dbo].[Streamer] OFF;
GO

-- Default Teams for Craig (Soccer)
SET IDENTITY_INSERT [dbo].[Team] ON;
INSERT INTO [dbo].[Team] ([TeamId], [StreamerId], [TeamName], [TeamCode], [JerseyColor], [NumberColor], [SportId], [IsDefault])
VALUES
	(1, 1, N'ECNL',      'ECNL',  '#8B0000', '#FFFFFF', 1, 1),  -- Craig's home team (dark red)
	(2, 1, N'Opponent',   'OPP',   '#FFFFFF', '#003366', 1, 1),  -- Craig's default opponent (white)
	(3, 2, N'Home',       'HOME',  '#003366', '#FFFFFF', 1, 1),  -- Dave's home team (navy)
	(4, 2, N'Opponent',   'OPP',   '#FFFFFF', '#000000', 1, 1);  -- Dave's default opponent
SET IDENTITY_INSERT [dbo].[Team] OFF;
GO
