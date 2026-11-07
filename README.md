# BasslineGenerator

A .NET 10 library and console application for generating motif-based electronic
basslines as standard MIDI type 1 files. Import the resulting MIDI into a DAW or
a compatible MIDI workflow and assign it to a bass instrument.

`src/BasslineGenerator.Core` contains the reusable generation and MIDI logic;
`src/BasslineGenerator.Cli` handles console interaction and references Core.
`tests/BasslineGenerator.Tests` tests Core independently of the CLI. Core can
also be referenced by an ASP.NET Core API, web backend, or desktop application.

## Build and run

Install the .NET 10 SDK, then from the repository root:

```sh
dotnet build
dotnet test
dotnet run --project src/BasslineGenerator.Cli
```

Alternatively, run `dotnet run` from the `src/BasslineGenerator.Cli` directory. Choose
single section or full arrangement, style (for a single section), key, scale,
BPM, bars and progression. Confirm with `Y` to write `bassline.mid` in the
current working directory. An existing file with that name is overwritten.
The program displays the actual progression and motif selected.

## Musical model

Notes use 4/4 time and a 480 PPQ grid. Rhythms are weighted selections of
repeated bar patterns; phrase endings leave space for longer notes. A single
chord-relative motif is reused throughout the song, with preset-specific
variations. The progression advances one chord per bar, including across section
boundaries. Dreamy is sparse, Pop is moderate, Acid adds chromatic approaches and
octave movement, and Rave uses dense, repetitive patterns. The full arrangement
uses Intro (Dreamy), Build (Pop), Breakdown (Dreamy), Drop (Acid), Peak (Rave),
and Outro (Dreamy); the CLI bar count is **per section**.

Scale degrees and pitch classes support Major, Natural Minor (`Minor`), Dorian,
and Mixolydian; sharps and flats such as `C#` and `Bb` are accepted. Progression
names preserve Roman numeral case and specify actual major/minor chord qualities
and semitone roots relative to the selected tonic. Consequently, a chosen
progression can deliberately borrow notes outside the selected scale (including
the major V in `i - V - VI - IV`); scale tones are used for non-chromatic
passing notes. Random selection chooses from the major-family or minor-family
progressions according to the scale.

## Programmatic use

Reference `BasslineGenerator.Core` and construct the generator directly:

```csharp
using BasslineGenerator.Export;
using BasslineGenerator.Generation;
using BasslineGenerator.Models;

var exporter = new MidiExporter();
var generator = new BasslineGenerator.Generation.BasslineGenerator();
var settings = new GeneratorSettings
{
    RootNote = "A", ScaleType = "Minor", BPM = 140, Style = SongWorld.Acid,
    Progression = "i - VI - III - VII", // null selects randomly
    Seed = 42,                         // optional reproducible output
    Velocity = 100, Channel = 0,       // MIDI channel is zero-based (0–15)
    VelocityVariation = 8, TimingVariation = 10, Swing = 0.15,
    AccentDownbeats = true,
    Sections = [
        new("Intro", SongWorld.Dreamy, 16),
        new("Build", SongWorld.Pop, 16),
        new("Acid", SongWorld.Acid, 32),
        new("Peak", SongWorld.Rave, 32),
        new("Outro", SongWorld.Dreamy, 16)
    ]
};
var result = generator.Generate(settings);
exporter.Save(result, "bassline.mid");
```

`Generate` does not write to disk. Its result exposes the notes, in-memory MIDI
file, selected motif, progression, sections, key, scale, requested style, BPM,
total bar count and note statistics. For arrangements, `Style` is the requested
settings value; the actual style of each part is in `Sections`. Use
`IMidiExporter` to abstract saving, or save the returned MIDI file separately.
For custom presets or rhythm generation, use the generator constructor accepting
the section generator, motif generator, MIDI file factory, and presets.

Omit `Sections` to use `Style` and `Bars` for a single section, or use
`BasslineGenerator.Generation.BasslineGenerator.FullArrangement(barsPerSection)`.
Sections are consecutive and total at most 4096 bars. BPM is 4–1000; velocity is
1–127; velocity variation is 0–127; timing variation is 0–30 ticks; swing is
0–0.5 of an eighth note. Humanization defaults to off. Notes are quantized when
timing variation and swing are zero. The exporter writes a conductor track
containing tempo and time signature and a separate note track.