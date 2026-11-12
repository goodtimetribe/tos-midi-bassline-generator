using BasslineGenerator.Generation;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;

namespace BasslineGenerator.Tests;

public class ChordGeneratorTests
{
    [Fact]
    public void GeneratesRootPositionTriadsWithDefaultVelocity()
    {
        var notes = new ChordGenerator().Generate("A",
            ChordProgressionLibrary.Find("i - V - VI - IV")!,
            [new SongSection("Section", SongWorld.Dreamy, 1)]);

        Assert.Equal(
        [
            new BassNote(57, 0, MusicConstants.TicksPerBar, 90),
            new BassNote(60, 0, MusicConstants.TicksPerBar, 90),
            new BassNote(64, 0, MusicConstants.TicksPerBar, 90)
        ], notes);
        Assert.All(notes, note => Assert.Equal(ChordGenerator.DefaultVelocity, note.Velocity));
    }

    [Fact]
    public void PreservesWrittenMajorAndMinorThirdsWithoutScaleNormalization()
    {
        var notes = new ChordGenerator().Generate("A",
            ChordProgressionLibrary.Find("i - V - VI - IV")!,
            [new SongSection("Section", SongWorld.Dreamy, 2)]);

        var minor = notes.Where(note => note.Start == 0).Select(note => note.Pitch).ToArray();
        var majorDominant = notes.Where(note => note.Start == MusicConstants.TicksPerBar).Select(note => note.Pitch).ToArray();

        Assert.Equal([57, 60, 64], minor);
        Assert.Equal([64, 68, 71], majorDominant);
        Assert.DoesNotContain(68 % 12, ScaleLibrary.GetScale("A", "Minor"));
    }

    [Fact]
    public void UsesAbsoluteBarIndexesAcrossSections()
    {
        var notes = new ChordGenerator().Generate("A",
            ChordProgressionLibrary.Find("Synthwave")!,
            [
                new SongSection("Intro", SongWorld.Dreamy, 3),
                new SongSection("Drop", SongWorld.Acid, 2)
            ]);

        var roots = notes.Where((_, index) => index % 3 == 0).Select(note => note.Pitch).ToArray();

        Assert.Equal([57, 65, 60, 67, 57], roots);
        Assert.Equal(15, notes.Count);
        for (var bar = 0; bar < 5; bar++)
        {
            var chord = notes.Where(note => note.Start == bar * MusicConstants.TicksPerBar).ToArray();
            Assert.Equal(3, chord.Length);
            Assert.All(chord, note => Assert.Equal(MusicConstants.TicksPerBar, note.Length));
        }
    }
}
