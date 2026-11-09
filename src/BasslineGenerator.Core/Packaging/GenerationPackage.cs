using BasslineGenerator.Models;

namespace BasslineGenerator.Packaging;

/// <summary>A note positioned in beats, suitable for piano-roll and timeline views.</summary>
public sealed record NoteData(string Note, int MidiNote, double StartBeat, double DurationBeats, int Velocity);

/// <summary>
/// Everything needed to reproduce, visualize, share and edit a generated bassline.
/// Only <see cref="Version"/>, <see cref="Seed"/> and <see cref="EffectiveConfiguration"/> are required as input;
/// statistics and notes are derived and recomputed on regeneration.
/// </summary>
public sealed record GenerationPackage(int Version, int Seed, GenerationConfiguration EffectiveConfiguration)
{
    public const int CurrentVersion = 1;

    public GenerationStatistics? Statistics { get; init; }
    public IReadOnlyList<NoteData>? Notes { get; init; }
}
