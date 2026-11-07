using BasslineGenerator.MusicTheory;

namespace BasslineGenerator.Tests;

public class ChordTests
{
    [Fact]
    public void ProgressionsPreserveWrittenRootsAndQualities()
    {
        Assert.Equal(6, ChordProgressionLibrary.All.Count);
        var progression = ChordProgressionLibrary.Select("i - V - VI - IV", "Minor", new Random(1));
        Assert.Equal(new[] { 0, 7, 8, 5 }, progression.Chords.Select(c => c.RootOffset));
        Assert.Equal(new[] { 3, 4, 4, 4 }, progression.Chords.Select(c => c.Third));
        Assert.Throws<ArgumentException>(() => ChordProgressionLibrary.Select("unknown", "Minor", new Random()));
    }

    [Fact]
    public void RandomSelectionIsSeeded()
    {
        Assert.Equal(ChordProgressionLibrary.Select(null, "Minor", new Random(42)).Name,
            ChordProgressionLibrary.Select(null, "Minor", new Random(42)).Name);
    }
}
