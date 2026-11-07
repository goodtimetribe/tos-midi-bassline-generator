namespace BasslineGenerator.MusicTheory;

/// <summary>A chord root in semitones above the tonic, and its third in semitones.</summary>
public sealed record Chord(int RootOffset, int Third);
public sealed record ChordProgression(string Name, IReadOnlyList<Chord> Chords);

public static class ChordProgressionLibrary
{
    private static readonly ChordProgression[] Progressions =
    [
        new("i - VI - III - VII", [new(0, 3), new(8, 4), new(3, 4), new(10, 4)]),
        new("i - iv - v", [new(0, 3), new(5, 3), new(7, 3)]),
        new("I - V - vi - IV", [new(0, 4), new(7, 4), new(9, 3), new(5, 4)]),
        new("i - bVII - bVI - bVII", [new(0, 3), new(10, 4), new(8, 4), new(10, 4)]),
        new("vi - IV - I - V", [new(9, 3), new(5, 4), new(0, 4), new(7, 4)]),
        new("i - V - VI - IV", [new(0, 3), new(7, 4), new(8, 4), new(5, 4)])
    ];

    public static IReadOnlyList<ChordProgression> All => Progressions;

    public static ChordProgression Select(string? name, string scaleType, Random random)
    {
        if (name is not null)
            return Progressions.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("Unknown progression.", nameof(name));
        // The major and minor libraries retain their written chord qualities.
        var major = scaleType.Equals("Major", StringComparison.OrdinalIgnoreCase)
            || scaleType.Equals("Mixolydian", StringComparison.OrdinalIgnoreCase);
        var candidates = Progressions.Where(p => major ? p.Name.StartsWith("I") || p.Name.StartsWith("vi") : p.Name.StartsWith("i")).ToArray();
        return candidates[random.Next(candidates.Length)];
    }
}
