using FloatingLyrics.Core.Models;

namespace FloatingLyrics.Media.WindowsSessions;

public sealed record MediaSessionInfo(string Id, string SourceId, string DisplayName, PlaybackState State, bool IsSelected);

/// <summary>Stable, source-independent selection policy. A manual session wins; automatic selection prefers playing sessions.</summary>
public static class MediaSessionSelector
{
    public static string? Select(IReadOnlyList<MediaSessionInfo> sessions, string? selectedSessionId,
        string? preferredSourceId, string? previousSessionId, string? systemSessionId)
    {
        if (selectedSessionId is not null && sessions.Any(s => s.Id == selectedSessionId))
            return selectedSessionId;

        var playing = sessions.Where(s => s.State == PlaybackState.Playing).ToArray();
        IReadOnlyList<MediaSessionInfo> pool = playing.Length > 0 ? playing : sessions;
        // A preference only affects a new choice. It must not flap between simultaneous playing sources.
        return pool.FirstOrDefault(s => s.Id == previousSessionId)?.Id
            ?? pool.FirstOrDefault(s => s.SourceId == preferredSourceId)?.Id
            ?? pool.FirstOrDefault(s => s.Id == systemSessionId)?.Id
            ?? pool.OrderBy(s => s.SourceId, StringComparer.Ordinal).ThenBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault()?.Id;
    }
}
