using Microsoft.AspNetCore.SignalR;
using Scorecard.Api.Services;
using Scorecard.Shared.DTOs;

namespace Scorecard.Api.Hubs;

/// <summary>
/// SignalR hub for real-time game state updates.
/// Overlays join a group by streamKey. Controller pushes updates to the group.
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

        // Send current state immediately (crash recovery)
        if (Guid.TryParse(streamKey, out var key))
        {
            var state = await _gameService.GetGameStateByStreamKeyAsync(key);
            if (state != null)
            {
                await Clients.Caller.SendAsync("GameStateUpdated", state);
            }
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
        }
    }
}
