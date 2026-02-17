using Microsoft.AspNetCore.SignalR;
using Scoreboard.Api.Services;
using Scoreboard.Shared.DTOs;

namespace Scoreboard.Api.Hubs;

/// <summary>
/// SignalR hub for real-time game state updates.
/// Overlays join a group by streamKey. Controller pushes updates to the group.
/// 
/// PERFORMANCE: State broadcasts never include logo blobs.
/// Logos are loaded via a dedicated lightweight query and sent on a separate channel.
/// </summary>
public class GameHub : Hub
{
    private readonly GameService _gameService;

    public GameHub(GameService gameService)
    {
        _gameService = gameService;
    }

    /// <summary>
    /// Overlay clients call this to subscribe to a specific streamer's game updates
    /// </summary>
    public async Task JoinStream(string streamKey)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, streamKey);

        if (Guid.TryParse(streamKey, out var key))
        {
            // Send lightweight state (no logos — projection query skips LogoUrl)
            var state = await _gameService.GetGameStateByStreamKeyAsync(key);
            if (state != null)
            {
                await Clients.Caller.SendAsync("GameStateUpdated", state);
            }

            // Send logos separately via dedicated lightweight query
            var (homeLogo, awayLogo) = await _gameService.GetLogosByStreamKeyAsync(key);
            await Clients.Caller.SendAsync("LogosUpdated", homeLogo, awayLogo);
        }
    }

    /// <summary>
    /// Leave a stream group
    /// </summary>
    public async Task LeaveStream(string streamKey)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, streamKey);
    }

    /// <summary>
    /// Request current game state (for reconnection/crash recovery)
    /// </summary>
    public async Task GetState(string streamKey)
    {
        if (Guid.TryParse(streamKey, out var key))
        {
            var state = await _gameService.GetGameStateByStreamKeyAsync(key);
            if (state != null)
            {
                await Clients.Caller.SendAsync("GameStateUpdated", state);
            }

            var (homeLogo, awayLogo) = await _gameService.GetLogosByStreamKeyAsync(key);
            await Clients.Caller.SendAsync("LogosUpdated", homeLogo, awayLogo);
        }
    }
}
