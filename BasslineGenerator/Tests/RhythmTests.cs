using BasslineGenerator.Generation;
using BasslineGenerator.Models;

namespace BasslineGenerator.Tests;

public class RhythmTests
{
    [Theory]
    [InlineData(RhythmDensity.Sparse)]
    [InlineData(RhythmDensity.Medium)]
    [InlineData(RhythmDensity.Dense)]
    public void OnsetsAreOrderedAndStayWithinBar(RhythmDensity density)
    {
        var generator = new RhythmGenerator();
        var random = new Random(5);
        for (var i = 0; i < 100; i++)
        {
            var pattern = generator.Generate(density, random);
            Assert.Equal(0, pattern[0]);
            Assert.True(pattern.SequenceEqual(pattern.Distinct().Order()));
            Assert.All(pattern, x => Assert.InRange(x, 0, 15));
            Assert.All(generator.Generate(density, random, true), x => Assert.InRange(x, 0, 11));
        }
    }

    [Fact]
    public void DenseHasMoreOnsetsThanSparse()
    {
        var generator = new RhythmGenerator();
        Assert.True(generator.Generate(RhythmDensity.Dense, new Random(1)).Count >
            generator.Generate(RhythmDensity.Sparse, new Random(1)).Count);
    }
}
