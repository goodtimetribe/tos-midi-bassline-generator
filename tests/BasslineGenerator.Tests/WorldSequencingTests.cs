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
        new MotifGenerator(), new MidiFileFactory(),
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

    [Theory]
    [InlineData("Synthwave", new[] { 45, 41, 36, 43, 45, 41 })]
    [InlineData("Machine World", new[] { 45, 36, 38, 43, 45, 36 })]
    [InlineData("Techno Minimal", new[] { 45, 45, 43, 45, 45, 45 })]
    public void CuratedProgressionsDriveDownbeatsAndLoopAcrossSections(string progression, int[] expectedRoots)
    {
        var result = CreateGenerator().Generate(new GeneratorSettings
        {
            Seed = 14, RootNote = "A", ScaleType = "Minor", Progression = progression,
            Movement = 0, Tension = 0, Mutation = 0, Repetition = 100,
            Sections = [new("Intro", SongWorld.Dreamy, 3), new("Drop", SongWorld.Acid, 3)]
        });
        for (var bar = 0; bar < expectedRoots.Length; bar++)
            Assert.Equal(expectedRoots[bar], Assert.Single(result.Notes, n => n.Start == bar * MusicConstants.TicksPerBar).Pitch);
    }

    [Fact]
    public void ValidatesSettings()
    {
        var generator = CreateGenerator();
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(new GeneratorSettings { Swing = double.NaN }));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(new GeneratorSettings { Channel = 16 }));
        Assert.Throws<ArgumentException>(() => generator.Generate(new GeneratorSettings { Sections = [] }));
    }

    [Fact]
    public void DefaultGeneratorReturnsMetadataAndStatisticsForAnArrangement()
    {
        var settings = new GeneratorSettings
        {
            RootNote = "Bb", ScaleType = "Dorian", Style = SongWorld.Rave, BPM = 125, Seed = 7,
            Sections = [new("Intro", SongWorld.Dreamy, 2), new("Drop", SongWorld.Acid, 3)]
        };
        var result = new Generator().Generate(settings);
        settings.RootNote = "C";
        settings.BPM = 140;

        Assert.Equal("Bb", result.Key);
        Assert.Equal("Dorian", result.ScaleType);
        Assert.Equal(SongWorld.Rave, result.Style);
        Assert.Equal(125, result.BPM);
        Assert.Equal(5, result.BarCount);
        Assert.Equal(new[] { SongWorld.Dreamy, SongWorld.Acid }, result.Sections.Select(s => s.Style));
        Assert.Same(result.Motif, Assert.Single(result.SelectedMotifs));
        Assert.Equal(result.Notes.Count, result.Statistics.NoteCount);
        Assert.Equal(result.Notes.Min(n => n.Pitch), result.Statistics.LowestPitch);
        Assert.Equal(result.Notes.Max(n => n.Pitch), result.Statistics.HighestPitch);
        Assert.NotNull(result.MidiFile);
    }

    [Fact]
    public void DefaultAndCustomConstructionProduceTheSameSeededOutput()
    {
        var settings = new GeneratorSettings { Seed = 42, Bars = 4 };
        var expected = CreateGenerator().Generate(settings);
        var actual = new Generator().Generate(settings);
        Assert.Equal(expected.Notes, actual.Notes);
        Assert.Equal(expected.Progression, actual.Progression);
        Assert.Equal(expected.Motif.Name, actual.Motif.Name);
    }

    [Fact]
    public void MissingSeedReturnsAReproducibleConfigurationWithResolvedOverrides()
    {
        var settings = new GeneratorSettings
        {
            RootNote = "A", ScaleType = "Minor", Style = SongWorld.Acid, Bars = 2,
            Movement = 90, Groove = 25
        };
        var generator = CreateGenerator();
        var first = generator.Generate(settings);
        var replay = generator.Generate(new GeneratorSettings
        {
            RootNote = first.Configuration.RootNote,
            ScaleType = first.Configuration.ScaleType,
            Style = SongWorld.Acid,
            Bars = first.Configuration.Bars,
            Seed = first.Configuration.Seed,
            Movement = first.Configuration.Movement,
            Groove = first.Configuration.Groove,
            Mutation = first.Configuration.Mutation,
            Tension = first.Configuration.Tension,
            Repetition = first.Configuration.Repetition,
            Progression = first.Configuration.RequestedProgression
        });

        Assert.InRange(first.Configuration.Seed, 0, int.MaxValue);
        Assert.Equal(first.Notes, replay.Notes);
        Assert.Equal(first.Progression, replay.Progression);
        Assert.Equal(first.Configuration.Seed, replay.Configuration.Seed);
        Assert.Equal("single", first.Configuration.Mode);
        Assert.NotEmpty(first.Configuration.ProgressionId);
        var section = Assert.Single(first.Configuration.Sections);
        Assert.Equal("Acid", section.Style);
        Assert.Equal(new MusicalDimensions(90, 25, 50, 80, 85), section.Behavior);
        Assert.Equal(90, first.Configuration.Movement);
        Assert.Equal(25, first.Configuration.Groove);
    }

    [Fact]
    public void ArrangementConfigurationResolvesEachSectionStyleAndMode()
    {
        var sections = Generator.FullArrangement(1);
        var result = CreateGenerator().Generate(new GeneratorSettings
        {
            Seed = 42, Sections = sections, IsArrangement = true,
            Movement = 90
        });
        Assert.Equal("arrangement", result.Configuration.Mode);
        Assert.Equal(sections.Count, result.Configuration.Sections.Count);
        Assert.Equal(new[] { "Dreamy", "Pop", "Dreamy", "Acid", "Rave", "Dreamy" },
            result.Configuration.Sections.Select(section => section.Style));
        Assert.All(result.Configuration.Sections, section => Assert.Equal(90, section.Behavior.Movement));
        Assert.Equal(50, result.Configuration.Sections[1].Behavior.Groove);
    }
}
