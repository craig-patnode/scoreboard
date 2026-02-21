using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Scoreboard.Api.Hubs;
using Scoreboard.Api.Services;
using Scoreboard.Shared.DTOs;

namespace Scoreboard.Api.Controllers;

[ApiController]
[Route("api/game")]
public class GameController : ControllerBase
{
    private readonly GameService _gameService;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly GameStateCache _cache;

    public GameController(GameService gameService, IHubContext<GameHub> hubContext, GameStateCache cache)
    {
        _gameService = gameService;
        _hubContext = hubContext;
        _cache = cache;
    }

    private int GetStreamerId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    private string GetStreamKey() =>
        User.FindFirstValue("StreamKey") ?? "";


    /// <summary>
    /// Push updated state to all overlay clients.
    /// Logo data is never included — the projection query skips LogoUrl entirely,
    /// so the DB never even reads those 200-500KB blobs on score/card/timer updates.
    /// </summary>
    private async Task BroadcastStateAsync()
    {
        var streamKey = GetStreamKey();
        var state = await _gameService.GetActiveGameStateAsync(GetStreamerId());
        if (state != null && !string.IsNullOrEmpty(streamKey))
        {
            _cache.Set(streamKey, state);
            await _hubContext.Clients.Group(streamKey).SendAsync("GameStateUpdated", state);
        }
    }

    /// <summary>
    /// Push logo URLs to all clients — only called when appearance changes.
    /// Uses a dedicated lightweight query that ONLY reads LogoUrl columns.
    /// </summary>
    private async Task BroadcastLogosAsync()
    {
        var streamerId = GetStreamerId();
        var streamKey = GetStreamKey();
        if (string.IsNullOrEmpty(streamKey)) return;

        var (homeLogo, awayLogo) = await _gameService.GetLogosAsync(streamerId);
        await _hubContext.Clients.Group(streamKey).SendAsync("LogosUpdated", homeLogo, awayLogo);
    }


    // ---- State ----

    [Authorize]
    [HttpGet("state")]
    public async Task<IActionResult> GetState()
    {
        var state = await _gameService.GetActiveGameStateAsync(GetStreamerId());
        if (state == null) return NotFound("No active game found.");
        return Ok(state);
    }

    /// <summary>
    /// Public endpoint for overlays - uses streamKey from query param, validates with X-Stream-Token header
    /// </summary>
    [HttpGet("state/{streamKey}")]
    public async Task<IActionResult> GetStateByStreamKey(string streamKey)
    {
        if (!Guid.TryParse(streamKey, out var key))
            return BadRequest("Invalid stream key.");

        // Validate X-Stream-Token header if provided
        Guid? token = null;
        if (Request.Headers.TryGetValue("X-Stream-Token", out var tokenHeader)
            && Guid.TryParse(tokenHeader.FirstOrDefault(), out var parsedToken))
        {
            token = parsedToken;
        }

        var streamer = await _gameService.ValidateStreamAccessAsync(key, token);
        if (streamer == null) return Unauthorized();

        var state = await _gameService.GetGameStateByStreamKeyAsync(key);
        if (state == null) return NotFound("No active game.");
        return Ok(state);
    }

    // ---- Game Lifecycle ----

    [Authorize]
    [HttpPost("create")]
    public async Task<IActionResult> CreateGame([FromBody] CreateGameRequest request)
    {
        try
        {
            var game = await _gameService.CreateGameAsync(GetStreamerId(), request);
            await BroadcastStateAsync();
            return Ok(new { game.GameId });
        }
        catch (Exception)
        {
            return StatusCode(500, new
            {
                error = "Failed to create game.",
                message = "An error occurred while setting up the game. Please try again."
            });
        }
    }

    [Authorize]
    [HttpPost("reset")]
    public async Task<IActionResult> ResetGame()
    {
        await _gameService.ResetGameAsync(GetStreamerId());
        await BroadcastStateAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("status")]
    public async Task<IActionResult> SetStatus([FromBody] Dictionary<string, string> body)
    {
        if (body.TryGetValue("status", out var status))
        {
            await _gameService.SetGameStatusAsync(GetStreamerId(), status.ToUpper());
            await BroadcastStateAsync();
        }
        return Ok();
    }

    // ---- Timer ----

    [Authorize]
    [HttpPost("timer/start")]
    public async Task<IActionResult> StartTimer()
    {
        await _gameService.StartTimerAsync(GetStreamerId());
        await BroadcastStateAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("timer/stop")]
    public async Task<IActionResult> StopTimer()
    {
        await _gameService.StopTimerAsync(GetStreamerId());
        await BroadcastStateAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("timer/reset")]
    public async Task<IActionResult> ResetTimer()
    {
        await _gameService.ResetTimerAsync(GetStreamerId());
        await BroadcastStateAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("timer/set")]
    public async Task<IActionResult> SetTimer([FromBody] SetTimerRequest request)
    {
        await _gameService.SetTimerAsync(GetStreamerId(), request.Seconds);
        await BroadcastStateAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("timer/mode")]
    public async Task<IActionResult> SetTimerMode([FromBody] SetTimerModeRequest request)
    {
        await _gameService.SetTimerModeAsync(GetStreamerId(), request.CountDown);
        await BroadcastStateAsync();
        return Ok();
    }

    // ---- Scores ----

    [Authorize]
    [HttpPost("score/home")]
    public async Task<IActionResult> SetHomeScore([FromBody] UpdateScoreRequest request)
    {
        await _gameService.UpdateScoreAsync(GetStreamerId(), true, request.Score);
        await BroadcastStateAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("score/away")]
    public async Task<IActionResult> SetAwayScore([FromBody] UpdateScoreRequest request)
    {
        await _gameService.UpdateScoreAsync(GetStreamerId(), false, request.Score);
        await BroadcastStateAsync();
        return Ok();
    }
    
    // ---- Period ----

    [Authorize]
    [HttpPost("period")]
    public async Task<IActionResult> SetPeriod([FromBody] SetPeriodRequest request)
    {
        await _gameService.SetPeriodAsync(GetStreamerId(), request);
        await BroadcastStateAsync();
        return Ok();
    }
    
    // ---- Cards ----

    [Authorize]
    [HttpPost("cards/home/yellow")]
    public async Task<IActionResult> SetHomeYellowCards([FromBody] UpdateCardsRequest request)
    {
        await _gameService.UpdateCardsAsync(GetStreamerId(), true, true, request.Count);
        await BroadcastStateAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("cards/home/red")]
    public async Task<IActionResult> SetHomeRedCards([FromBody] UpdateCardsRequest request)
    {
        await _gameService.UpdateCardsAsync(GetStreamerId(), true, false, request.Count);
        await BroadcastStateAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("cards/away/yellow")]
    public async Task<IActionResult> SetAwayYellowCards([FromBody] UpdateCardsRequest request)
    {
        await _gameService.UpdateCardsAsync(GetStreamerId(), false, true, request.Count);
        await BroadcastStateAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("cards/away/red")]
    public async Task<IActionResult> SetAwayRedCards([FromBody] UpdateCardsRequest request)
    {
        await _gameService.UpdateCardsAsync(GetStreamerId(), false, false, request.Count);
        await BroadcastStateAsync();
        return Ok();
    }

    // ---- Team Names ----

    [Authorize]
    [HttpPost("team/home/name")]
    public async Task<IActionResult> SetHomeTeamName([FromBody] UpdateTeamNameRequest request)
    {
        await _gameService.UpdateTeamNameAsync(GetStreamerId(), true, request.Name);
        await BroadcastStateAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("team/away/name")]
    public async Task<IActionResult> SetAwayTeamName([FromBody] UpdateTeamNameRequest request)
    {
        await _gameService.UpdateTeamNameAsync(GetStreamerId(), false, request.Name);
        await BroadcastStateAsync();
        return Ok();
    }

    // ---- Team Appearance ----

    [Authorize]
    [HttpPost("team/home/appearance")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> SetHomeTeamAppearance([FromBody] UpdateTeamAppearanceRequest request)
    {
        await _gameService.UpdateTeamAppearanceAsync(GetStreamerId(), true, request);
        await BroadcastStateAsync();
        await BroadcastLogosAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("team/away/appearance")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> SetAwayTeamAppearance([FromBody] UpdateTeamAppearanceRequest request)
    {
        await _gameService.UpdateTeamAppearanceAsync(GetStreamerId(), false, request);
        await BroadcastStateAsync();
        await BroadcastLogosAsync();
        return Ok();
    }

    // ---- Penalties ----

    [Authorize]
    [HttpPost("penalty/record")]
    public async Task<IActionResult> RecordPenalty([FromBody] RecordPenaltyRequest request)
    {
        await _gameService.RecordPenaltyKickAsync(GetStreamerId(), request.Team, request.Result);
        await BroadcastStateAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("penalty/undo")]
    public async Task<IActionResult> UndoPenalty([FromBody] UndoPenaltyRequest request)
    {
        await _gameService.UndoPenaltyKickAsync(GetStreamerId(), request.Team);
        await BroadcastStateAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("penalty/reset")]
    public async Task<IActionResult> ResetPenalties()
    {
        await _gameService.ResetPenaltiesAsync(GetStreamerId());
        await BroadcastStateAsync();
        return Ok();
    }

}
