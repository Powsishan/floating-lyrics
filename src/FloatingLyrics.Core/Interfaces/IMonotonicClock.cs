namespace FloatingLyrics.Core.Interfaces;

public interface IMonotonicClock
{
    TimeSpan Elapsed { get; }
}
