using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;

namespace BasslineGenerator.Generation;

/// <summary>Generates sustained root-position triads from a selected progression.</summary>
public sealed class ChordGenerator
{
    public const int DefaultVelocity = 90;

    private const int ChordRootBase = 48;

    public IReadOnlyList<BassNote> Generate(string rootNote, ChordProgression progression,
        IReadOnlyList<SongSection> sections, int velocity = DefaultVelocity)
    {
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentNullException.ThrowIfNull(sections);
        if (progression.Chords.Count == 0) throw new ArgumentException("Progression must contain at least one chord.", nameof(progression));
        if (sections.Count == 0 || sections.Any(section => section is null || section.Bars < 1))
            throw new ArgumentException("Sections must contain at least one bar each.", nameof(sections));
        if (velocity is < 1 or > 127) throw new ArgumentOutOfRangeException(nameof(velocity));

        var tonic = NoteUtilities.PitchClass(rootNote);
        var notes = new List<BassNote>();
        var firstBar = 0;
        foreach (var section in sections)
        {
            for (var bar = 0; bar < section.Bars; bar++)
            {
                var chord = progression.Chords[(firstBar + bar) % progression.Chords.Count];
                var root = ChordRootBase + tonic + chord.RootOffset;
                var start = (long)(firstBar + bar) * MusicConstants.TicksPerBar;
                notes.Add(new BassNote(root, start, MusicConstants.TicksPerBar, velocity));
                notes.Add(new BassNote(root + chord.Third, start, MusicConstants.TicksPerBar, velocity));
                notes.Add(new BassNote(root + 7, start, MusicConstants.TicksPerBar, velocity));
            }
            firstBar += section.Bars;
        }
        return notes;
    }
}
