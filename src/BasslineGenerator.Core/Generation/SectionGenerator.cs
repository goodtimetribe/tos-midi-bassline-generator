using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;

namespace BasslineGenerator.Generation;

public sealed class SectionGenerator(IRhythmGenerator rhythm, BassMovementGenerator movement)
{
    public IReadOnlyList<BassNote> Generate(SongSection section, int firstBar, int root,
        IReadOnlyList<int> scale, ChordProgression progression, Motif motif, IPreset preset,
        GeneratorSettings settings, Random random, ref int previousPitch)
    {
        var notes = new List<BassNote>();
        var dimensions = preset.Dimensions.WithOverrides(settings);
        dimensions.Validate();
        var pattern = CreatePattern(preset.Density, dimensions.Groove, random);
        var phrase = motif.Movements.ToArray();
        if (phrase.Length == 0) throw new ArgumentException("Motif must contain at least one movement.", nameof(motif));
        if (phrase.Length > 1 && firstBar > 0 && random.Next(100) < dimensions.Mutation)
            phrase[random.Next(1, phrase.Length)] = movement.Choose(random);
        var choices = CreateChoices(phrase, dimensions, random);
        for (var bar = 0; bar < section.Bars; bar++)
        {
            if (phrase.Length > 1 && bar > 0 && bar % 4 == 0 && random.Next(100) < dimensions.Mutation)
            {
                var index = random.Next(1, phrase.Length);
                phrase[index] = movement.Choose(random);
                choices = CreateChoices(phrase, dimensions, random);
                if (random.Next(100) < dimensions.Mutation)
                    pattern = CreatePattern(preset.Density, dimensions.Groove, random);
            }
            if (bar > 0 && random.Next(100) >= dimensions.Repetition)
            {
                pattern = CreatePattern(preset.Density, dimensions.Groove, random);
                var freshPhrase = phrase.Select((choice, index) => index == 0 ? choice : movement.Choose(random)).ToArray();
                choices = CreateChoices(freshPhrase, dimensions, random);
            }
            var ending = bar % 4 == 3 || bar == section.Bars - 1;
            var onsets = ending && random.Next(100) < dimensions.Mutation / 2
                ? pattern.Where(x => x < 12).ToArray() : pattern.ToArray();
            var chord = progression.Chords[(firstBar + bar) % progression.Chords.Count];
            var barStart = (long)(firstBar + bar) * MusicConstants.TicksPerBar;
            var barEnd = barStart + MusicConstants.TicksPerBar;
            var starts = new long[onsets.Length];
            for (var i = 0; i < onsets.Length; i++)
            {
                var step = onsets[i];
                var tick = barStart + step * MusicConstants.Sixteenth;
                if (step % 4 == 2) tick += (long)(settings.Swing * MusicConstants.Ppq / 2);
                if (settings.TimingVariation > 0 && step != 0)
                    tick += random.Next(-settings.TimingVariation, settings.TimingVariation + 1);
                starts[i] = Math.Clamp(tick, i == 0 ? barStart : starts[i - 1] + 1, barEnd - (onsets.Length - i));
            }
            for (var i = 0; i < onsets.Length; i++)
            {
                var choice = choices[i % choices.Length];
                var pitch = movement.Pitch(choice.Movement, root, chord, scale, previousPitch, choice.Chromatic);
                if (i > 0 && choice.Traverse)
                    pitch = ScaleStep(previousPitch, scale, pitch >= previousPitch);
                previousPitch = pitch;
                var next = i + 1 < starts.Length ? starts[i + 1] : barEnd;
                var length = Math.Max(1, (long)((next - starts[i]) * preset.Gate));
                var velocity = settings.Velocity + (settings.AccentDownbeats && onsets[i] == 0 ? 10 : 0)
                    + (onsets[i] % 4 != 0 ? dimensions.Groove / 10 : 0);
                if (settings.VelocityVariation > 0)
                    velocity += random.Next(-settings.VelocityVariation, settings.VelocityVariation + 1);
                notes.Add(new BassNote(pitch, starts[i], length, Math.Clamp(velocity, 1, 127)));
            }
        }
        return notes;
    }

    private static PhraseChoice[] CreateChoices(IReadOnlyList<BassMovement> phrase, MusicalDimensions dimensions, Random random)
    {
        var choices = new PhraseChoice[phrase.Count];
        choices[0] = new PhraseChoice(BassMovement.Root, false, false);
        for (var i = 1; i < choices.Length; i++)
        {
            if (choices[i - 1].Chromatic)
            {
                choices[i] = new PhraseChoice(BassMovement.Root, false, false);
                continue;
            }
            var choice = random.Next(100) < dimensions.Movement ? phrase[i] : BassMovement.Root;
            if (choice == BassMovement.Root && random.Next(100) < dimensions.Movement / 2)
                choice = random.Next(3) switch
                {
                    0 => BassMovement.Third,
                    1 => BassMovement.Fifth,
                    _ => BassMovement.Octave
                };
            if (choice is BassMovement.Seventh or BassMovement.PassingTone && random.Next(100) >= dimensions.Tension)
                choice = BassMovement.Fifth;
            if (random.Next(100) < dimensions.Tension / 3) choice = BassMovement.PassingTone;
            var chromatic = choice == BassMovement.PassingTone && random.Next(100) < dimensions.Tension;
            var traverse = choice != BassMovement.PassingTone && random.Next(100) < dimensions.Movement / 3;
            choices[i] = new PhraseChoice(choice, chromatic, traverse);
        }
        return choices;
    }

    private readonly record struct PhraseChoice(BassMovement Movement, bool Chromatic, bool Traverse);

    private IReadOnlyList<int> CreatePattern(RhythmDensity density, int groove, Random random)
    {
        var source = rhythm.Generate(density, random);
        var steps = new SortedSet<int> { 0 };
        foreach (var original in source.Where(step => step != 0))
        {
            var step = original / 4 * 4;
            if (random.Next(100) < groove)
            {
                if (random.Next(100) < groove / 4) continue;
                step = Math.Min(15, step + (random.Next(2) == 0 ? 2 : 3));
            }
            steps.Add(step);
        }
        return steps.ToArray();
    }

    private static int ScaleStep(int previous, IReadOnlyList<int> scale, bool ascending)
    {
        var direction = ascending ? 1 : -1;
        for (var distance = 1; distance <= 12; distance++)
        {
            var candidate = previous + direction * distance;
            if (candidate is >= 28 and <= 60 && scale.Contains(candidate % 12)) return candidate;
        }
        return previous;
    }
}
