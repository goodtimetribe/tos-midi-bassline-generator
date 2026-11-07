using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
namespace BasslineGenerator.Generation;

/// <summary>Weighted, repeating one-bar onset patterns on a sixteenth-note grid.</summary>
public sealed class RhythmGenerator : IRhythmGenerator
{
    public IReadOnlyList<int> Generate(RhythmDensity density, Random random, bool phraseEnding = false)
    {
        int[][] patterns = density switch
        {
            RhythmDensity.Sparse => [[0, 8], [0, 8], [0], [0, 4, 8]],
            RhythmDensity.Medium => [[0, 4, 8, 12], [0, 4, 8, 12], [0, 3, 6, 8, 12, 14], [0, 2, 4, 6, 8, 10, 12, 14]],
            RhythmDensity.Dense => [[0, 2, 4, 6, 8, 10, 12, 14], [0, 2, 3, 6, 8, 10, 11, 14], [0, 1, 2, 4, 6, 8, 9, 10, 12, 14], [0, 2, 4, 6, 8, 10, 12, 14]],
            _ => throw new ArgumentOutOfRangeException(nameof(density))
        };
        var selected = patterns[random.Next(patterns.Length)];
        return phraseEnding ? selected.Where(x => x < 12).ToArray() : selected.ToArray();
    }
}
