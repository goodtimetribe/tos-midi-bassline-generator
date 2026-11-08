using Melanchall.DryWetMidi.Core;

namespace BasslineGenerator.Models;

/// <summary>Generated MIDI and its musical metadata.</summary>
public sealed record GenerationResult(MidiFile MidiFile, IReadOnlyList<BassNote> Notes,
    Motif Motif, string Progression, IReadOnlyList<SongSection> Sections,
    string Key, string ScaleType, SongWorld Style, int BPM, int BarCount)
{
    /// <summary>The effective, reproducible configuration used to create this result.</summary>
    public GenerationConfiguration Configuration { get; init; } = null!;

    /// <summary>The motif reused across the generated sections.</summary>
    public IReadOnlyList<Motif> SelectedMotifs => [Motif];

    public GenerationStatistics Statistics => new(
        Notes.Count,
        Notes.Count == 0 ? null : Notes.Min(n => n.Pitch),
        Notes.Count == 0 ? null : Notes.Max(n => n.Pitch));
}

public sealed record GenerationStatistics(int NoteCount, int? LowestPitch, int? HighestPitch);

/// <summary>Fully resolved generation settings, including a seed and each section's behavior.</summary>
public sealed record GenerationConfiguration(
    int Seed, string RootNote, string ScaleType, string Style, string Mode, int BPM, int Bars,
    string? RequestedProgression, string ProgressionId, string ProgressionName, IReadOnlyList<string> ChordSequence,
    int Channel, int Velocity, int VelocityVariation, int TimingVariation, double Swing, bool AccentDownbeats,
    int? Movement, int? Groove, int? Mutation, int? Tension, int? Repetition,
    IReadOnlyList<EffectiveSectionConfiguration> Sections);

/// <summary>A section and its style defaults after all request overrides.</summary>
public sealed record EffectiveSectionConfiguration(string Name, string Style, int Bars,
    MusicalDimensions Behavior);
