namespace BasslineGenerator.Models;

/// <summary>A named, consecutive section in an arrangement.</summary>
public sealed record SongSection(string Name, SongWorld Style, int Bars);
