using System.ComponentModel.DataAnnotations;
using BasslineGenerator.MusicTheory;

namespace BasslineGenerator.Api.Models;

public sealed class GenerateRequest
{
    /// <summary>Natural root note. Values are case-sensitive.</summary>
    /// <example>A</example>
    [Required, RegularExpression("^[A-G]$(?![\\s\\S])", ErrorMessage = "Root note must be A, B, C, D, E, F, or G.")]
    public string? RootNote { get; set; }

    /// <summary>Scale type. Values are case-sensitive; see the listed modes and their descriptions.</summary>
    /// <example>Phrygian</example>
    [Required, ScaleType]
    public string? ScaleType { get; set; }

    /// <summary>
    /// Case-sensitive style returned by the presets endpoint. Dreamy is sparse,
    /// Pop is moderate, Acid adds scale approaches and octave movement, and Rave is dense.
    /// </summary>
    /// <example>Acid</example>
    [Required]
    public string? Style { get; set; }

    /// <summary>Tempo in beats per minute, inclusive.</summary>
    /// <example>140</example>
    [Required, Range(40, 250, ErrorMessage = "BPM must be between 40 and 250.")]
    public int? Bpm { get; set; }

    /// <summary>Length in bars, inclusive.</summary>
    /// <example>16</example>
    [Required, Range(1, 256, ErrorMessage = "Bars must be between 1 and 256.")]
    public int? Bars { get; set; }

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
}

public sealed class ScaleTypeAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null ||
        value is string name && ScaleLibrary.All.Any(scale => scale.Name.Equals(name, StringComparison.Ordinal));
}

/// <summary>A safe, human-readable error response.</summary>
/// <param name="Error">Description of the error without the supplied input.</param>
public sealed record ErrorResponse(string Error);

/// <summary>API health and version information.</summary>
/// <param name="Status">Current health status.</param>
/// <param name="Version">API version.</param>
public sealed record HealthResponse(string Status, string Version);
