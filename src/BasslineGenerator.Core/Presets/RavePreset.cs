using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
namespace BasslineGenerator.Presets;
public sealed class RavePreset : IPreset
{
    public SongWorld World => SongWorld.Rave;
    public RhythmDensity Density => RhythmDensity.Dense;
    public double MutationChance => 0.08;
    public double OctaveChance => 0.14;
    public double PassingChance => 0.02;
    public double Gate => 0.80;
}
