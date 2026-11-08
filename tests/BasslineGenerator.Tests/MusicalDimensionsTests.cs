using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;
using BasslineGenerator.Presets;
using Generator = BasslineGenerator.Generation.BasslineGenerator;

namespace BasslineGenerator.Tests;

public class MusicalDimensionsTests
{
    public static IEnumerable<object[]> Presets()
    {
        yield return [new DreamyPreset(), new MusicalDimensions(60, 20, 75, 15, 45)];
        yield return [new PopPreset(), new MusicalDimensions(40, 50, 25, 20, 80)];
        yield return [new AcidPreset(), new MusicalDimensions(75, 70, 50, 80, 85)];
        yield return [new RavePreset(), new MusicalDimensions(65, 55, 35, 60, 70)];
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void DefaultsAndOverridesAreApplied(IPreset preset, MusicalDimensions expected)
    {
        Assert.Equal(expected, preset.Dimensions);
        var defaults = Settings(17);
        defaults.Style = preset.World;
        var explicitDefaults = Settings(17);
        explicitDefaults.Style = preset.World;
        SetDimensions(explicitDefaults, expected);
        var generator = new Generator();
        Assert.Equal(generator.Generate(defaults).Notes, generator.Generate(explicitDefaults).Notes);

        foreach (var name in new[] { "Movement", "Groove", "Mutation", "Tension", "Repetition" })
        {
            var partial = Settings(17);
            partial.Style = preset.World;
            typeof(GeneratorSettings).GetProperty(name)!.SetValue(partial, 0);
            var fullySpecified = Settings(17);
            fullySpecified.Style = preset.World;
            SetDimensions(fullySpecified, expected);
            typeof(GeneratorSettings).GetProperty(name)!.SetValue(fullySpecified, 0);
            Assert.Equal(generator.Generate(partial).Notes, generator.Generate(fullySpecified).Notes);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void CoreRejectsInvalidDimensions(int invalid)
    {
        foreach (var name in new[] { "Movement", "Groove", "Mutation", "Tension", "Repetition" })
        {
            var settings = Settings(1);
            typeof(GeneratorSettings).GetProperty(name)!.SetValue(settings, invalid);
            Assert.Throws<ArgumentOutOfRangeException>(() => new Generator().Generate(settings));
        }
    }

    [Fact]
    public void MovementAndTensionChangePitchChoices()
    {
        var scale = ScaleLibrary.GetScale("A", "Minor");
        var lowMovement = GenerateMany(new(0, 0, 0, 0, 100));
        var highMovement = GenerateMany(new(100, 0, 0, 0, 100));
        Assert.True(highMovement.Sum(notes => notes.Select(n => n.Pitch).Distinct().Count()) >
                    lowMovement.Sum(notes => notes.Select(n => n.Pitch).Distinct().Count()));
        var lowTension = GenerateMany(new(70, 40, 0, 0, 100));
        var highTension = GenerateMany(new(70, 40, 0, 100, 100));
        Assert.Equal(0, lowTension.Sum(notes => notes.Count(n => !scale.Contains(n.Pitch % 12))));
        Assert.Equal(0, highTension.Sum(notes => notes.Count(n => !scale.Contains(n.Pitch % 12))));
        Assert.NotEqual(lowTension.SelectMany(notes => notes).Select(n => n.Pitch),
            highTension.SelectMany(notes => notes).Select(n => n.Pitch));
    }

    [Fact]
    public void GrooveAddsOffbeatsAndRepetitionReusesRhythm()
    {
        var straight = GenerateMany(new(50, 0, 0, 0, 100));
        var syncopated = GenerateMany(new(50, 100, 0, 0, 100));
        Assert.Equal(0, straight.Sum(notes => notes.Count(n => n.Start % MusicConstants.Ppq != 0)));
        Assert.True(syncopated.Sum(notes => notes.Count(n => n.Start % MusicConstants.Ppq != 0)) > 100);
        Assert.All(syncopated.SelectMany(notes => notes).Where(n => n.Start % MusicConstants.Ppq != 0),
            note => Assert.Equal(110, note.Velocity));
        var repeated = GenerateMany(new(50, 70, 0, 0, 100));
        var changing = GenerateMany(new(50, 70, 0, 0, 0));
        Assert.Equal(0, repeated.Sum(ChangedRhythms));
        Assert.True(changing.Sum(ChangedRhythms) > 100);
    }

    [Fact]
    public void MutationEvolvesRepeatedPhrases()
    {
        var stable = GenerateMany(new(70, 70, 0, 30, 100));
        var evolving = GenerateMany(new(70, 70, 100, 30, 100));
        Assert.Equal(0, stable.Sum(ChangedRhythms));
        Assert.True(evolving.Sum(ChangedRhythms) > 50);
        Assert.True(evolving.Sum(ChangedPitchPhrases) > stable.Sum(ChangedPitchPhrases));
    }

    private static int ChangedPitchPhrases(IReadOnlyList<BassNote> notes)
    {
        var phrases = new[] { 0, 4, 8, 12 }.Select(bar => string.Join(",",
            notes.Where(n => n.Start / MusicConstants.TicksPerBar == bar).Select(n => n.Pitch))).ToArray();
        return phrases.Skip(1).Count(phrase => phrase != phrases[0]);
    }

    private static int ChangedRhythms(IReadOnlyList<BassNote> notes)
    {
        var patterns = Enumerable.Range(0, 16).Select(bar => string.Join(",",
            notes.Where(n => n.Start / MusicConstants.TicksPerBar == bar)
                .Select(n => n.Start % MusicConstants.TicksPerBar))).ToArray();
        return patterns.Skip(1).Count(pattern => pattern != patterns[0]);
    }

    private static IReadOnlyList<BassNote>[] GenerateMany(MusicalDimensions dimensions) =>
        Enumerable.Range(0, 24).Select(seed =>
        {
            var settings = Settings(seed);
            SetDimensions(settings, dimensions);
            return new Generator().Generate(settings).Notes;
        }).ToArray();

    private static GeneratorSettings Settings(int seed) => new()
    {
        Seed = seed, Style = SongWorld.Acid, Bars = 16, RootNote = "A", ScaleType = "Minor",
        Progression = "i - VI - III - VII"
    };

    private static void SetDimensions(GeneratorSettings settings, MusicalDimensions dimensions)
    {
        settings.Movement = dimensions.Movement;
        settings.Groove = dimensions.Groove;
        settings.Mutation = dimensions.Mutation;
        settings.Tension = dimensions.Tension;
        settings.Repetition = dimensions.Repetition;
    }
}
