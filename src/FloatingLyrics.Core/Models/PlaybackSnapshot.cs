namespace FloatingLyrics.Core.Models;

public enum PlaybackState { Stopped, Paused, Playing }

/// <param name="ObservedAt">Timestamp of the authoritative player timeline, used to reject older samples.</param>
/// <param name="HasTimeline">False when the player cannot supply a usable position; timing must not be invented.</param>
public sealed record PlaybackSnapshot(
    string SourceId,
    TrackInfo? Track,
    PlaybackState State,
    TimeSpan Position,
    DateTimeOffset ObservedAt,
    bool HasTimeline = true,
    double PlaybackRate = 1);
