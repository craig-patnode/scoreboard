using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
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
	private readonly ILogger<AuthService> _logger;

	public AuthService(ScoreboardDbContext db, IConfiguration config, ILogger<AuthService> logger)
	{
		_db = db;
		_config = config;
		_logger = logger;
	}

	private static string? ValidatePassword(string password)
	{
		if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
			return "Password must be at least 8 characters.";
		if (!Regex.IsMatch(password, @"[A-Z]"))
			return "Password must contain at least one uppercase letter.";
		if (!Regex.IsMatch(password, @"[a-z]"))
			return "Password must contain at least one lowercase letter.";
		if (!Regex.IsMatch(password, @"[0-9]"))
			return "Password must contain at least one digit.";
		return null;
	}

	public async Task<AuthResponse> SignUpAsync(SignUpRequest request)
	{
		var passwordError = ValidatePassword(request.Password);
		if (passwordError != null)
			return new AuthResponse { Success = false, Message = passwordError };

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
			// Generic error to prevent email enumeration
			return new AuthResponse { Success = false, Message = "Unable to complete registration. Please try again." };
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
			Message = "Coupon applied successfully!",
			DiscountPercent = discount.DiscountPercent ?? 0,
			Description = discount.Description
		};
	}

	private string GenerateJwtToken(Streamer streamer)
	{
		var jwtKey = _config["Jwt:Key"]
			?? throw new InvalidOperationException("Jwt:Key must be configured.");
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

		var claims = new[]
		{
			new Claim(ClaimTypes.NameIdentifier, streamer.StreamerId.ToString()),
			new Claim(ClaimTypes.Name, streamer.DisplayName),
			new Claim("StreamKey", streamer.StreamKey.ToString()),
			new Claim("IsPilot", streamer.IsPilot.ToString())
		};

		var token = new JwtSecurityToken(
			issuer: _config["Jwt:Issuer"] ?? "Scoreboard",
			audience: _config["Jwt:Audience"] ?? "Scoreboard",
			claims: claims,
			expires: DateTime.UtcNow.AddHours(2),
			signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
		);

		return new JwtSecurityTokenHandler().WriteToken(token);
	}
}
