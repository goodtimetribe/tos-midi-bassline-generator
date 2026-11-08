using System.ComponentModel.DataAnnotations;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;

namespace BasslineGenerator.Api.Models;

public sealed class GenerateRequest : IValidatableObject
{
    /// <summary>Root note, including sharps and flats such as C# or Bb. Note names are case-insensitive.</summary>
    /// <example>A</example>
    [Required, RootNote]
    public string? RootNote { get; set; }

    /// <summary>Scale type, case-insensitive; Natural Minor is an alias for Minor.</summary>
    /// <example>Phrygian</example>
    [Required, ScaleType]
    public string? ScaleType { get; set; }

    /// <summary>
    /// Style returned by the presets endpoint, case-insensitive. Dreamy is sparse,
    /// Pop is moderate, Acid adds scale approaches and octave movement, and Rave is dense.
    /// Required for all requests, but used only in single-section mode.
    /// </summary>
    /// <example>Acid</example>
    [Required]
    public string? Style { get; set; }

    /// <summary>Tempo in beats per minute, inclusive.</summary>
    /// <example>140</example>
    [Required, Range(30, 300)]
    public int? Bpm { get; set; }

    /// <summary>Length in bars (1–512); in arrangement mode this is per section. Ignored for custom sections.</summary>
    /// <example>16</example>
    [Required, Range(1, 512)]
    public int? Bars { get; set; }

    /// <summary>Case-sensitive generation mode. Omit or set to null for a single section; arrangement uses six preset sections.</summary>
    /// <example>single</example>
    [GenerationMode]
    public string? Mode { get; set; }

    /// <summary>Case-sensitive progression name. Omit or set to null for a random progression chosen for the scale.</summary>
    /// <example>i - VI - III - VII</example>
    [Progression]
    public string? Progression { get; set; }

    /// <summary>Optional signed 32-bit seed. The same seed and settings reproduce the same MIDI with the same engine version.</summary>
    /// <example>12345</example>
    public int? Seed { get; set; }

    /// <summary>Zero-based MIDI channel (0–15). Defaults to 0.</summary>
    /// <example>0</example>
    [Range(0, 15)]
    public int? Channel { get; set; }

    /// <summary>Base MIDI velocity (1–127). Defaults to 100.</summary>
    /// <example>100</example>
    [Range(1, 127)]
    public int? Velocity { get; set; }

    /// <summary>Random velocity variation (0–127). Defaults to 0.</summary>
    /// <example>10</example>
    [Range(0, 127)]
    public int? VelocityVariation { get; set; }

    /// <summary>Random timing variation in MIDI ticks (0–30). Defaults to 0.</summary>
    /// <example>5</example>
    [Range(0, 30)]
    public int? TimingVariation { get; set; }

    /// <summary>Offbeat delay from 0 to 0.5 of an eighth note. Defaults to 0.</summary>
    /// <example>0.25</example>
    [Range(0.0, 0.5)]
    public double? Swing { get; set; }

    /// <summary>Accent downbeats. Defaults to false.</summary>
    /// <example>true</example>
    public bool? AccentDownbeats { get; set; }

    /// <summary>Optional custom sequence of named sections; overrides single mode and cannot be combined with arrangement mode. Each section is 1–512 bars and the total is at most 4096 bars.</summary>
    [SectionSequence]
    public List<SectionRequest?>? Sections { get; set; }

    /// <summary>Movement from root-focused (0) to melodic scale traversal and wider intervals (100). Omit to use the style preset.</summary>
    /// <example>70</example>
    [Range(0, 100)]
    public int? Movement { get; set; }

    /// <summary>Groove from straight downbeats (0) to syncopation, offbeat accents and rests (100). Omit to use the style preset.</summary>
    /// <example>80</example>
    [Range(0, 100)]
    public int? Groove { get; set; }

    /// <summary>Mutation from consistent phrases (0) to evolving motifs and section variations (100). Omit to use the style preset.</summary>
    /// <example>35</example>
    [Range(0, 100)]
    public int? Mutation { get; set; }

    /// <summary>Tension from consonant chord tones (0) to unstable scale tones and approaches (100). Omit to use the style preset.</summary>
    /// <example>65</example>
    [Range(0, 100)]
    public int? Tension { get; set; }

    /// <summary>Repetition from changing phrases (0) to recurring motifs and rhythms (100). Omit to use the style preset.</summary>
    /// <example>85</example>
    [Range(0, 100)]
    public int? Repetition { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Mode == GenerationModes.Arrangement && Sections is not null)
            yield return new ValidationResult("Custom sections cannot be combined with arrangement mode.", [nameof(Sections)]);
    }
}

public sealed class SectionRequest
{
    /// <summary>Section name.</summary>
    /// <example>Intro</example>
    public string? Name { get; set; }
    /// <summary>Preset name, case-insensitive.</summary>
    /// <example>Dreamy</example>
    public string? Style { get; set; }
    /// <summary>Section length in bars (1–512).</summary>
    /// <example>4</example>
    public int Bars { get; set; }
}

public sealed class SectionSequenceAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || value is List<SectionRequest?> sections
        && sections.Count > 0
        && sections.All(section => section is not null && section.Name is not null
            && section.Style is not null && Enum.GetNames<SongWorld>().Contains(section.Style.Trim(), StringComparer.OrdinalIgnoreCase)
            && section.Bars is >= 1 and <= 512)
        && sections.Sum(section => (long)section!.Bars) <= 4096;
}

public sealed class RootNoteAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null) return true;
        if (value is not string note) return false;
        try { NoteUtilities.PitchClass(note); return true; }
        catch (ArgumentException) { return false; }
    }
}

public sealed class ScaleTypeAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null ||
        value is string name && (name.Trim().Equals("Natural Minor", StringComparison.OrdinalIgnoreCase)
            || ScaleLibrary.All.Any(scale => scale.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)));
}

public static class GenerationModes
{
    public const string Single = "single";
    public const string Arrangement = "arrangement";
    public static IReadOnlyList<string> All { get; } = [Single, Arrangement];
}

public sealed class GenerationModeAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null ||
        value is string mode && GenerationModes.All.Contains(mode, StringComparer.Ordinal);
}

public sealed class ProgressionAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null ||
        value is string name && ChordProgressionLibrary.All.Any(p => p.Name.Equals(name, StringComparison.Ordinal));
}

/// <summary>A safe, human-readable error response.</summary>
/// <param name="Error">Description of the error without the supplied input.</param>
public sealed record ErrorResponse(string Error);

/// <summary>API health and version information.</summary>
/// <param name="Status">Current health status.</param>
/// <param name="Version">API version.</param>
public sealed record HealthResponse(string Status, string Version);
