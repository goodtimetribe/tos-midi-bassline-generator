using BasslineGenerator.Models;
namespace BasslineGenerator.Generation;

public static class PatternLibrary
{
    public static IReadOnlyList<Motif> Patterns { get; } =
    [
        new("Root Root Fifth Root", [BassMovement.Root, BassMovement.Root, BassMovement.Fifth, BassMovement.Root]),
        new("Root Third Fifth Octave", [BassMovement.Root, BassMovement.Third, BassMovement.Fifth, BassMovement.Octave]),
        new("Root Root Root Seventh", [BassMovement.Root, BassMovement.Root, BassMovement.Root, BassMovement.Seventh]),
        new("Root Fifth Root Fifth", [BassMovement.Root, BassMovement.Fifth, BassMovement.Root, BassMovement.Fifth]),
        new("Root Octave Fifth Root", [BassMovement.Root, BassMovement.Octave, BassMovement.Fifth, BassMovement.Root])
    ];
}
