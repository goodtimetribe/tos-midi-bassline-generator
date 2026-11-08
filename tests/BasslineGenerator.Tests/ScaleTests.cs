using BasslineGenerator.MusicTheory;
using BasslineGenerator.Models;
using Generator = BasslineGenerator.Generation.BasslineGenerator;

namespace BasslineGenerator.Tests;

public class ScaleTests
{
    [Theory]
    [InlineData("A", "Minor", new[] { 9, 11, 0, 2, 4, 5, 7 })]
    [InlineData("C", "Major", new[] { 0, 2, 4, 5, 7, 9, 11 })]
    [InlineData("D", "Dorian", new[] { 2, 4, 5, 7, 9, 11, 0 })]
    [InlineData("G", "Mixolydian", new[] { 7, 9, 11, 0, 2, 4, 5 })]
    [InlineData("C", "Lydian", new[] { 0, 2, 4, 6, 7, 9, 11 })]
    [InlineData("C", "Phrygian", new[] { 0, 1, 3, 5, 7, 8, 10 })]
    [InlineData("C", "Harmonic Minor", new[] { 0, 2, 3, 5, 7, 8, 11 })]
    [InlineData("C", "Melodic Minor", new[] { 0, 2, 3, 5, 7, 9, 11 })]
    [InlineData("C", "Major Pentatonic", new[] { 0, 2, 4, 7, 9 })]
    [InlineData("C", "Minor Pentatonic", new[] { 0, 3, 5, 7, 10 })]
    [InlineData("C", "Blues", new[] { 0, 3, 5, 6, 7, 10 })]
    public void GeneratesScale(string root, string type, int[] expected) =>
        Assert.Equal(expected, ScaleLibrary.GetScale(root, type));

    [Fact]
    public void SupportsDegreesAndAccidentals()
    {
        Assert.Equal(5, ScaleLibrary.Degree("Db", "Major", 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScaleLibrary.Degree("C", "Major", 0));
        Assert.Throws<ArgumentException>(() => ScaleLibrary.GetScale("H", "Major"));
        Assert.Throws<ArgumentException>(() => ScaleLibrary.GetScale("C", "unknown"));
        Assert.Equal(ScaleLibrary.GetScale("C", "Minor"), ScaleLibrary.GetScale("C", "Natural Minor"));
        Assert.Equal(9, ScaleLibrary.Degree("C", "Major Pentatonic", 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScaleLibrary.Degree("C", "Major Pentatonic", 6));
    }

    [Fact]
    public void EveryScaleConstrainsNotesAcrossRootsStylesAndProgressions()
    {
        var generator = new Generator();
        foreach (var scale in ScaleLibrary.All)
        foreach (var root in new[] { "C", "B", "Bb" })
        foreach (var style in Enum.GetValues<SongWorld>())
        foreach (var progression in ChordProgressionLibrary.All)
        foreach (var seed in new[] { 0, 17, 42 })
        {
            var settings = new GeneratorSettings
            {
                RootNote = root, ScaleType = scale.Name, Style = style, Progression = progression.Name,
                Bars = 4, Seed = seed, Movement = 100, Tension = 100, Mutation = 100
            };
            var notes = generator.Generate(settings).Notes;
            var allowed = ScaleLibrary.GetScale(root, scale.Name);
            Assert.NotEmpty(notes);
            Assert.All(notes, note =>
            {
                Assert.InRange(note.Pitch, 0, 127);
                Assert.Contains(note.Pitch % 12, allowed);
            });
        }
    }

    [Fact]
    public void ChangingScaleChangesSeededPitchSequences()
    {
        var generator = new Generator();
        var sequences = new[] { "Major", "Phrygian", "Lydian", "Blues", "Harmonic Minor" }
            .Select(scale => string.Join(",", generator.Generate(new GeneratorSettings
            {
                RootNote = "A", ScaleType = scale, Style = SongWorld.Acid, BPM = 140,
                Bars = 16, Seed = 42, Progression = "i - VI - III - VII"
            }).Notes.Select(n => n.Pitch))).ToArray();
        Assert.Equal(sequences.Length, sequences.Distinct().Count());
    }

    [Theory]
    [InlineData("Major", 4)]
    [InlineData("Phrygian", 1)]
    [InlineData("Lydian", 6)]
    [InlineData("Blues", 6)]
    [InlineData("Harmonic Minor", 11)]
    public void CharacteristicIntervalsAppearInGeneratedNotes(string scale, int interval)
    {
        var generator = new Generator();
        var intervals = Enumerable.Range(0, 8).SelectMany(seed => generator.Generate(new GeneratorSettings
        {
            RootNote = "A", ScaleType = scale, Style = SongWorld.Acid, BPM = 140,
            Bars = 16, Seed = seed, Progression = "i - VI - III - VII"
        }).Notes.Select(note => (note.Pitch - 9 + 12) % 12));
        Assert.Contains(interval, intervals);
    }
}
