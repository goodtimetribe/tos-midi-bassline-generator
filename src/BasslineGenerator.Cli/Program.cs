using BasslineGenerator;
using BasslineGenerator.Export;
using BasslineGenerator.Generation;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;

try
{
    var options = CliOptions.Parse(args);
    if (options.Help) { CliOptions.PrintHelp(); return; }
    Console.WriteLine("-----------------------------------\nBASSLINE GENERATOR\n-----------------------------------");
    var mode = options.Mode ?? AskInt("Mode: 1. Single Section  2. Full Arrangement", 1, 2);
    if (mode == 2 && options.Style.HasValue)
        throw new ArgumentException("--style applies only to single-section mode.");
    var style = mode == 1
        ? (SongWorld)((options.Style ?? AskInt("Style: 1. Dreamy  2. Pop  3. Acid  4. Rave", 1, 4)) - 1)
        : SongWorld.Dreamy;
    var root = options.Key;
    while (root is null)
    {
        root = Ask("Key (e.g. A, C#, Bb)");
        try { NoteUtilities.PitchClass(root); }
        catch (ArgumentException) { Console.WriteLine("Enter a valid note name."); root = null; }
    }
    var scalePrompt = "Scale: " + string.Join("  ", ScaleLibrary.All.Select((s, i) => $"{i + 1}. {s.Name}"));
    var scale = options.Scale ?? AskInt(scalePrompt, 1, ScaleLibrary.All.Count);
    var bpm = options.Bpm ?? AskInt("BPM (30–300)", 30, 300);
    var bars = options.Bars ?? AskInt(mode == 1 ? "Bars (1–512)" : "Bars per section (1–512)", 1, 512);
    if (!options.Progression.HasValue)
    {
        Console.WriteLine("Progression: 0. Random");
        for (var i = 0; i < ChordProgressionLibrary.All.Count; i++)
        {
            var p = ChordProgressionLibrary.All[i];
            Console.WriteLine($"{i + 1}. {p.DisplayName} ({string.Join(" - ", p.ChordSequence)}) — {p.Description} {p.SuggestedUse}");
        }
    }
    var progression = options.Progression ?? AskInt("Select progression", 0, ChordProgressionLibrary.All.Count);
    var confirm = options.Confirm ?? Ask("Generate MIDI? (Y/N)").Equals("Y", StringComparison.OrdinalIgnoreCase);
    if (!confirm) return;
    var settings = new GeneratorSettings
    {
        RootNote = root, ScaleType = ScaleLibrary.All[scale - 1].Name,
        BPM = bpm, Bars = bars, Style = style,
        Progression = progression == 0 ? null : ChordProgressionLibrary.All[progression - 1].Id,
        Sections = mode == 2 ? BasslineGenerator.Generation.BasslineGenerator.FullArrangement(bars) : null
    };
    var result = new BasslineGenerator.Generation.BasslineGenerator().Generate(settings);
    var outputPath = Path.GetFullPath($"bassline-{Guid.NewGuid():N}.mid");
    new MidiExporter().SaveNew(result, outputPath);
    Console.WriteLine($"Style: {string.Join(" → ", result.Sections.Select(s => s.Style))}");
    Console.WriteLine($"Key: {root}\nScale: {settings.ScaleType}\nBars: {result.Sections.Sum(s => s.Bars)}\nBPM: {bpm}");
    Console.WriteLine($"Progression: {result.Progression}\nGenerated Motifs: {result.Motif.Name}\nOutput File: {outputPath}");
}
catch (EndOfStreamException)
{
    Console.Error.WriteLine("Input ended; no MIDI file was generated.");
    Environment.ExitCode = 1;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"{ex.Message} Use --help for usage.");
    Environment.ExitCode = 1;
}
catch (IOException ex)
{
    Console.Error.WriteLine($"Could not write MIDI file: {ex.Message}");
    Environment.ExitCode = 1;
}

static string Ask(string prompt)
{
    Console.Write($"{prompt}: ");
    return Console.ReadLine()?.Trim() ?? throw new EndOfStreamException();
}

static int AskInt(string prompt, int min, int max)
{
    while (true)
    {
        if (int.TryParse(Ask(prompt), out var value) && value >= min && value <= max) return value;
        Console.WriteLine($"Enter a number from {min} to {max}.");
    }
}
