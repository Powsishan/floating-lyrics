using FloatingLyrics.Core.Models;

namespace FloatingLyrics.Core.Interfaces;

public interface ILyricsProvider
{
    Task<IReadOnlyList<LyricsDocument>> SearchAsync(TrackInfo track, CancellationToken cancellationToken);
}
