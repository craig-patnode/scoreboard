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
    public string? HomeTeamShortName { get; set; }
    public string HomeJerseyColor { get; set; } = "#8B0000";
    public string HomeNumberColor { get; set; } = "#FFFFFF";
    public string? HomeLogoUrl { get; set; }
    public int HomeScore { get; set; }
    public int HomeYellowboards { get; set; }
    public int HomeRedboards { get; set; }

    // Away Team
    public int AwayTeamId { get; set; }
    public string AwayTeamName { get; set; } = "Opponent";
    public string? AwayTeamShortName { get; set; }
    public string AwayJerseyColor { get; set; } = "#FFFFFF";
    public string AwayNumberColor { get; set; } = "#003366";
    public string? AwayLogoUrl { get; set; }
    public int AwayScore { get; set; }
    public int AwayYellowboards { get; set; }
    public int AwayRedboards { get; set; }

    // Timer - client computes current time from these fields
    public bool IsTimerRunning { get; set; }
    public string TimerDirection { get; set; } = "UP";
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
    public int CurrentPeriod { get; set; } = 1;
    public string? Venue { get; set; }
    public DateTime GameDateUtc { get; set; }

    // Sport config
    public string SportName { get; set; } = "Soccer";
    public string SportCode { get; set; } = "SOC";
    public bool Hasboards { get; set; } = true;
    public bool HasTimer { get; set; } = true;
    public int DefaultPeriodLengthSeconds { get; set; } = 2700;
}

/// <summary>
/// Request to update a team's score
/// </summary>
public class UpdateScoreRequest
{
    public int Score { get; set; }
}

/// <summary>
/// Request to update a team's board count
/// </summary>
public class UpdateboardsRequest
{
    public int Count { get; set; }
}

/// <summary>
/// Request to set the timer value
/// </summary>
public class SetTimerRequest
{
    public int Seconds { get; set; }
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
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Signup request
/// </summary>
public class SignUpRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PlanCode { get; set; } = "MONTHLY";
    public string? CouponCode { get; set; }
}

/// <summary>
/// Login request
/// </summary>
public class LoginRequest
{
    public string EmailAddress { get; set; } = string.Empty;
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
    public string? HomeTeamName { get; set; }
    public string? AwayTeamName { get; set; }
    public string? Venue { get; set; }
}
