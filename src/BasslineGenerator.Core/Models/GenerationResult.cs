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

    public GenerationStatistics Statistics => GenerationStatistics.From(Notes, Sections.Count, BarCount);
}

/// <summary>Summary of generated notes; the first three members predate the others and keep their order.</summary>
public sealed record GenerationStatistics(int NoteCount, int? LowestPitch, int? HighestPitch)
{
    public int UniqueNotes { get; init; }
    public string? LowestNote { get; init; }
    public string? HighestNote { get; init; }
    public double AverageNoteLengthBeats { get; init; }
    public double AverageVelocity { get; init; }
    /// <summary>Consecutive notes at least an octave apart.</summary>
    public int OctaveJumps { get; init; }
    public int SectionCount { get; init; }
    public int ArrangementBars { get; init; }
    public double ArrangementBeats { get; init; }

    public static GenerationStatistics From(IReadOnlyList<BassNote> notes, int sectionCount, int bars)
    {
        var jumps = 0;
        for (var i = 1; i < notes.Count; i++)
            if (Math.Abs(notes[i].Pitch - notes[i - 1].Pitch) >= 12) jumps++;
        var lowest = notes.Count == 0 ? (int?)null : notes.Min(n => n.Pitch);
        var highest = notes.Count == 0 ? (int?)null : notes.Max(n => n.Pitch);
        return new GenerationStatistics(notes.Count, lowest, highest)
        {
            UniqueNotes = notes.Select(n => n.Pitch).Distinct().Count(),
            LowestNote = lowest is { } low ? MusicTheory.NoteUtilities.NoteName(low) : null,
            HighestNote = highest is { } high ? MusicTheory.NoteUtilities.NoteName(high) : null,
            AverageNoteLengthBeats = notes.Count == 0 ? 0 : Math.Round(notes.Average(n => n.Length) / MusicTheory.MusicConstants.Ppq, 4),
            AverageVelocity = notes.Count == 0 ? 0 : Math.Round(notes.Average(n => n.Velocity), 2),
            OctaveJumps = jumps,
            SectionCount = sectionCount,
            ArrangementBars = bars,
            ArrangementBeats = bars * MusicTheory.MusicConstants.BeatsPerBar
        };
    }
}

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
