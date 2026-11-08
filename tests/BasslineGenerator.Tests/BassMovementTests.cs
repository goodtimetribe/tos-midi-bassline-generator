using BasslineGenerator.Generation;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;

namespace BasslineGenerator.Tests;

public class BassMovementTests
{
    [Fact]
    public void WeightedChoicesFavorRoot()
    {
        var generator = new BassMovementGenerator();
        var random = new Random(7);
        var choices = Enumerable.Range(0, 20000).Select(_ => generator.Choose(random)).ToArray();
        Assert.InRange(choices.Count(c => c == BassMovement.Root), 9500, 10500);
        Assert.InRange(choices.Count(c => c == BassMovement.Fifth), 3700, 4300);
        Assert.InRange(choices.Count(c => c == BassMovement.Octave), 2700, 3300);
        Assert.InRange(choices.Count(c => c == BassMovement.Third), 1700, 2300);
        Assert.InRange(choices.Count(c => c == BassMovement.PassingTone), 800, 1200);
    }

    [Fact]
    public void UsesChordQualityAndScaleForPassingTone()
    {
        var generator = new BassMovementGenerator();
        var scale = ScaleLibrary.GetScale("A", "Minor");
        var chord = new Chord(0, 3);
        Assert.Equal(48, generator.Pitch(BassMovement.Third, 45, chord, scale, 45, false));
        Assert.Equal(43, generator.Pitch(BassMovement.PassingTone, 45, chord, scale, 45, false));
        Assert.Equal(41, generator.Pitch(BassMovement.PassingTone, 45, chord, scale, 45, true));
    }
}
