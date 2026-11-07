using BasslineGenerator.MusicTheory;

namespace BasslineGenerator.Tests;

public class ScaleTests
{
    [Theory]
    [InlineData("A", "Minor", new[] { 9, 11, 0, 2, 4, 5, 7 })]
    [InlineData("C", "Major", new[] { 0, 2, 4, 5, 7, 9, 11 })]
    [InlineData("D", "Dorian", new[] { 2, 4, 5, 7, 9, 11, 0 })]
    [InlineData("G", "Mixolydian", new[] { 7, 9, 11, 0, 2, 4, 5 })]
    public void GeneratesScale(string root, string type, int[] expected) =>
        Assert.Equal(expected, ScaleLibrary.GetScale(root, type));

    [Fact]
    public void SupportsDegreesAndAccidentals()
    {
        Assert.Equal(5, ScaleLibrary.Degree("Db", "Major", 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScaleLibrary.Degree("C", "Major", 0));
        Assert.Throws<ArgumentException>(() => ScaleLibrary.GetScale("H", "Major"));
    }
}
