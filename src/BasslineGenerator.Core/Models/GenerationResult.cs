using Melanchall.DryWetMidi.Core;

namespace BasslineGenerator.Models;

/// <summary>Generated MIDI and its musical metadata.</summary>
public sealed record GenerationResult(MidiFile MidiFile, IReadOnlyList<BassNote> Notes,
    Motif Motif, string Progression, IReadOnlyList<SongSection> Sections,
    string Key, string ScaleType, SongWorld Style, int BPM, int BarCount)
{
    /// <summary>The motif reused across the generated sections.</summary>
    public IReadOnlyList<Motif> SelectedMotifs => [Motif];

    public GenerationStatistics Statistics => new(
        Notes.Count,
        Notes.Count == 0 ? null : Notes.Min(n => n.Pitch),
        Notes.Count == 0 ? null : Notes.Max(n => n.Pitch));
}

public sealed record GenerationStatistics(int NoteCount, int? LowestPitch, int? HighestPitch);
