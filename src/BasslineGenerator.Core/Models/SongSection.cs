namespace BasslineGenerator.Models;

/// <summary>A named, consecutive section in an arrangement.</summary>
public sealed record SongSection(string Name, SongWorld Style, int Bars, MusicalDimensions? Behavior = null)
{
    /// <summary>The section's explicit behavior if present; otherwise its style preset with settings overrides.</summary>
    public MusicalDimensions Resolve(Interfaces.IPreset preset, GeneratorSettings settings) =>
        Behavior ?? preset.Dimensions.WithOverrides(settings);
}
