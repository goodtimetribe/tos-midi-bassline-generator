using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
namespace BasslineGenerator.Presets;
public sealed class PopPreset : IPreset
{
    public SongWorld World => SongWorld.Pop;
    public MusicalDimensions Dimensions => new(40, 50, 25, 20, 80);
    public RhythmDensity Density => RhythmDensity.Medium;
    public double MutationChance => 0.08;
    public double OctaveChance => 0.04;
    public double PassingChance => 0.01;
    public double Gate => 0.85;
}
