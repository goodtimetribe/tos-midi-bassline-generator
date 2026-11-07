namespace BasslineGenerator.Models;

/// <summary>Musical behavior values on a scale from 0 to 100.</summary>
public sealed record MusicalDimensions(int Movement, int Groove, int Mutation, int Tension, int Repetition)
{
    public MusicalDimensions WithOverrides(GeneratorSettings settings) => new(
        settings.Movement ?? Movement, settings.Groove ?? Groove, settings.Mutation ?? Mutation,
        settings.Tension ?? Tension, settings.Repetition ?? Repetition);

    public void Validate()
    {
        if (Movement is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(Movement));
        if (Groove is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(Groove));
        if (Mutation is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(Mutation));
        if (Tension is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(Tension));
        if (Repetition is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(Repetition));
    }
}
