using Melanchall.DryWetMidi.Core;

namespace BasslineGenerator.Models;

/// <summary>Generated MIDI and its musical metadata.</summary>
public sealed record GenerationResult(MidiFile MidiFile, IReadOnlyList<BassNote> Notes,
    Motif Motif, string Progression, IReadOnlyList<SongSection> Sections);
