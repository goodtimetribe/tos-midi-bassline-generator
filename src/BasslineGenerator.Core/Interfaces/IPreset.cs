using BasslineGenerator.Models;

namespace BasslineGenerator.Interfaces;

public interface IPreset
{
    SongWorld World { get; }
    MusicalDimensions Dimensions { get; }
    RhythmDensity Density { get; }
    double MutationChance { get; }
    double OctaveChance { get; }
    double PassingChance { get; }
    double Gate { get; }
}
