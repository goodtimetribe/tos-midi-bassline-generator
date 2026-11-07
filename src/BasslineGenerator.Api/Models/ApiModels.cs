using System.ComponentModel.DataAnnotations;

namespace BasslineGenerator.Api.Models;

public sealed class GenerateRequest
{
    /// <summary>Natural root note: A, B, C, D, E, F, or G.</summary>
    [Required, RegularExpression("^[A-G]$(?![\\s\\S])", ErrorMessage = "Root note must be A, B, C, D, E, F, or G.")]
    public string? RootNote { get; set; }

    /// <summary>Major, Minor, Dorian, or Mixolydian.</summary>
    [Required, RegularExpression("^(Major|Minor|Dorian|Mixolydian)$(?![\\s\\S])", ErrorMessage = "Scale type must be Major, Minor, Dorian, or Mixolydian.")]
    public string? ScaleType { get; set; }

    /// <summary>A style returned by the presets endpoint.</summary>
    [Required]
    public string? Style { get; set; }

    /// <summary>Tempo between 40 and 250 BPM.</summary>
    [Required, Range(40, 250, ErrorMessage = "BPM must be between 40 and 250.")]
    public int? Bpm { get; set; }

    /// <summary>Length between 1 and 256 bars.</summary>
    [Required, Range(1, 256, ErrorMessage = "Bars must be between 1 and 256.")]
    public int? Bars { get; set; }
}

public sealed record ErrorResponse(string Error);
public sealed record HealthResponse(string Status, string Version);
