using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
namespace BasslineGenerator.Presets;
public sealed class DreamyPreset : IPreset
{
    public SongWorld World => SongWorld.Dreamy;
    public RhythmDensity Density => RhythmDensity.Sparse;
    public double MutationChance => 0.02;
    public double OctaveChance => 0.01;
    public double PassingChance => 0;
    public double Gate => 0.94;
}
