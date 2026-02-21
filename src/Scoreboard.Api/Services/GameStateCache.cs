using System.Collections.Concurrent;
using Scoreboard.Shared.DTOs;

namespace Scoreboard.Api.Services;

/// <summary>
/// In-memory cache for game state, keyed by stream key.
/// Eliminates DB round-trips when overlay clients poll for state.
///
/// Write path: BroadcastStateAsync caches state after every change.
/// Read path:  Hub.GetState reads from cache (zero DB queries).
///
/// Singleton lifetime — shared across all requests.
/// </summary>
public class GameStateCache
{
    private readonly ConcurrentDictionary<string, CachedEntry> _cache = new();

    public record CachedEntry(GameStateDto State, long Version, DateTime CachedAtUtc);

    /// <summary>
    /// Cache (or update) the game state for a stream key.
    /// Called by GameController after every state-changing action.
    /// </summary>
    public long Set(string streamKey, GameStateDto state)
    {
        var entry = _cache.AddOrUpdate(
            streamKey,
            _ => new CachedEntry(state, 1, DateTime.UtcNow),
            (_, existing) => new CachedEntry(state, existing.Version + 1, DateTime.UtcNow));
        return entry.Version;
    }

    /// <summary>
    /// Get cached state. Returns null if no cached entry exists.
    /// </summary>
    public CachedEntry? Get(string streamKey)
        => _cache.TryGetValue(streamKey, out var entry) ? entry : null;

    /// <summary>
    /// Remove cached entry (e.g., when game ends).
    /// </summary>
    public void Remove(string streamKey)
        => _cache.TryRemove(streamKey, out _);
}
