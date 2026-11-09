namespace BasslineGenerator.MusicTheory;

public static class NoteUtilities
{
    private static readonly Dictionary<string, int> PitchClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = 0, ["C#"] = 1, ["Db"] = 1, ["D"] = 2, ["D#"] = 3, ["Eb"] = 3,
        ["E"] = 4, ["F"] = 5, ["F#"] = 6, ["Gb"] = 6, ["G"] = 7,
        ["G#"] = 8, ["Ab"] = 8, ["A"] = 9, ["A#"] = 10, ["Bb"] = 10, ["B"] = 11
    };

    private static readonly string[] Names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    /// <summary>Scientific pitch name for a MIDI note, where 60 is C4.</summary>
    public static string NoteName(int midiNote) => $"{Names[midiNote % 12]}{midiNote / 12 - 1}";

    public static int PitchClass(string note) =>
        note is not null && PitchClasses.TryGetValue(note.Trim(), out var value)
            ? value : throw new ArgumentException("Invalid note name.", nameof(note));
}
