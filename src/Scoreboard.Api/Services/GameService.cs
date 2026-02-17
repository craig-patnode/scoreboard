using Microsoft.EntityFrameworkCore;
using Scoreboard.Api.Data;
using Scoreboard.Api.Data.Entities;
using Scoreboard.Shared.DTOs;

namespace Scoreboard.Api.Services;

public class GameService
{
    private readonly ScoreboardDbContext _db;

    public GameService(ScoreboardDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get the full game state DTO for a streamer's active game
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
    /// Get the active game state for a streamer by their ID
    /// </summary>
    public async Task<GameStateDto?> GetActiveGameStateAsync(int streamerId)
    {
        var game = await _db.Games
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Include(g => g.Sport)
            .Include(g => g.TeamStats)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.StreamerId == streamerId
                                      && g.IsActive
                                      && g.GameStatus != "FULLTIME");

        if (game == null) return null;

        var homeStats = game.TeamStats.FirstOrDefault(ts => ts.IsHome);
        var awayStats = game.TeamStats.FirstOrDefault(ts => !ts.IsHome);

        return new GameStateDto
        {
            GameId = game.GameId,
            GameStatus = game.GameStatus,

            HomeTeamId = game.HomeTeamId,
            HomeTeamName = game.HomeTeam?.TeamName ?? "Home",
            HomeTeamCode = game.HomeTeam?.TeamCode ?? "HOME",
            HomeJerseyColor = game.HomeTeam?.JerseyColor ?? "#8B0000",
            HomeNumberColor = game.HomeTeam?.NumberColor ?? "#FFFFFF",
            HomeLogoUrl = game.HomeTeam?.LogoUrl,
            HomeScore = homeStats?.Score ?? 0,
            HomeYellowCards = homeStats?.YellowCards ?? 0,
            HomeRedCards = homeStats?.RedCards ?? 0,

            AwayTeamId = game.AwayTeamId,
            AwayTeamName = game.AwayTeam?.TeamName ?? "Opponent",
            AwayTeamCode = game.AwayTeam?.TeamCode ?? "OPP",
            AwayJerseyColor = game.AwayTeam?.JerseyColor ?? "#FFFFFF",
            AwayNumberColor = game.AwayTeam?.NumberColor ?? "#003366",
            AwayLogoUrl = game.AwayTeam?.LogoUrl,
            AwayScore = awayStats?.Score ?? 0,
            AwayYellowCards = awayStats?.YellowCards ?? 0,
            AwayRedCards = awayStats?.RedCards ?? 0,

            IsTimerRunning = game.TimerIsRunning,
            TimerDirection = game.TimerDirection,
            ElapsedSecondsAtPause = game.ElapsedSecondsAtPause,
            TimerStartedAtUtc = game.TimerStartedAtUtc,
            TimerSetSeconds = game.TimerSetSeconds,
            ServerTimeUtc = DateTime.UtcNow,

            CurrentPeriod = game.CurrentPeriod,
            Venue = game.Venue,
            GameDateUtc = game.GameDateUtc,

            SportName = game.Sport?.SportName ?? "Soccer",
            SportCode = game.Sport?.SportCode ?? "SOC",
            HasCards = game.Sport?.HasCards ?? true,
            HasTimer = game.Sport?.HasTimer ?? true,
            DefaultPeriodLengthSeconds = game.Sport?.DefaultPeriodLengthSeconds ?? 2700
        };
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
                SportId = 1,
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
                SportId = 1,
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
            SportId = 1, // Soccer for now
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

        var teamId = isHome ? game.HomeTeamId : game.AwayTeamId;
        var team = await _db.Teams.FindAsync(teamId);
        if (team == null) return;

        team.TeamName = name;
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

        if (request.JerseyColor != null)
            team.JerseyColor = request.JerseyColor;
        if (request.NumberColor != null)
            team.NumberColor = request.NumberColor;
        if (request.LogoData != null)
            team.LogoUrl = request.LogoData;

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

    public async Task ResetGameAsync(int streamerId)
    {
        var game = await GetActiveGameAsync(streamerId);
        if (game == null) return;

        game.TimerIsRunning = false;
        game.TimerStartedAtUtc = null;
        game.ElapsedSecondsAtPause = 0;
        game.TimerSetSeconds = 0;
        game.GameStatus = "PREGAME";
        game.CurrentPeriod = 1;
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
}
