namespace BasslineGenerator.MusicTheory;

public static class ScaleLibrary
{
    private static readonly Dictionary<string, int[]> Scales = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Major"] = [0, 2, 4, 5, 7, 9, 11],
        ["Minor"] = [0, 2, 3, 5, 7, 8, 10],
        ["Natural Minor"] = [0, 2, 3, 5, 7, 8, 10],
        ["Dorian"] = [0, 2, 3, 5, 7, 9, 10],
        ["Mixolydian"] = [0, 2, 4, 5, 7, 9, 10]
    };

    /// <summary>Returns the pitch classes of the seven scale degrees, starting at the tonic.</summary>
    public static IReadOnlyList<int> GetScale(string root, string type)
    {
        var tonic = NoteUtilities.PitchClass(root);
        if (type is null || !Scales.TryGetValue(type.Trim(), out var intervals))
            throw new ArgumentException("Unknown scale type.", nameof(type));
        return intervals.Select(i => (tonic + i) % 12).ToArray();
    }

    public static int Degree(string root, string type, int degree)
    {
        if (degree is < 1 or > 7) throw new ArgumentOutOfRangeException(nameof(degree));
        return GetScale(root, type)[degree - 1];
    }
}
