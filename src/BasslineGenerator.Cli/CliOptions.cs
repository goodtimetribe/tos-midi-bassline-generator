using BasslineGenerator.MusicTheory;

namespace BasslineGenerator;

internal sealed class CliOptions
{
    public bool Help { get; private set; }
    public int? Mode { get; private set; }
    public int? Style { get; private set; }
    public string? Key { get; private set; }
    public int? Scale { get; private set; }
    public int? Bpm { get; private set; }
    public int? Bars { get; private set; }
    public int? Progression { get; private set; }
    public bool? Confirm { get; private set; }

    public static CliOptions Parse(string[] args)
    {
        var result = new CliOptions();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var parts = args[i].Split('=', 2);
            var name = parts[0].ToLowerInvariant();
            if (name == "-h") name = "--help";
            if (name == "--yes") name = "--confirm";
            if (!seen.Add(name)) throw new ArgumentException($"Option {name} was specified more than once.");

            if (name == "--help")
            {
                if (parts.Length != 1) throw new ArgumentException("--help does not take a value.");
                result.Help = true;
                continue;
            }
            if (parts[0].Equals("--yes", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 1) throw new ArgumentException("--yes does not take a value.");
                result.Confirm = true;
                continue;
            }
            if (name is not ("--mode" or "--style" or "--key" or "--scale" or "--bpm" or "--bars" or "--progression" or "--confirm"))
                throw new ArgumentException($"Unknown option: {parts[0]}.");
            var value = parts.Length == 2 ? parts[1] : ++i < args.Length ? args[i] : "";
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Option {name} requires a value.");
            value = value.Trim();

            switch (name)
            {
                case "--mode": result.Mode = Choice(name, value, ["single", "arrangement"]); break;
                case "--style": result.Style = Choice(name, value, ["dreamy", "pop", "acid", "rave"]); break;
                case "--scale": result.Scale = Choice(name, value, ScaleLibrary.All.Select(s => s.Name).ToArray()); break;
                case "--key":
                    try { NoteUtilities.PitchClass(value); }
                    catch (ArgumentException) { throw new ArgumentException($"Invalid key: {value}."); }
                    result.Key = value;
                    break;
                case "--bpm": result.Bpm = Number(name, value, 4, 1000); break;
                case "--bars": result.Bars = Number(name, value, 1, 4096); break;
                case "--progression":
                    if (value.Equals("random", StringComparison.OrdinalIgnoreCase)) result.Progression = 0;
                    else if (int.TryParse(value, out _)) result.Progression = Number(name, value, 0, ChordProgressionLibrary.All.Count);
                    else
                    {
                        var index = ChordProgressionLibrary.All.ToList().FindIndex(p => p.Name.Equals(value, StringComparison.Ordinal));
                        if (index < 0) throw new ArgumentException($"Unknown progression: {value}.");
                        result.Progression = index + 1;
                    }
                    break;
                case "--confirm":
                    result.Confirm = value.ToLowerInvariant() switch
                    {
                        "y" or "yes" => true,
                        "n" or "no" => false,
                        _ => throw new ArgumentException("--confirm must be yes or no.")
                    };
                    break;
            }
        }
        return result;
    }

    private static int Choice(string name, string value, string[] choices)
    {
        if (int.TryParse(value, out _)) return Number(name, value, 1, choices.Length);
        var index = Array.FindIndex(choices, choice => choice.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (index < 0) throw new ArgumentException($"Invalid {name}: {value}. Choose {string.Join(", ", choices)}, or 1–{choices.Length}.");
        return index + 1;
    }

    private static int Number(string name, string value, int min, int max)
    {
        if (!int.TryParse(value, out var number) || number < min || number > max)
            throw new ArgumentException($"{name} must be a number from {min} to {max}.");
        return number;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
            Usage: dotnet run --project src/BasslineGenerator.Cli -- [options]

            Omitted options are requested interactively.
              --mode <single|arrangement>       1 or 2
              --style <dreamy|pop|acid|rave>    1–4; single section only
              --key <note>                      e.g. A, C#, Bb
              --scale <name|number>             see scales below; quote names with spaces
              --bpm <number>                    4–1000
              --bars <number>                   1–4096; 1–682 per arrangement section
              --progression <random|number|name>
              --confirm <yes|no>                answer the generation prompt
              --yes                             shorthand for --confirm yes
              -h, --help                        show this help

            """);
        Console.WriteLine("Scales:");
        for (var i = 0; i < ScaleLibrary.All.Count; i++)
            Console.WriteLine($"  {i + 1}. {ScaleLibrary.All[i].Name} — {ScaleLibrary.All[i].Mood}");
        Console.WriteLine("Progressions (names are case-sensitive):\n  0. Random");
        for (var i = 0; i < ChordProgressionLibrary.All.Count; i++)
            Console.WriteLine($"  {i + 1}. {ChordProgressionLibrary.All[i].Name}");
    }
}
