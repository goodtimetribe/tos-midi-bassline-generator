using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
namespace BasslineGenerator.Presets;
public sealed class AcidPreset : IPreset
{
    public SongWorld World => SongWorld.Acid;
    public MusicalDimensions Dimensions => new(75, 70, 50, 80, 85);
    public RhythmDensity Density => RhythmDensity.Dense;
    public double MutationChance => 0.22;
    public double OctaveChance => 0.18;
    public double PassingChance => 0.10;
    public double Gate => 0.72;
}
