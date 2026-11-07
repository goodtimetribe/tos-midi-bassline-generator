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
        var pattern = rhythm.Generate(preset.Density, random);
        for (var bar = 0; bar < section.Bars; bar++)
        {
            // Repeat a rhythm for a phrase; vary the ending and renew it every four bars.
            if (bar > 0 && bar % 4 == 0) pattern = rhythm.Generate(preset.Density, random);
            var ending = bar % 4 == 3 || bar == section.Bars - 1;
            var onsets = ending ? pattern.Where(x => x < 12).ToArray() : pattern.ToArray();
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
                var choice = motif.Movements[i % motif.Movements.Count];
                if (section.Style == SongWorld.Dreamy && choice == BassMovement.Octave) choice = BassMovement.Root;
                if (i > 0 && random.NextDouble() < preset.MutationChance) choice = movement.Choose(random);
                if (i > 0 && random.NextDouble() < preset.OctaveChance) choice = BassMovement.Octave;
                if (i > 0 && random.NextDouble() < preset.PassingChance) choice = BassMovement.PassingTone;
                var pitch = movement.Pitch(choice, root, chord, scale, previousPitch, section.Style == SongWorld.Acid);
                previousPitch = pitch;
                var next = i + 1 < starts.Length ? starts[i + 1] : barEnd;
                var length = Math.Max(1, (long)((next - starts[i]) * preset.Gate));
                var velocity = settings.Velocity + (settings.AccentDownbeats && onsets[i] == 0 ? 10 : 0);
                if (settings.VelocityVariation > 0)
                    velocity += random.Next(-settings.VelocityVariation, settings.VelocityVariation + 1);
                notes.Add(new BassNote(pitch, starts[i], length, Math.Clamp(velocity, 1, 127)));
            }
        }
        return notes;
    }
}
