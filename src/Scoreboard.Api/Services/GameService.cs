using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Scoreboard.Api.Data;
using Scoreboard.Api.Data.Entities;
using Scoreboard.Shared.DTOs;

namespace Scoreboard.Api.Services;

public class GameService
{
	private readonly ScoreboardDbContext _db;
	private static readonly HashSet<string> ValidPeriods = new() { "1H", "2H", "OT1", "OT2", "PEN" };
	private static readonly HashSet<string> AllowedLogoMimeTypes = new() { "data:image/png", "data:image/jpeg", "data:image/webp" };
	private static readonly Regex HexColorRegex = new(@"^#[0-9A-Fa-f]{3,8}$", RegexOptions.Compiled);
	private const int MaxPenaltyKicks = 15;

	public GameService(ScoreboardDbContext db)
	{
		_db = db;
	}

	/// <summary>
	/// Ensure a default Sport record exists and return its SportId.
	/// Avoids hardcoding SportId = 1 which fails if the Sport table is empty.
	/// </summary>
	private async Task<int> GetOrCreateDefaultSportIdAsync()
	{
		var sport = await _db.Sports.FirstOrDefaultAsync(s => s.SportCode == "SOC" && s.IsActive);
		if (sport != null) return sport.SportId;

		sport = new Sport
		{
			SportName = "Soccer",
			SportCode = "SOC",
			HalvesCount = 2,
			PeriodName = "Half",
			HasCards = true,
			HasTimer = true,
			TimerDirection = "UP",
			DefaultPeriodLengthSeconds = 2700,
			IsActive = true
		};
		_db.Sports.Add(sport);
		await _db.SaveChangesAsync();
		return sport.SportId;
	}

	/// <summary>
	/// Get the full game state DTO by stream key — WITHOUT logo data.
	/// </summary>
	public async Task<GameStateDto?> GetGameStateByStreamKeyAsync(Guid streamKey)
	{
		var streamer = await _db.Streamers
			.AsNoTracking()
			.FirstOrDefaultAsync(s => s.StreamKey == streamKey && s.IsActive && !s.IsBlocked);

		if (streamer == null) return null;

		return await GetActiveGameStateAsync(streamer.StreamerId);
	}

	/// <summary>
	/// Get the full game state DTO — WITHOUT logo data (fast, ~1KB result).
	/// Used for all real-time broadcasts and state fetches.
	/// </summary>
	public async Task<GameStateDto?> GetActiveGameStateAsync(int streamerId)
	{
		var dto = await _db.Games
			.Where(g => g.StreamerId == streamerId
						&& g.IsActive
						&& g.GameStatus != "FULLTIME")
			.Select(g => new GameStateDto
			{
				GameId = g.GameId,
				GameStatus = g.GameStatus,

				HomeTeamId = g.HomeTeamId,
				HomeTeamName = g.HomeTeam != null ? g.HomeTeam.TeamName : "Home",
				HomeTeamCode = g.HomeTeam != null ? g.HomeTeam.TeamCode : "HOME",
				HomeJerseyColor = g.HomeTeam != null ? g.HomeTeam.JerseyColor ?? "#8B0000" : "#8B0000",
				HomeNumberColor = g.HomeTeam != null ? g.HomeTeam.NumberColor ?? "#FFFFFF" : "#FFFFFF",
				// HomeLogoUrl intentionally omitted — loaded via GetLogosAsync
				HomeScore = g.TeamStats.Where(ts => ts.IsHome).Select(ts => ts.Score).FirstOrDefault(),
				HomeYellowCards = g.TeamStats.Where(ts => ts.IsHome).Select(ts => ts.YellowCards).FirstOrDefault(),
				HomeRedCards = g.TeamStats.Where(ts => ts.IsHome).Select(ts => ts.RedCards).FirstOrDefault(),

				AwayTeamId = g.AwayTeamId,
				AwayTeamName = g.AwayTeam != null ? g.AwayTeam.TeamName : "Opponent",
				AwayTeamCode = g.AwayTeam != null ? g.AwayTeam.TeamCode : "OPP",
				AwayJerseyColor = g.AwayTeam != null ? g.AwayTeam.JerseyColor ?? "#FFFFFF" : "#FFFFFF",
				AwayNumberColor = g.AwayTeam != null ? g.AwayTeam.NumberColor ?? "#003366" : "#003366",
				// AwayLogoUrl intentionally omitted
				AwayScore = g.TeamStats.Where(ts => !ts.IsHome).Select(ts => ts.Score).FirstOrDefault(),
				AwayYellowCards = g.TeamStats.Where(ts => !ts.IsHome).Select(ts => ts.YellowCards).FirstOrDefault(),
				AwayRedCards = g.TeamStats.Where(ts => !ts.IsHome).Select(ts => ts.RedCards).FirstOrDefault(),

				IsTimerRunning = g.TimerIsRunning,
				TimerDirection = g.TimerDirection,
				ElapsedSecondsAtPause = g.ElapsedSecondsAtPause,
				TimerStartedAtUtc = g.TimerStartedAtUtc,
				TimerSetSeconds = g.TimerSetSeconds,
				ServerTimeUtc = DateTime.UtcNow,

				CurrentPeriod = g.CurrentPeriod,
				HalfLengthMinutes = g.HalfLengthMinutes,
				OtLengthMinutes = g.OtLengthMinutes,
				HomePenaltyKicks = g.HomePenaltyKicks,
				AwayPenaltyKicks = g.AwayPenaltyKicks,
				Venue = g.Venue,
				GameDateUtc = g.GameDateUtc,

				SportName = g.Sport != null ? g.Sport.SportName : "Soccer",
				SportCode = g.Sport != null ? g.Sport.SportCode : "SOC",
				HasCards = g.Sport != null ? g.Sport.HasCards : true,
				HasTimer = g.Sport != null ? g.Sport.HasTimer : true,
				DefaultPeriodLengthSeconds = g.Sport != null ? g.Sport.DefaultPeriodLengthSeconds : 2700
			})
			.AsNoTracking()
			.FirstOrDefaultAsync();

		return dto;
	}


	/// <summary>
	/// Create a new game for a streamer (only one active at a time)
	/// </summary>
	public async Task<Game> CreateGameAsync(int streamerId, CreateGameRequest request)
	{
		// Deactivate any existing active games
		var existingGames = await _db.Games
			.Where(g => g.StreamerId == streamerId && g.IsActive && g.GameStatus != "FULLTIME")
			.ToListAsync();

		foreach (var eg in existingGames)
		{
			eg.GameStatus = "FULLTIME";
			eg.ModifiedDateUtc = DateTime.UtcNow;
		}

		// Save deactivation first to clear the unique filtered index
		if (existingGames.Any())
			await _db.SaveChangesAsync();

		// Ensure a default sport exists (avoids FK violation on empty Sport table)
		var sportId = await GetOrCreateDefaultSportIdAsync();

		// Look up by TeamCode — stable, never changes regardless of team name
		var homeTeam = await _db.Teams
			.FirstOrDefaultAsync(t => t.StreamerId == streamerId && t.TeamCode == "HOME" && t.IsActive);
		var awayTeam = await _db.Teams
			.FirstOrDefaultAsync(t => t.StreamerId == streamerId && t.TeamCode == "OPP" && t.IsActive);

		// Auto-create missing teams if needed
		if (homeTeam == null)
		{
			homeTeam = new Team
			{
				StreamerId = streamerId,
				TeamName = request.HomeTeamName ?? "Home",
				TeamCode = "HOME",
				JerseyColor = "#8B0000",
				NumberColor = "#FFFFFF",
				SportId = sportId,
				IsDefault = true
			};
			_db.Teams.Add(homeTeam);
			await _db.SaveChangesAsync();
		}

		if (awayTeam == null)
		{
			awayTeam = new Team
			{
				StreamerId = streamerId,
				TeamName = request.AwayTeamName ?? "Opponent",
				TeamCode = "OPP",
				JerseyColor = "#FFFFFF",
				NumberColor = "#003366",
				SportId = sportId,
				IsDefault = true
			};
			_db.Teams.Add(awayTeam);
			await _db.SaveChangesAsync();
		}

		int homeTeamId, awayTeamId;

		if (request.HomeTeamId.HasValue && request.AwayTeamId.HasValue)
		{
			homeTeamId = request.HomeTeamId.Value;
			awayTeamId = request.AwayTeamId.Value;
		}
		else
		{
			// Update team names if provided
			if (!string.IsNullOrWhiteSpace(request.HomeTeamName))
			{
				homeTeam.TeamName = request.HomeTeamName;
				homeTeam.ModifiedDateUtc = DateTime.UtcNow;
			}
			if (!string.IsNullOrWhiteSpace(request.AwayTeamName))
			{
				awayTeam.TeamName = request.AwayTeamName;
				awayTeam.ModifiedDateUtc = DateTime.UtcNow;
			}

			homeTeamId = homeTeam.TeamId;
			awayTeamId = awayTeam.TeamId;
		}

		var game = new Game
		{
			StreamerId = streamerId,
			SportId = sportId,
			HomeTeamId = homeTeamId,
			AwayTeamId = awayTeamId,
			GameDateUtc = DateTime.UtcNow,
			Venue = request.Venue,
			GameStatus = "PREGAME",
			TimerDirection = "UP",
			IsActive = true
		};

		_db.Games.Add(game);
		await _db.SaveChangesAsync();

		// Create team stats records
		_db.GameTeamStats.AddRange(
			new GameTeamStats { GameId = game.GameId, TeamId = homeTeamId, IsHome = true },
			new GameTeamStats { GameId = game.GameId, TeamId = awayTeamId, IsHome = false }
		);

		await _db.SaveChangesAsync();
		return game;
	}

	public async Task<Game?> GetActiveGameAsync(int streamerId)
	{
		return await _db.Games
			.Include(g => g.TeamStats)
			.FirstOrDefaultAsync(g => g.StreamerId == streamerId
									  && g.IsActive
									  && g.GameStatus != "FULLTIME");
	}

	/// <summary>
	/// Get ONLY the logo URLs for the active game's teams.
	/// Called once on client connect and when appearance changes — never on score/card/timer updates.
	/// </summary>
	public async Task<(string? HomeLogoUrl, string? AwayLogoUrl)> GetLogosAsync(int streamerId)
	{
		var result = await _db.Games
			.Where(g => g.StreamerId == streamerId
						&& g.IsActive
						&& g.GameStatus != "FULLTIME")
			.Select(g => new
			{
				HomeLogo = g.HomeTeam != null ? g.HomeTeam.LogoUrl : null,
				AwayLogo = g.AwayTeam != null ? g.AwayTeam.LogoUrl : null
			})
			.AsNoTracking()
			.FirstOrDefaultAsync();

		return (result?.HomeLogo, result?.AwayLogo);
	}

	/// <summary>
	/// Get logos by stream key (for hub/overlay use)
	/// </summary>
	public async Task<(string? HomeLogoUrl, string? AwayLogoUrl)> GetLogosByStreamKeyAsync(Guid streamKey)
	{
		var streamer = await _db.Streamers
			.AsNoTracking()
			.FirstOrDefaultAsync(s => s.StreamKey == streamKey && s.IsActive && !s.IsBlocked);

		if (streamer == null) return (null, null);

		return await GetLogosAsync(streamer.StreamerId);
	}

	// ---- Timer Operations ----

	public async Task StartTimerAsync(int streamerId)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		if (!game.TimerIsRunning)
		{
			game.TimerStartedAtUtc = DateTime.UtcNow;
			game.TimerIsRunning = true;
			if (game.GameStatus == "PREGAME") game.GameStatus = "LIVE";
			game.ModifiedDateUtc = DateTime.UtcNow;
			await _db.SaveChangesAsync();
		}
	}

	public async Task StopTimerAsync(int streamerId)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null || !game.TimerIsRunning) return;

		// Accumulate elapsed time
		if (game.TimerStartedAtUtc.HasValue)
		{
			game.ElapsedSecondsAtPause += (int)(DateTime.UtcNow - game.TimerStartedAtUtc.Value).TotalSeconds;
		}
		game.TimerIsRunning = false;
		game.TimerStartedAtUtc = null;
		game.ModifiedDateUtc = DateTime.UtcNow;
		await _db.SaveChangesAsync();
	}

	public async Task ResetTimerAsync(int streamerId)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		game.TimerIsRunning = false;
		game.TimerStartedAtUtc = null;
		game.ElapsedSecondsAtPause = 0;
		game.ModifiedDateUtc = DateTime.UtcNow;
		await _db.SaveChangesAsync();
	}

	public async Task SetTimerAsync(int streamerId, int seconds)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		game.TimerSetSeconds = seconds;
		game.ElapsedSecondsAtPause = seconds; // For count-up, this sets current position
		if (game.TimerDirection == "DOWN")
		{
			game.ElapsedSecondsAtPause = 0; // For countdown, reset elapsed
			game.TimerSetSeconds = seconds;
		}
		game.ModifiedDateUtc = DateTime.UtcNow;
		await _db.SaveChangesAsync();
	}

	public async Task SetTimerModeAsync(int streamerId, bool countDown)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		game.TimerDirection = countDown ? "DOWN" : "UP";
		game.ModifiedDateUtc = DateTime.UtcNow;
		await _db.SaveChangesAsync();
	}

	// ---- Score Operations ----

	public async Task UpdateScoreAsync(int streamerId, bool isHome, int score)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		var stats = game.TeamStats.FirstOrDefault(ts => ts.IsHome == isHome);
		if (stats == null) return;

		stats.Score = Math.Max(0, score);
		stats.ModifiedDateUtc = DateTime.UtcNow;
		await _db.SaveChangesAsync();
	}

	// ---- Card Operations ----

	public async Task UpdateCardsAsync(int streamerId, bool isHome, bool isYellow, int count)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		var stats = game.TeamStats.FirstOrDefault(ts => ts.IsHome == isHome);
		if (stats == null) return;

		var clamped = Math.Clamp(count, 0, 3);
		if (isYellow) stats.YellowCards = clamped;
		else stats.RedCards = clamped;

		stats.ModifiedDateUtc = DateTime.UtcNow;
		await _db.SaveChangesAsync();
	}

	// ---- Team Name Operations ----

	public async Task UpdateTeamNameAsync(int streamerId, bool isHome, string name)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		// Sanitize: trim and cap length
		var sanitized = (name ?? "").Trim();
		if (sanitized.Length > 50) sanitized = sanitized[..50];
		if (string.IsNullOrEmpty(sanitized)) return;

		var teamId = isHome ? game.HomeTeamId : game.AwayTeamId;
		var team = await _db.Teams.FindAsync(teamId);
		if (team == null) return;

		team.TeamName = sanitized;
		team.ModifiedDateUtc = DateTime.UtcNow;
		await _db.SaveChangesAsync();
	}

	public async Task UpdateTeamAppearanceAsync(int streamerId, bool isHome, UpdateTeamAppearanceRequest request)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		var teamId = isHome ? game.HomeTeamId : game.AwayTeamId;
		var team = await _db.Teams.FindAsync(teamId);
		if (team == null) return;

		if (request.JerseyColor != null && HexColorRegex.IsMatch(request.JerseyColor))
			team.JerseyColor = request.JerseyColor;
		if (request.NumberColor != null && HexColorRegex.IsMatch(request.NumberColor))
			team.NumberColor = request.NumberColor;
		if (request.LogoData != null)
		{
			// Validate logo MIME type — only allow known image formats
			var isValidLogo = AllowedLogoMimeTypes.Any(mime => request.LogoData.StartsWith(mime + ";base64,", StringComparison.OrdinalIgnoreCase));
			if (isValidLogo)
				team.LogoUrl = request.LogoData;
		}

		team.ModifiedDateUtc = DateTime.UtcNow;
		await _db.SaveChangesAsync();
	}

	// ---- Game Status Operations ----

	public async Task SetGameStatusAsync(int streamerId, string status)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		game.GameStatus = status;
		game.ModifiedDateUtc = DateTime.UtcNow;
		await _db.SaveChangesAsync();
	}

	public async Task SetPeriodAsync(int streamerId, SetPeriodRequest request)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		var normalized = request.CurrentPeriod?.ToUpperInvariant() ?? "1H";
		if (!ValidPeriods.Contains(normalized)) normalized = "1H";

		game.CurrentPeriod = normalized;

		if (request.HalfLengthMinutes.HasValue)
			game.HalfLengthMinutes = request.HalfLengthMinutes.Value;
		if (request.OtLengthMinutes.HasValue)
			game.OtLengthMinutes = request.OtLengthMinutes.Value;

		game.ModifiedDateUtc = DateTime.UtcNow;
		await _db.SaveChangesAsync();
	}

	public async Task ResetGameAsync(int streamerId)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		game.TimerIsRunning = false;
		game.TimerStartedAtUtc = null;
		game.ElapsedSecondsAtPause = 0;
		game.TimerSetSeconds = 0;
		game.GameStatus = "PREGAME";
		game.CurrentPeriod = "1H";
		game.HomePenaltyKicks = "[]";
		game.AwayPenaltyKicks = "[]";
		game.ModifiedDateUtc = DateTime.UtcNow;

		foreach (var stats in game.TeamStats)
		{
			stats.Score = 0;
			stats.YellowCards = 0;
			stats.RedCards = 0;
			stats.ModifiedDateUtc = DateTime.UtcNow;
		}

		await _db.SaveChangesAsync();
	}

	public async Task ResetPenaltiesAsync(int streamerId)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		game.HomePenaltyKicks = "[]";
		game.AwayPenaltyKicks = "[]";
		game.ModifiedDateUtc = DateTime.UtcNow;
		await _db.SaveChangesAsync();
	}

	// ---- Validation ----

	public async Task<Streamer?> ValidateStreamAccessAsync(Guid streamKey, Guid? streamToken)
	{
		var streamer = await _db.Streamers
			.AsNoTracking()
			.FirstOrDefaultAsync(s => s.StreamKey == streamKey && s.IsActive && !s.IsBlocked);

		if (streamer == null) return null;

		// If token is provided, validate it
		if (streamToken.HasValue && streamer.StreamToken != streamToken.Value)
			return null;

		return streamer;
	}
	
	public async Task RecordPenaltyKickAsync(int streamerId, string team, string result)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		var normalized = (result ?? "goal").ToLowerInvariant();
		if (normalized != "goal" && normalized != "miss") normalized = "goal";

		bool isHome = (team ?? "home").Equals("home", StringComparison.OrdinalIgnoreCase);
		var json = isHome ? game.HomePenaltyKicks : game.AwayPenaltyKicks;
		var kicks = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new List<string>();

		// Cap penalty kicks to prevent unbounded growth
		if (kicks.Count >= MaxPenaltyKicks) return;

		kicks.Add(normalized);
		var updated = System.Text.Json.JsonSerializer.Serialize(kicks);

		if (isHome) game.HomePenaltyKicks = updated;
		else game.AwayPenaltyKicks = updated;

		game.ModifiedDateUtc = DateTime.UtcNow;
		await _db.SaveChangesAsync();
	}

	public async Task UndoPenaltyKickAsync(int streamerId, string team)
	{
		var game = await GetActiveGameAsync(streamerId);
		if (game == null) return;

		bool isHome = (team ?? "home").Equals("home", StringComparison.OrdinalIgnoreCase);
		var json = isHome ? game.HomePenaltyKicks : game.AwayPenaltyKicks;
		var kicks = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new List<string>();

		if (kicks.Count > 0)
		{
			kicks.RemoveAt(kicks.Count - 1);
			var updated = System.Text.Json.JsonSerializer.Serialize(kicks);
			if (isHome) game.HomePenaltyKicks = updated;
			else game.AwayPenaltyKicks = updated;

			game.ModifiedDateUtc = DateTime.UtcNow;
			await _db.SaveChangesAsync();
		}
	}

}
