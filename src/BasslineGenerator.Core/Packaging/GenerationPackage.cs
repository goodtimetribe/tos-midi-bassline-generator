using BasslineGenerator.Models;

namespace BasslineGenerator.Packaging;

/// <summary>A note positioned in beats, suitable for piano-roll and timeline views.</summary>
public sealed record NoteData(string Note, int MidiNote, double StartBeat, double DurationBeats, int Velocity);

/// <summary>
/// Everything needed to reproduce, visualize, share and edit a generated bassline.
/// Statistics and notes are derived and recomputed on regeneration.
/// </summary>
public sealed record GenerationPackage(int Version, int Seed, GenerationConfiguration EffectiveConfiguration)
{
    public const int CurrentVersion = 1;
    public const string CurrentEngineVersion = "1.0.0";
    public const string LegacyEngineVersion = "1.0.0";

    /// <summary>Generator compatibility version; missing on packages created before engine versioning.</summary>
    public string? EngineVersion { get; init; }
    public GenerationStatistics? Statistics { get; init; }
    public IReadOnlyList<NoteData>? Notes { get; init; }
}
