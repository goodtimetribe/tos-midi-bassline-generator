using BasslineGenerator.Export;
using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;
using BasslineGenerator.Presets;

namespace BasslineGenerator.Generation;

/// <summary>Coordinates a continuous progression and recurring motif across sections.</summary>
public sealed class BasslineGenerator(SectionGenerator sections, MotifGenerator motifs, MidiFileFactory midiFiles, IEnumerable<IPreset> presets) : IBassGenerator
{
    public BasslineGenerator() : this(
        new SectionGenerator(new RhythmGenerator(), new BassMovementGenerator()),
        new MotifGenerator(), new MidiFileFactory(),
        [new DreamyPreset(), new PopPreset(), new AcidPreset(), new RavePreset()])
    {
    }

    public GenerationResult Generate(GeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.BPM is < 30 or > 300) throw new ArgumentOutOfRangeException(nameof(settings.BPM));
        if (settings.Channel is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(settings.Channel));
        if (settings.Velocity is < 1 or > 127) throw new ArgumentOutOfRangeException(nameof(settings.Velocity));
        if (settings.VelocityVariation is < 0 or > 127) throw new ArgumentOutOfRangeException(nameof(settings.VelocityVariation));
        if (settings.TimingVariation is < 0 or > 30) throw new ArgumentOutOfRangeException(nameof(settings.TimingVariation));
        if (!double.IsFinite(settings.Swing) || settings.Swing is < 0 or > 0.5) throw new ArgumentOutOfRangeException(nameof(settings.Swing));
        var scale = ScaleLibrary.GetScale(settings.RootNote, settings.ScaleType);
        var song = settings.Sections?.ToArray() ?? [new SongSection("Single Section", settings.Style, settings.Bars)];
        if (song.Length == 0 || song.Any(s => s is null || s.Bars < 1 || s.Bars > 512) || song.Sum(s => (long)s.Bars) > 4096)
            throw new ArgumentException("Sections must contain 1–512 bars each and 1–4096 bars in total.", nameof(settings));
        var presetMap = presets.ToDictionary(p => p.World);
        if (song.Any(s => !presetMap.ContainsKey(s.Style))) throw new ArgumentException("Unknown style.", nameof(settings));
        foreach (var section in song) presetMap[section.Style].Dimensions.WithOverrides(settings).Validate();
        var random = settings.Seed is int seed ? new Random(seed) : new Random();
        var progression = ChordProgressionLibrary.Select(settings.Progression, settings.ScaleType, random);
        var motif = motifs.Generate(random);
        var root = 36 + NoteUtilities.PitchClass(settings.RootNote);
        var previous = root;
        var firstBar = 0;
        var notes = new List<BassNote>();
        foreach (var section in song)
        {
            notes.AddRange(sections.Generate(section, firstBar, root, scale, progression, motif, presetMap[section.Style], settings, random, ref previous));
            firstBar += section.Bars;
        }
        return new GenerationResult(midiFiles.Create(notes, settings.BPM, settings.Channel, firstBar),
            notes, motif, progression.Name, song, settings.RootNote, settings.ScaleType,
            settings.Style, settings.BPM, firstBar);
    }

    /// <summary>Creates an arrangement; bars specifies the length of each part.</summary>
    public static IReadOnlyList<SongSection> FullArrangement(int bars) =>
    [
        new("Intro", SongWorld.Dreamy, bars),
        new("Build", SongWorld.Pop, bars),
        new("Breakdown", SongWorld.Dreamy, bars),
        new("Drop", SongWorld.Acid, bars),
        new("Peak", SongWorld.Rave, bars),
        new("Outro", SongWorld.Dreamy, bars)
    ];
}
