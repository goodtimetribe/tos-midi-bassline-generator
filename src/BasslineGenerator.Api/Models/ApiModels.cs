using System.ComponentModel.DataAnnotations;

namespace BasslineGenerator.Api.Models;

public sealed class GenerateRequest
{
    /// <summary>Natural root note. Values are case-sensitive.</summary>
    /// <example>A</example>
    [Required, RegularExpression("^[A-G]$(?![\\s\\S])", ErrorMessage = "Root note must be A, B, C, D, E, F, or G.")]
    public string? RootNote { get; set; }

    /// <summary>Scale type: Major, Minor, Dorian, or Mixolydian. Values are case-sensitive.</summary>
    /// <example>Minor</example>
    [Required, RegularExpression("^(Major|Minor|Dorian|Mixolydian)$(?![\\s\\S])", ErrorMessage = "Scale type must be Major, Minor, Dorian, or Mixolydian.")]
    public string? ScaleType { get; set; }

    /// <summary>
    /// Case-sensitive style returned by the presets endpoint. Dreamy is sparse,
    /// Pop is moderate, Acid adds chromatic and octave movement, and Rave is dense.
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
}

/// <summary>A safe, human-readable error response.</summary>
/// <param name="Error">Description of the error without the supplied input.</param>
public sealed record ErrorResponse(string Error);

/// <summary>API health and version information.</summary>
/// <param name="Status">Current health status.</param>
/// <param name="Version">API version.</param>
public sealed record HealthResponse(string Status, string Version);
