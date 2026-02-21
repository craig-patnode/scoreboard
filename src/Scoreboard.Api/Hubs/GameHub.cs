using Microsoft.AspNetCore.SignalR;
using Scoreboard.Api.Services;

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
	private readonly GameStateCache _cache;

	public GameHub(GameService gameService, GameStateCache cache)
	{
		_gameService = gameService;
		_cache = cache;
	}

	/// <summary>
	/// Overlay clients call this to subscribe to a specific streamer's game updates.
	/// Hits DB once on join, then populates the cache so subsequent polls are free.
	/// </summary>
	public async Task JoinStream(string streamKey)
	{
		await Groups.AddToGroupAsync(Context.ConnectionId, streamKey);

		if (Guid.TryParse(streamKey, out var key))
		{
			var state = await _gameService.GetGameStateByStreamKeyAsync(key);
			if (state != null)
			{
				_cache.Set(streamKey, state);
				await Clients.Caller.SendAsync("GameStateUpdated", state);
			}

			// Logos sent separately — only on join/reconnect (never on polls)
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
	/// Lightweight state poll — reads from in-memory cache (zero DB queries).
	/// Client passes lastVersion; if unchanged, server sends nothing (zero bandwidth).
	/// Logos are NOT re-sent here — they only come via JoinStream/appearance changes.
	/// </summary>
	public async Task GetState(string streamKey, long lastVersion = -1)
	{
		var cached = _cache.Get(streamKey);
		if (cached != null)
		{
			if (lastVersion == cached.Version) return; // No change — send nothing
			await Clients.Caller.SendAsync("GameStateUpdated", cached.State);
			await Clients.Caller.SendAsync("StateVersion", cached.Version);
			return;
		}

		// Cache miss (first request before any broadcast) — fall back to DB
		if (Guid.TryParse(streamKey, out var key))
		{
			var state = await _gameService.GetGameStateByStreamKeyAsync(key);
			if (state != null)
			{
				var version = _cache.Set(streamKey, state);
				await Clients.Caller.SendAsync("GameStateUpdated", state);
				await Clients.Caller.SendAsync("StateVersion", version);
			}

			var (homeLogo, awayLogo) = await _gameService.GetLogosByStreamKeyAsync(key);
			await Clients.Caller.SendAsync("LogosUpdated", homeLogo, awayLogo);
		}
	}
}
