namespace BasslineGenerator.MusicTheory;

public sealed record ScaleDefinition(string Name, string Mood, IReadOnlyList<int> Intervals);

public static class ScaleLibrary
{
    private static ScaleDefinition Define(string name, string mood, params int[] intervals) =>
        new(name, mood, Array.AsReadOnly(intervals));

    public static IReadOnlyList<ScaleDefinition> All { get; } = Array.AsReadOnly<ScaleDefinition>(
    [
        Define("Major", "Bright and uplifting.", 0, 2, 4, 5, 7, 9, 11),
        Define("Minor", "Dark and reflective; natural minor.", 0, 2, 3, 5, 7, 8, 10),
        Define("Dorian", "Minor with a bright, soulful color.", 0, 2, 3, 5, 7, 9, 10),
        Define("Mixolydian", "Warm, bluesy and relaxed.", 0, 2, 4, 5, 7, 9, 10),
        Define("Lydian", "Bright, floating, cinematic.", 0, 2, 4, 6, 7, 9, 11),
        Define("Phrygian", "Dark, tense, exotic.", 0, 1, 3, 5, 7, 8, 10),
        Define("Harmonic Minor", "Dramatic, epic, fantasy-inspired.", 0, 2, 3, 5, 7, 8, 11),
        Define("Melodic Minor", "Sophisticated, jazzy, modern.", 0, 2, 3, 5, 7, 9, 11),
        Define("Major Pentatonic", "Simple, open, uplifting.", 0, 2, 4, 7, 9),
        Define("Minor Pentatonic", "Rock, blues, synthwave-friendly.", 0, 3, 5, 7, 10),
        Define("Blues", "Expressive, gritty, traditional blues vocabulary.", 0, 3, 5, 6, 7, 10)
    ]);

    /// <summary>Returns the pitch classes of the scale degrees, starting at the tonic.</summary>
    public static IReadOnlyList<int> GetScale(string root, string type)
    {
        var tonic = NoteUtilities.PitchClass(root);
        var name = type?.Trim();
        if (string.Equals(name, "Natural Minor", StringComparison.OrdinalIgnoreCase)) name = "Minor";
        var definition = All.FirstOrDefault(scale => scale.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Unknown scale type.", nameof(type));
        return definition.Intervals.Select(i => (tonic + i) % 12).ToArray();
    }

    public static int Degree(string root, string type, int degree)
    {
        var scale = GetScale(root, type);
        if (degree < 1 || degree > scale.Count) throw new ArgumentOutOfRangeException(nameof(degree));
        return scale[degree - 1];
    }
}
