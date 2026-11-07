namespace BasslineGenerator.Models;

/// <summary>A MIDI note positioned in absolute ticks.</summary>
public sealed record BassNote(int Pitch, long Start, long Length, int Velocity);
