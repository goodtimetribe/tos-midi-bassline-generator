namespace BasslineGenerator.Models;

/// <summary>A recurring sequence of chord-relative movements.</summary>
public sealed record Motif(string Name, IReadOnlyList<BassMovement> Movements);

public enum BassMovement { Root, Fifth, Octave, Third, Seventh, PassingTone }
