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

    public GameController(GameService gameService, IHubContext<GameHub> hubContext)
    {
        _gameService = gameService;
        _hubContext = hubContext;
    }

    private int GetStreamerId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    private string GetStreamKey() =>
        User.FindFirstValue("StreamKey") ?? "";

    /// <summary>
    /// Push updated state to all overlay clients for this streamer
    /// </summary>
    private async Task BroadcastStateAsync()
    {
        var streamerId = GetStreamerId();
        var streamKey = GetStreamKey();
        var state = await _gameService.GetActiveGameStateAsync(streamerId);
        if (state != null && !string.IsNullOrEmpty(streamKey))
        {
            await _hubContext.Clients.Group(streamKey).SendAsync("GameStateUpdated", state);
        }
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
        var game = await _gameService.CreateGameAsync(GetStreamerId(), request);
        await BroadcastStateAsync();
        return Ok(new { game.GameId });
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
    [RequestSizeLimit(5_000_000)] // 5MB max for logo uploads
    public async Task<IActionResult> SetHomeTeamAppearance([FromBody] UpdateTeamAppearanceRequest request)
    {
        await _gameService.UpdateTeamAppearanceAsync(GetStreamerId(), true, request);
        await BroadcastStateAsync();
        return Ok();
    }

    [Authorize]
    [HttpPost("team/away/appearance")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> SetAwayTeamAppearance([FromBody] UpdateTeamAppearanceRequest request)
    {
        await _gameService.UpdateTeamAppearanceAsync(GetStreamerId(), false, request);
        await BroadcastStateAsync();
        return Ok();
    }
}
