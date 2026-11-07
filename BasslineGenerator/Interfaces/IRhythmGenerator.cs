using BasslineGenerator.Models;

namespace BasslineGenerator.Interfaces;

public interface IRhythmGenerator
{
    IReadOnlyList<int> Generate(RhythmDensity density, Random random, bool phraseEnding = false);
}
