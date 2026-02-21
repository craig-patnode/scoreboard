using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scoreboard.Api.Data;
using Scoreboard.Api.Data.Entities;
using Scoreboard.Shared.DTOs;

namespace Scoreboard.Api.Services;

public class AuthService
{
	private readonly ScoreboardDbContext _db;
	private readonly IConfiguration _config;

	public AuthService(ScoreboardDbContext db, IConfiguration config)
	{
		_db = db;
		_config = config;
	}

	public async Task<AuthResponse> SignUpAsync(SignUpRequest request)
	{
		// Check if email already exists - if so, attempt login for idempotency
		var existing = await _db.Streamers
			.FirstOrDefaultAsync(s => s.EmailAddress == request.EmailAddress);
		if (existing != null)
		{
			// If the password matches, treat as a successful retry (idempotent)
			if (BCrypt.Net.BCrypt.Verify(request.Password, existing.PasswordHash))
			{
				var existingToken = GenerateJwtToken(existing);
				return new AuthResponse
				{
					Success = true,
					Token = existingToken,
					StreamerId = existing.StreamerId,
					StreamKey = existing.StreamKey.ToString(),
					DisplayName = existing.DisplayName
				};
			}
			return new AuthResponse { Success = false, Message = "Email already registered." };
		}

		var plan = await _db.SubscriptionPlans
			.FirstOrDefaultAsync(p => p.PlanCode == request.PlanCode && p.IsActive);

		if (plan == null)
			return new AuthResponse { Success = false, Message = "Invalid subscription plan." };

		int? discountId = null;
		if (!string.IsNullOrWhiteSpace(request.CouponCode))
		{
			var discount = await _db.Discounts.FirstOrDefaultAsync(d =>
				d.CouponCode == request.CouponCode
				&& d.IsActive
				&& d.ValidFromUtc <= DateTime.UtcNow
				&& (d.ValidToUtc == null || d.ValidToUtc > DateTime.UtcNow)
				&& (d.MaxRedemptions == null || d.CurrentRedemptions < d.MaxRedemptions));

			if (discount != null)
			{
				discountId = discount.DiscountId;
				discount.CurrentRedemptions++;
			}
		}

		var streamer = new Streamer
		{
			DisplayName = request.DisplayName,
			EmailAddress = request.EmailAddress,
			PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
			SubscriptionPlanId = plan.SubscriptionPlanId,
			DiscountId = discountId,
			SubscriptionStartUtc = DateTime.UtcNow,
			SubscriptionEndUtc = DateTime.UtcNow.AddMonths(plan.BillingIntervalMonths),
			IsActive = true
		};

		_db.Streamers.Add(streamer);
		await _db.SaveChangesAsync();

		// Provision default teams for Soccer
		_db.Teams.AddRange(
			new Team
			{
				StreamerId = streamer.StreamerId,
				TeamName = "Home",
				TeamCode = "HOME",
				JerseyColor = "#8B0000",
				NumberColor = "#FFFFFF",
				SportId = 1,
				IsDefault = true
			},
			new Team
			{
				StreamerId = streamer.StreamerId,
				TeamName = "Opponent",
				TeamCode = "OPP",
				JerseyColor = "#FFFFFF",
				NumberColor = "#003366",
				SportId = 1,
				IsDefault = true
			}
		);
		await _db.SaveChangesAsync();

		var token = GenerateJwtToken(streamer);
		return new AuthResponse
		{
			Success = true,
			Token = token,
			StreamerId = streamer.StreamerId,
			StreamKey = streamer.StreamKey.ToString(),
			DisplayName = streamer.DisplayName
		};
	}

	public async Task<AuthResponse> LoginAsync(LoginRequest request)
	{
		var streamer = await _db.Streamers
			.FirstOrDefaultAsync(s => s.EmailAddress == request.EmailAddress && s.IsActive && !s.IsBlocked);

		if (streamer == null || !BCrypt.Net.BCrypt.Verify(request.Password, streamer.PasswordHash))
			return new AuthResponse { Success = false, Message = "Invalid email or password." };

		var token = GenerateJwtToken(streamer);
		return new AuthResponse
		{
			Success = true,
			Token = token,
			StreamerId = streamer.StreamerId,
			StreamKey = streamer.StreamKey.ToString(),
			DisplayName = streamer.DisplayName
		};
	}

	public async Task<Streamer?> GetStreamerByIdAsync(int streamerId)
	{
		return await _db.Streamers
			.AsNoTracking()
			.FirstOrDefaultAsync(s => s.StreamerId == streamerId && s.IsActive && !s.IsBlocked);
	}

	public async Task<CouponValidationResponse> ValidateCouponAsync(string code)
	{
		if (string.IsNullOrWhiteSpace(code))
			return new CouponValidationResponse { IsValid = false, Message = "Coupon code is required." };

		var discount = await _db.Discounts.FirstOrDefaultAsync(d =>
			d.CouponCode.ToUpper() == code.ToUpper()
			&& d.IsActive
			&& d.ValidFromUtc <= DateTime.UtcNow
			&& (d.ValidToUtc == null || d.ValidToUtc > DateTime.UtcNow)
			&& (d.MaxRedemptions == null || d.CurrentRedemptions < d.MaxRedemptions));

		if (discount == null)
			return new CouponValidationResponse { IsValid = false, Message = "Invalid coupon code." };

		return new CouponValidationResponse
		{
			IsValid = true,
			Message = $"Coupon applied! {discount.DiscountPercent}% off.",
			DiscountPercent = discount.DiscountPercent ?? 0,
			Description = discount.Description
		};
	}

	private string GenerateJwtToken(Streamer streamer)
	{
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
			_config["Jwt:Key"] ?? "ScoreboardDefaultSecretKeyThatShouldBeChanged123!"));

		var claims = new[]
		{
			new Claim(ClaimTypes.NameIdentifier, streamer.StreamerId.ToString()),
			new Claim(ClaimTypes.Email, streamer.EmailAddress),
			new Claim(ClaimTypes.Name, streamer.DisplayName),
			new Claim("StreamKey", streamer.StreamKey.ToString()),
			new Claim("IsPilot", streamer.IsPilot.ToString())
		};

		var token = new JwtSecurityToken(
			issuer: _config["Jwt:Issuer"] ?? "Scoreboard",
			audience: _config["Jwt:Audience"] ?? "Scoreboard",
			claims: claims,
			expires: DateTime.UtcNow.AddHours(24),
			signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
		);

		return new JwtSecurityTokenHandler().WriteToken(token);
	}
}
