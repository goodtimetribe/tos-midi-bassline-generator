using BasslineGenerator.Generation;
using BasslineGenerator.Models;

namespace BasslineGenerator.Tests;

public class MotifTests
{
    [Fact]
    public void SelectsReusableRootedMotifDeterministically()
    {
        Assert.Equal(5, PatternLibrary.Patterns.Count);
        var generator = new MotifGenerator();
        var motif = generator.Generate(new Random(5));
        Assert.Same(motif, generator.Generate(new Random(5)));
        Assert.Equal(BassMovement.Root, motif.Movements[0]);
        Assert.Equal(4, motif.Movements.Count);
    }
}
