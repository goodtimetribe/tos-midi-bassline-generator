using BasslineGenerator.Models;
namespace BasslineGenerator.Generation;

public sealed class MotifGenerator
{
    public Motif Generate(Random random) => PatternLibrary.Patterns[random.Next(PatternLibrary.Patterns.Count)];
}
