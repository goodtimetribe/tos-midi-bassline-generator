# BasslineGenerator

A .NET 10 console application for generating motif-based electronic basslines as
standard MIDI type 1 files. Import the resulting MIDI into a DAW or a compatible
MIDI workflow and assign it to a bass instrument.

## Build and run

Install the .NET 10 SDK, then from the repository root:

```sh
dotnet build
dotnet test
dotnet run --project BasslineGenerator
```

Alternatively, run `dotnet run` from the `BasslineGenerator` directory. Choose
single section or full arrangement, style (for a single section), key, scale,
BPM, bars and progression. Confirm with `Y` to write a uniquely named
`bassline-<id>.mid` file in the current working directory. Existing files are
not overwritten. The program displays the output path and the actual
progression and motif selected.

### Command-line options

Use `--help` (or `-h`) to list all options, accepted values, and progression
names. Any omitted option is requested interactively; style is only used for a
single section. For example, to generate without prompts:

```sh
dotnet run --project BasslineGenerator -- --mode single --style acid --key A --scale minor --bpm 140 --bars 16 --progression "i - VI - III - VII" --yes
```

Mode, style, and scale also accept the numbers shown in their prompts.
Progression accepts its displayed number, `random`, or its exact, case-sensitive
name. Use `--confirm yes` or `--confirm no` to answer the generation prompt
explicitly; `--yes` is shorthand for `--confirm yes`. Options may also be
written as `--option=value`. Invalid options or values produce an error.

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

The generator is constructed through interfaces and presets, or can be created
directly:

```csharp
using BasslineGenerator.Export;
using BasslineGenerator.Generation;
using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using BasslineGenerator.Presets;

var exporter = new MidiExporter();
var generator = new BasslineGenerator.Generation.BasslineGenerator(
    new SectionGenerator(new RhythmGenerator(), new BassMovementGenerator()),
    new MotifGenerator(), exporter,
    new IPreset[] { new DreamyPreset(), new PopPreset(), new AcidPreset(), new RavePreset() });
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
exporter.Save(result.MidiFile, "bassline.mid");
```

`Save` overwrites an existing file at the specified path. Use `SaveNew` to
create a file only if the path does not already exist.

Omit `Sections` to use `Style` and `Bars` for a single section, or use
`BasslineGenerator.Generation.BasslineGenerator.FullArrangement(barsPerSection)`.
Sections are consecutive and total at most 4096 bars. BPM is 4–1000; velocity is
1–127; velocity variation is 0–127; timing variation is 0–30 ticks; swing is
0–0.5 of an eighth note. Humanization defaults to off. Notes are quantized when
timing variation and swing are zero. The exporter writes a conductor track
containing tempo and time signature and a separate note track.