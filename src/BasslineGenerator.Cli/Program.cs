using BasslineGenerator.Export;
using BasslineGenerator.Generation;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;

try
{
    Console.WriteLine("-----------------------------------\nBASSLINE GENERATOR\n-----------------------------------");
    var mode = AskInt("Mode: 1. Single Section  2. Full Arrangement", 1, 2);
    var style = mode == 1
        ? (SongWorld)(AskInt("Style: 1. Dreamy  2. Pop  3. Acid  4. Rave", 1, 4) - 1)
        : SongWorld.Dreamy;
    string root;
    while (true)
    {
        root = Ask("Key (e.g. A, C#, Bb)");
        try { NoteUtilities.PitchClass(root); break; }
        catch (ArgumentException) { Console.WriteLine("Enter a valid note name."); }
    }
    var scale = AskInt("Scale: 1. Major  2. Minor  3. Dorian  4. Mixolydian", 1, 4);
    var bpm = AskInt("BPM (4–1000)", 4, 1000);
    var bars = AskInt(mode == 1 ? "Bars (1–4096)" : "Bars per section (1–682)", 1, mode == 1 ? 4096 : 682);
    Console.WriteLine("Progression: 0. Random");
    for (var i = 0; i < ChordProgressionLibrary.All.Count; i++)
        Console.WriteLine($"{i + 1}. {ChordProgressionLibrary.All[i].Name}");
    var progression = AskInt("Select progression", 0, ChordProgressionLibrary.All.Count);
    var confirm = Ask("Generate MIDI? (Y/N)");
    if (!confirm.Equals("Y", StringComparison.OrdinalIgnoreCase)) return;
    var settings = new GeneratorSettings
    {
        RootNote = root, ScaleType = new[] { "Major", "Minor", "Dorian", "Mixolydian" }[scale - 1],
        BPM = bpm, Bars = bars, Style = style,
        Progression = progression == 0 ? null : ChordProgressionLibrary.All[progression - 1].Name,
        Sections = mode == 2 ? BasslineGenerator.Generation.BasslineGenerator.FullArrangement(bars) : null
    };
    var result = new BasslineGenerator.Generation.BasslineGenerator().Generate(settings);
    new MidiExporter().Save(result, "bassline.mid");
    Console.WriteLine($"Style: {string.Join(" → ", result.Sections.Select(s => s.Style))}");
    Console.WriteLine($"Key: {root}\nScale: {settings.ScaleType}\nBars: {result.Sections.Sum(s => s.Bars)}\nBPM: {bpm}");
    Console.WriteLine($"Progression: {result.Progression}\nGenerated Motifs: {result.Motif.Name}\nOutput File: {Path.GetFullPath("bassline.mid")}");
}
catch (EndOfStreamException)
{
    Console.Error.WriteLine("Input ended; no MIDI file was generated.");
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
