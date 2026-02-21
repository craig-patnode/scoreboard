using System.ComponentModel.DataAnnotations;
using Scoreboard.Shared.Enums;

namespace Scoreboard.Shared.DTOs;

/// <summary>
/// The complete game state sent to overlays via SignalR.
/// This is the core DTO shared across all clients (Web, OBS, future MAUI).
/// Timer is computed client-side from TimerStartedAtUtc + ElapsedSecondsAtPause.
/// </summary>
public class GameStateDto
{
	public int GameId { get; set; }
	public string GameStatus { get; set; } = "PREGAME";

	// Home Team
	public int HomeTeamId { get; set; }
	public string HomeTeamName { get; set; } = "Home";
	public string HomeTeamCode { get; set; } = "HOME";
	public string HomeJerseyColor { get; set; } = "#8B0000";
	public string HomeNumberColor { get; set; } = "#FFFFFF";
	public string? HomeLogoUrl { get; set; }
	public int HomeScore { get; set; }
	public int HomeYellowCards { get; set; }
	public int HomeRedCards { get; set; }

	// Away Team
	public int AwayTeamId { get; set; }
	public string AwayTeamName { get; set; } = "Opponent";
	public string AwayTeamCode { get; set; } = "OPP";
	public string AwayJerseyColor { get; set; } = "#FFFFFF";
	public string AwayNumberColor { get; set; } = "#003366";
	public string? AwayLogoUrl { get; set; }
	public int AwayScore { get; set; }
	public int AwayYellowCards { get; set; }
	public int AwayRedCards { get; set; }

	// Timer - client computes current time from these fields
	public bool IsTimerRunning { get; set; }
	public string TimerDirection { get; set; } = "UP";
	public int HalfLengthMinutes { get; set; } = 45;
	public int OtLengthMinutes { get; set; } = 5;
	public string HomePenaltyKicks { get; set; } = "[]";
	public string AwayPenaltyKicks { get; set; } = "[]";

	public int ElapsedSecondsAtPause { get; set; }
	public DateTime? TimerStartedAtUtc { get; set; }
	public int TimerSetSeconds { get; set; }
	public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;

	// Computed helper: what the timer shows right now (server-side snapshot)
	public int CurrentTimerSeconds
	{
		get
		{
			if (!IsTimerRunning)
				return ElapsedSecondsAtPause;

			var elapsed = ElapsedSecondsAtPause;
			if (TimerStartedAtUtc.HasValue)
			{
				elapsed += (int)(DateTime.UtcNow - TimerStartedAtUtc.Value).TotalSeconds;
			}

			if (TimerDirection == "DOWN")
				return Math.Max(0, TimerSetSeconds - elapsed);

			return elapsed;
		}
	}

	// Game info
	public string CurrentPeriod { get; set; } = "1H";
	public string? Venue { get; set; }
	public DateTime GameDateUtc { get; set; }

	// Sport config
	public string SportName { get; set; } = "Soccer";
	public string SportCode { get; set; } = "SOC";
	public bool HasCards { get; set; } = true;
	public bool HasTimer { get; set; } = true;
	public int DefaultPeriodLengthSeconds { get; set; } = 2700;
}

/// <summary>
/// Request to update a team's score
/// </summary>
public class UpdateScoreRequest
{
	[Range(0, 999)]
	public int Score { get; set; }
}

/// <summary>
/// Request to update a team's card count
/// </summary>
public class UpdateCardsRequest
{
	[Range(0, 20)]
	public int Count { get; set; }
}

/// <summary>
/// Request to set the timer value
/// </summary>
public class SetTimerRequest
{
	[Range(0, 36000)]
	public int Seconds { get; set; }
}

/// <summary>
/// Request to set the current period and half/OT settings
/// </summary>
public class SetPeriodRequest
{
	[Required]
	[StringLength(5)]
	public string CurrentPeriod { get; set; } = "1H";
	[Range(1, 120)]
	public int? HalfLengthMinutes { get; set; }
	[Range(1, 60)]
	public int? OtLengthMinutes { get; set; }
}

/// <summary>
/// Request to set timer direction
/// </summary>
public class SetTimerModeRequest
{
	public bool CountDown { get; set; }
}

/// <summary>
/// Request to update team name
/// </summary>
public class UpdateTeamNameRequest
{
	[Required]
	[StringLength(50, MinimumLength = 1)]
	public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Request to update team appearance (jersey color, number color, logo)
/// </summary>
public class UpdateTeamAppearanceRequest
{
	[StringLength(9)]
	public string? JerseyColor { get; set; }
	[StringLength(9)]
	public string? NumberColor { get; set; }
	[StringLength(2_000_000)]
	public string? LogoData { get; set; }  // Base64 data URI (e.g., "data:image/png;base64,...")
}

/// <summary>
/// Signup request
/// </summary>
public class SignUpRequest
{
	[Required]
	[StringLength(50, MinimumLength = 1)]
	public string DisplayName { get; set; } = string.Empty;
	[Required]
	[EmailAddress]
	[StringLength(254)]
	public string EmailAddress { get; set; } = string.Empty;
	[Required]
	[StringLength(128, MinimumLength = 8)]
	public string Password { get; set; } = string.Empty;
	[Required]
	[StringLength(20)]
	public string PlanCode { get; set; } = "MONTHLY";
	[StringLength(50)]
	public string? CouponCode { get; set; }
}

/// <summary>
/// Login request
/// </summary>
public class LoginRequest
{
	[Required]
	[EmailAddress]
	[StringLength(254)]
	public string EmailAddress { get; set; } = string.Empty;
	[Required]
	[StringLength(128)]
	public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Auth response
/// </summary>
public class AuthResponse
{
	public bool Success { get; set; }
	public string? Token { get; set; }
	public string? Message { get; set; }
	public int? StreamerId { get; set; }
	public string? StreamKey { get; set; }
	public string? DisplayName { get; set; }
}

/// <summary>
/// Create/start a new game
/// </summary>
public class CreateGameRequest
{
	public int? HomeTeamId { get; set; }
	public int? AwayTeamId { get; set; }
	[StringLength(50)]
	public string? HomeTeamName { get; set; }
	[StringLength(50)]
	public string? AwayTeamName { get; set; }
	[StringLength(100)]
	public string? Venue { get; set; }
}

public class RecordPenaltyRequest
{
	/// <summary>"home" or "away"</summary>
	[Required]
	[RegularExpression("^(home|away)$", ErrorMessage = "Team must be 'home' or 'away'.")]
	public string Team { get; set; } = "home";

	/// <summary>"goal" or "miss"</summary>
	[Required]
	[RegularExpression("^(goal|miss)$", ErrorMessage = "Result must be 'goal' or 'miss'.")]
	public string Result { get; set; } = "goal";
}

public class UndoPenaltyRequest
{
	/// <summary>"home" or "away"</summary>
	[Required]
	[RegularExpression("^(home|away)$", ErrorMessage = "Team must be 'home' or 'away'.")]
	public string Team { get; set; } = "home";
}

/// <summary>
/// Coupon validation response
/// </summary>
public class CouponValidationResponse
{
	public bool IsValid { get; set; }
	public string? Message { get; set; }
	public decimal DiscountPercent { get; set; }
	public string? Description { get; set; }
}

