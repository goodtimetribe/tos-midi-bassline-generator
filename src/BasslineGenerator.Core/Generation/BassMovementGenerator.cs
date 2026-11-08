using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;
namespace BasslineGenerator.Generation;

public sealed class BassMovementGenerator
{
    /// <summary>Chooses a movement using the default 50/20/15/10/5 distribution.</summary>
    public BassMovement Choose(Random random)
    {
        var n = random.Next(100);
        return n < 50 ? BassMovement.Root : n < 70 ? BassMovement.Fifth :
            n < 85 ? BassMovement.Octave : n < 95 ? BassMovement.Third : BassMovement.PassingTone;
    }

    /// <summary>
    /// Resolves chord and approach tones to the selected scale. The legacy chromatic flag
    /// selects a deeper diatonic approach instead of adding an out-of-scale note.
    /// </summary>
    public int Pitch(BassMovement movement, int root, Chord chord, IReadOnlyList<int> scale, int previous, bool chromatic)
    {
        var chordRoot = root + chord.RootOffset;
        // Keep chord roots in a bass register while preserving their pitch class.
        while (chordRoot > 47) chordRoot -= 12;
        chordRoot = NearestScalePitch(chordRoot, scale);
        var pitch = movement switch
        {
            BassMovement.Root => chordRoot,
            BassMovement.Fifth => chordRoot + 7,
            BassMovement.Octave => chordRoot + 12,
            BassMovement.Third => chordRoot + chord.Third,
            BassMovement.Seventh => chordRoot + 10,
            BassMovement.PassingTone => FindPassingTone(chordRoot, scale, chromatic ? 2 : 1),
            _ => throw new ArgumentOutOfRangeException(nameof(movement))
        };
        pitch = NearestScalePitch(pitch, scale);
        // Invert fifths and thirds where this avoids an unnecessarily large leap.
        if (movement is BassMovement.Fifth or BassMovement.Third or BassMovement.Seventh)
        {
            var lower = pitch - 12;
            if (lower >= 28 && Math.Abs(lower - previous) < Math.Abs(pitch - previous)) pitch = lower;
        }
        return Math.Clamp(pitch, 0, 127);
    }

    private static int FindPassingTone(int root, IReadOnlyList<int> scale, int steps)
    {
        var pitch = root;
        while (steps-- > 0)
        {
            do { pitch--; } while (!scale.Contains((pitch % 12 + 12) % 12));
        }
        return pitch;
    }

    private static int NearestScalePitch(int pitch, IReadOnlyList<int> scale)
    {
        for (var distance = 0; distance < 12; distance++)
        {
            var lower = pitch - distance;
            if (lower >= 0 && scale.Contains(lower % 12)) return lower;
            var upper = pitch + distance;
            if (upper <= 127 && scale.Contains(upper % 12)) return upper;
        }
        throw new ArgumentException("Scale must contain a MIDI pitch class.", nameof(scale));
    }
}
