using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scoreboard.Api.Data.Entities;

[Table("Sport")]
public class Sport
{
    [Key]
    public int SportId { get; set; }
    public string SportName { get; set; } = string.Empty;
    public string SportCode { get; set; } = string.Empty;
    public int HalvesCount { get; set; } = 2;
    public string PeriodName { get; set; } = "Half";
    public bool HasCards { get; set; }
    public bool HasTimer { get; set; } = true;
    public string TimerDirection { get; set; } = "UP";
    public int DefaultPeriodLengthSeconds { get; set; } = 2700;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
}

[Table("SubscriptionPlan")]
public class SubscriptionPlan
{
    [Key]
    public int SubscriptionPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public decimal PriceAmount { get; set; }
    public int BillingIntervalMonths { get; set; }
    public decimal DiscountPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
}

[Table("Discount")]
public class Discount
{
    [Key]
    public int DiscountId { get; set; }
    public string CouponCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountAmount { get; set; }
    public int? MaxRedemptions { get; set; }
    public int CurrentRedemptions { get; set; }
    public DateTime ValidFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ValidToUtc { get; set; }
    public bool IsOneTimeUse { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
}

[Table("Streamer")]
public class Streamer
{
    [Key]
    public int StreamerId { get; set; }
    public Guid StreamKey { get; set; } = Guid.NewGuid();
    public Guid StreamToken { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int? SubscriptionPlanId { get; set; }
    public int? DiscountId { get; set; }
    public DateTime? SubscriptionStartUtc { get; set; }
    public DateTime? SubscriptionEndUtc { get; set; }
    public bool IsPilot { get; set; }
    public bool IsDemoMode { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsBlocked { get; set; }
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDateUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public SubscriptionPlan? SubscriptionPlan { get; set; }
    public Discount? DiscountApplied { get; set; }
    public ICollection<Team> Teams { get; set; } = new List<Team>();
    public ICollection<Game> Games { get; set; } = new List<Game>();
}

[Table("Team")]
public class Team
{
    [Key]
    public int TeamId { get; set; }
    public int StreamerId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string TeamCode { get; set; } = "HOME";  // "HOME" or "OPP" - stable lookup key
    public string? JerseyColor { get; set; }
    public string? NumberColor { get; set; }
    public string? LogoUrl { get; set; }
    public int SportId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDateUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Streamer? Streamer { get; set; }
    public Sport? Sport { get; set; }
}

[Table("Game")]
public class Game
{
    [Key]
    public int GameId { get; set; }
    public int StreamerId { get; set; }
    public int SportId { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public DateTime GameDateUtc { get; set; } = DateTime.UtcNow;
    public string? Venue { get; set; }

    // Timer state
    public DateTime? TimerStartedAtUtc { get; set; }
    public int ElapsedSecondsAtPause { get; set; }
    public bool TimerIsRunning { get; set; }
    public string TimerDirection { get; set; } = "UP";
    public int TimerSetSeconds { get; set; }

    // Game state
    public string CurrentPeriod { get; set; } = "1H";
    public string GameStatus { get; set; } = "PREGAME";
    public int HalfLengthMinutes { get; set; } = 45;
    public int OtLengthMinutes { get; set; } = 5;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDateUtc { get; set; } = DateTime.UtcNow;
    public string HomePenaltyKicks { get; set; } = "[]";
    public string AwayPenaltyKicks { get; set; } = "[]";


    // Navigation
    public Streamer? Streamer { get; set; }
    public Sport? Sport { get; set; }
    public Team? HomeTeam { get; set; }
    public Team? AwayTeam { get; set; }
    public ICollection<GameTeamStats> TeamStats { get; set; } = new List<GameTeamStats>();
}

[Table("GameTeamStats")]
public class GameTeamStats
{
    [Key]
    public int GameTeamStatsId { get; set; }
    public int GameId { get; set; }
    public int TeamId { get; set; }
    public bool IsHome { get; set; }
    public int Score { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public DateTime ModifiedDateUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Game? Game { get; set; }
    public Team? Team { get; set; }
}
