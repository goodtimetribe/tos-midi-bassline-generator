using BasslineGenerator.Export;
using BasslineGenerator.Generation;
using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;
using BasslineGenerator.Presets;
using Generator = BasslineGenerator.Generation.BasslineGenerator;

namespace BasslineGenerator.Tests;

public class WorldSequencingTests
{
    internal static Generator CreateGenerator() => new(
        new SectionGenerator(new RhythmGenerator(), new BassMovementGenerator()),
        new MotifGenerator(), new MidiExporter(),
        new IPreset[] { new DreamyPreset(), new PopPreset(), new AcidPreset(), new RavePreset() });

    [Fact]
    public void SectionsAreContinuousAndRepeatSameProgression()
    {
        var settings = new GeneratorSettings
        {
            Seed = 14, RootNote = "A", ScaleType = "Minor", Progression = "i - VI - III - VII",
            Sections = [new("Intro", SongWorld.Dreamy, 3), new("Drop", SongWorld.Acid, 2), new("Outro", SongWorld.Dreamy, 1)]
        };
        var generator = CreateGenerator();
        var result = generator.Generate(settings);
        Assert.Equal(result.Notes, generator.Generate(settings).Notes);
        Assert.Equal(3, result.Sections.Count);
        var expectedRoots = new[] { 45, 41, 36, 43, 45, 41 };
        for (var bar = 0; bar < expectedRoots.Length; bar++)
        {
            var note = Assert.Single(result.Notes, n => n.Start == bar * MusicConstants.TicksPerBar);
            Assert.Equal(expectedRoots[bar], note.Pitch);
        }
        Assert.All(result.Notes, n => Assert.InRange(n.Start + n.Length, 1, 6L * MusicConstants.TicksPerBar));
        Assert.True(result.Notes.Count(n => n.Start >= 3L * MusicConstants.TicksPerBar && n.Start < 5L * MusicConstants.TicksPerBar) >
            result.Notes.Count(n => n.Start < 2L * MusicConstants.TicksPerBar));
    }

    [Fact]
    public void ValidatesSettings()
    {
        var generator = CreateGenerator();
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(new GeneratorSettings { Swing = double.NaN }));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(new GeneratorSettings { Channel = 16 }));
        Assert.Throws<ArgumentException>(() => generator.Generate(new GeneratorSettings { Sections = [] }));
    }
}
