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

    public int Pitch(BassMovement movement, int root, Chord chord, IReadOnlyList<int> scale, int previous, bool chromatic)
    {
        var chordRoot = root + chord.RootOffset;
        // Keep chord roots in a bass register while preserving their pitch class.
        while (chordRoot > 47) chordRoot -= 12;
        var pitch = movement switch
        {
            BassMovement.Root => chordRoot,
            BassMovement.Fifth => chordRoot + 7,
            BassMovement.Octave => chordRoot + 12,
            BassMovement.Third => chordRoot + chord.Third,
            BassMovement.Seventh => chordRoot + 10,
            BassMovement.PassingTone => chromatic ? chordRoot - 1 : FindPassingTone(chordRoot, scale),
            _ => throw new ArgumentOutOfRangeException(nameof(movement))
        };
        // Invert fifths and thirds where this avoids an unnecessarily large leap.
        if (movement is BassMovement.Fifth or BassMovement.Third or BassMovement.Seventh)
        {
            var lower = pitch - 12;
            if (lower >= 28 && Math.Abs(lower - previous) < Math.Abs(pitch - previous)) pitch = lower;
        }
        return Math.Clamp(pitch, 0, 127);
    }

    private static int FindPassingTone(int root, IReadOnlyList<int> scale)
    {
        for (var distance = 1; distance <= 12; distance++)
            if (scale.Contains((root - distance + 120) % 12)) return root - distance;
        return root;
    }
}
