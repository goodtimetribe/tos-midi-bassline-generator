using System.Diagnostics.CodeAnalysis;
using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;

namespace BasslineGenerator.Packaging;

/// <summary>Creates and replays generation packages entirely in memory.</summary>
public sealed class GenerationPackageService(IBassGenerator generator)
{
    public GenerationPackage Create(GeneratorSettings settings) => FromResult(generator.Generate(settings));

    public static GenerationPackage FromResult(GenerationResult result) =>
        new(GenerationPackage.CurrentVersion, result.Configuration.Seed, result.Configuration)
        {
            EngineVersion = GenerationPackage.CurrentEngineVersion,
            Statistics = result.Statistics,
            Notes = result.Notes.Select(n => new NoteData(
                NoteUtilities.NoteName(n.Pitch), n.Pitch, (double)n.Start / MusicConstants.Ppq,
                (double)n.Length / MusicConstants.Ppq, n.Velocity)).ToArray()
        };

    /// <summary>Replays a package's effective configuration, ignoring any supplied statistics and notes.</summary>
    public bool TryRegenerate(GenerationPackage? package, [NotNullWhen(true)] out GenerationPackage? regenerated)
    {
        regenerated = null;
        if (!TryReplay(package, out var result)) return false;
        regenerated = FromResult(result);
        return true;
    }

    /// <summary>Replays the configuration to obtain the generated notes and MIDI.</summary>
    public bool TryReplay(GenerationPackage? package, [NotNullWhen(true)] out GenerationResult? result)
    {
        result = null;
        try
        {
            if (package is not { Version: GenerationPackage.CurrentVersion, EffectiveConfiguration: { } config }
                || (package.EngineVersion ?? GenerationPackage.LegacyEngineVersion) != GenerationPackage.CurrentEngineVersion
                || config.Seed != package.Seed || config.Sections is null || config.Sections.Count == 0)
                return false;
            result = generator.Generate(ToSettings(config));
            return true;
        }
        catch (Exception)
        {
            // Packages are untrusted input; callers report a fixed message without details.
            return false;
        }
    }

    public static GeneratorSettings ToSettings(GenerationConfiguration config) => new()
    {
        RootNote = config.RootNote,
        ScaleType = config.ScaleType,
        Style = Enum.Parse<SongWorld>(config.Style, true),
        BPM = config.BPM,
        Bars = config.Bars,
        Movement = config.Movement, Groove = config.Groove, Mutation = config.Mutation,
        Tension = config.Tension, Repetition = config.Repetition,
        Progression = config.RequestedProgression,
        Seed = config.Seed,
        Channel = config.Channel,
        Velocity = config.Velocity,
        VelocityVariation = config.VelocityVariation,
        TimingVariation = config.TimingVariation,
        Swing = config.Swing,
        AccentDownbeats = config.AccentDownbeats,
        Mode = config.Mode is "single" or "custom" or "arrangement" ? config.Mode : throw new ArgumentException("Unknown mode."),
        IsArrangement = config.Mode == "arrangement",
        // Explicit per-section behavior makes the replay independent of preset defaults.
        Sections = config.Sections.Select(s => new SongSection(s.Name, Enum.Parse<SongWorld>(s.Style, true), s.Bars, s.Behavior)).ToArray()
    };
}
