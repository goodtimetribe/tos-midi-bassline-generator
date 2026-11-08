using BasslineGenerator.MusicTheory;

namespace BasslineGenerator.Tests;

public class ChordTests
{
    [Fact]
    public void ProgressionsPreserveWrittenRootsAndQualities()
    {
        var progression = ChordProgressionLibrary.Select("i - V - VI - IV", "Minor", new Random(1));
        Assert.Equal(new[] { 0, 7, 8, 5 }, progression.Chords.Select(c => c.RootOffset));
        Assert.Equal(new[] { 3, 4, 4, 4 }, progression.Chords.Select(c => c.Third));
        Assert.Throws<ArgumentException>(() => ChordProgressionLibrary.Select("unknown", "Minor", new Random()));
    }

    [Fact]
    public void CuratedProgressionsAreRegisteredWithCorrectChordsAndMetadata()
    {
        var expected = new (string Name, string Sequence, int[] Roots, int[] Thirds)[]
        {
            ("Pop Anthem", "I - V - vi - IV", [0,7,9,5], [4,4,3,4]),
            ("Classic Pop", "I - vi - IV - V", [0,9,5,7], [4,3,4,4]),
            ("Heroic", "I - IV - V - I", [0,5,7,0], [4,4,4,4]),
            ("Adventure", "I - V - IV - V", [0,7,5,7], [4,4,4,4]),
            ("Dream Pop", "I - III - IV - V", [0,4,5,7], [4,4,4,4]),
            ("Synthwave", "i - VI - III - VII", [0,8,3,10], [3,4,4,4]),
            ("Dark Journey", "i - VII - VI - VII", [0,10,8,10], [3,4,4,4]),
            ("Melancholy", "i - III - VII - VI", [0,3,10,8], [3,4,4,4]),
            ("Future Retro", "i - v - VI - VII", [0,7,8,10], [3,3,4,4]),
            ("Dreamwave", "i - VI - iv - VII", [0,8,5,10], [3,4,3,4]),
            ("Classic Rave", "i - VII - VI - VII", [0,10,8,10], [3,4,4,4]),
            ("Trance Lift", "i - VI - III - VII", [0,8,3,10], [3,4,4,4]),
            ("Progressive", "i - iv - VI - VII", [0,5,8,10], [3,3,4,4]),
            ("Techno Minimal", "i - i - VII - i", [0,0,10,0], [3,3,4,3]),
            ("Acid Driver", "i - iv - i - v", [0,5,0,7], [3,3,3,3]),
            ("Epic Fantasy", "i - VI - VII - i", [0,8,10,0], [3,4,4,3]),
            ("Ancient Ruins", "i - bII - VII - i", [0,1,10,0], [3,4,4,3]),
            ("Boss Battle", "i - iv - VII - III", [0,5,10,3], [3,3,4,4]),
            ("Machine World", "i - III - iv - VII", [0,3,5,10], [3,4,3,4]),
            ("Finale", "i - VI - iv - V", [0,8,5,7], [3,4,3,4])
        };
        Assert.Equal(23, ChordProgressionLibrary.All.Count);
        Assert.Equal(23, ChordProgressionLibrary.All.Select(p => p.Id).Distinct().Count());
        Assert.Equal(23, ChordProgressionLibrary.All.Select(p => p.DisplayName).Distinct().Count());
        foreach (var item in expected)
        {
            var p = Assert.Single(ChordProgressionLibrary.All, p => p.DisplayName == item.Name);
            Assert.Same(p, ChordProgressionLibrary.Find(p.Id));
            Assert.Same(p, ChordProgressionLibrary.Find(p.DisplayName));
            Assert.True(new BasslineGenerator.Api.Models.ProgressionAttribute().IsValid(p.Id));
            Assert.True(new BasslineGenerator.Api.Models.ProgressionAttribute().IsValid(p.DisplayName));
            Assert.Equal(item.Sequence, string.Join(" - ", p.ChordSequence));
            Assert.Equal(item.Roots, p.Chords.Select(c => c.RootOffset));
            Assert.Equal(item.Thirds, p.Chords.Select(c => c.Third));
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
            Assert.False(string.IsNullOrWhiteSpace(p.SuggestedUse));
            Assert.False(string.IsNullOrWhiteSpace(p.Mood));
        }
    }

    [Theory]
    [InlineData("i - VI - III - VII", "synthwave")]
    [InlineData("i - iv - v", "minor-classic")]
    [InlineData("I - V - vi - IV", "pop-anthem")]
    [InlineData("i - bVII - bVI - bVII", "dark-journey")]
    [InlineData("vi - IV - I - V", "relative-major")]
    [InlineData("i - V - VI - IV", "minor-dramatic")]
    public void LegacySequencesAndNumericOrderArePreserved(string name, string id)
    {
        Assert.Equal(id, ChordProgressionLibrary.Find(name)!.Id);
        Assert.Equal(name, ChordProgressionLibrary.Find(name)!.Name);
        Assert.Equal(id, ChordProgressionLibrary.All[Array.IndexOf(
            new[] { "synthwave", "minor-classic", "pop-anthem", "dark-journey", "relative-major", "minor-dramatic" }, id)].Id);
    }

    [Fact]
    public void RandomSelectionIsSeeded()
    {
        Assert.Equal(ChordProgressionLibrary.Select(null, "Minor", new Random(42)).Name,
            ChordProgressionLibrary.Select(null, "Minor", new Random(42)).Name);
    }
}
