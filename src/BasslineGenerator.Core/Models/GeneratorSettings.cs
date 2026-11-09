namespace BasslineGenerator.Models;

/// <summary>Configuration for a single section or an explicit sequence of sections.</summary>
public sealed class GeneratorSettings
{
    public string RootNote { get; set; } = "A";
    public string ScaleType { get; set; } = "Minor";
    public int BPM { get; set; } = 140;
    public int Bars { get; set; } = 16;
    public SongWorld Style { get; set; } = SongWorld.Acid;
    /// <summary>Optional 0–100 overrides; null uses the current section's style preset.</summary>
    public int? Movement { get; set; }
    public int? Groove { get; set; }
    public int? Mutation { get; set; }
    public int? Tension { get; set; }
    public int? Repetition { get; set; }
    /// <summary>Zero-based MIDI channel (0–15).</summary>
    public int Channel { get; set; }
    public int Velocity { get; set; } = 100;
    /// <summary>Progression name, or null for a seeded random choice.</summary>
    public string? Progression { get; set; }
    public int? Seed { get; set; }
    /// <summary>Marks an arrangement generated from the built-in arrangement template.</summary>
    public bool IsArrangement { get; set; }
    public int VelocityVariation { get; set; }
    public int TimingVariation { get; set; }
    /// <summary>Delay applied to offbeat eighth notes, from 0 to 0.5 of an eighth note.</summary>
    public double Swing { get; set; }
    public bool AccentDownbeats { get; set; }
    /// <summary>Optional mode label (single, custom or arrangement) to report when replaying a package.</summary>
    public string? Mode { get; set; }
    public IReadOnlyList<SongSection>? Sections { get; set; }
}
