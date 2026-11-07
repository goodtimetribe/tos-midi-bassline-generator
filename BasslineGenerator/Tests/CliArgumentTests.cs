using System.Diagnostics;
using BasslineGenerator.Export;
using Melanchall.DryWetMidi.Core;

namespace BasslineGenerator.Tests;

public class CliArgumentTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"bassline-cli-{Guid.NewGuid():N}");

    public CliArgumentTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task NamedOptionsGenerateWithoutPrompts()
    {
        var result = await Run("", "--mode", "single", "--style", "acid", "--key", "C#",
            "--scale", "minor", "--bpm", "140", "--bars", "2", "--progression", "i - VI - III - VII", "--yes");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("Generate MIDI?", result.Output);
        Assert.DoesNotContain("Select progression", result.Output);
        Assert.DoesNotContain("Mode: 1.", result.Output);
        Assert.DoesNotContain("Style: 1.", result.Output);
        Assert.DoesNotContain("Key (e.g.", result.Output);
        Assert.DoesNotContain("Scale: 1.", result.Output);
        Assert.DoesNotContain("BPM (4–1000):", result.Output);
        Assert.DoesNotContain("Bars (1–4096):", result.Output);
        Assert.Contains("Style: Acid", result.Output);
        Assert.Contains("Key: C#\nScale: Minor\nBars: 2\nBPM: 140", result.Output);
        Assert.Contains("Progression: i - VI - III - VII", result.Output);
        var file = Assert.Single(Directory.GetFiles(directory, "*.mid"));
        Assert.Contains(file, result.Output);
        Assert.Equal(MidiFileFormat.MultiTrack, MidiFile.Read(file).OriginalFormat);
    }

    [Fact]
    public async Task OmittedOptionsArePromptedAndNumericOptionsWork()
    {
        var result = await Run("Bb\n120\nY\n", "--mode=1", "--style=2", "--scale=2",
            "--bars=1", "--progression=1");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Key (e.g. A, C#, Bb):", result.Output);
        Assert.Contains("BPM (4–1000):", result.Output);
        Assert.Contains("Generate MIDI? (Y/N):", result.Output);
        Assert.DoesNotContain("Mode: 1.", result.Output);
        Assert.DoesNotContain("Style: 1.", result.Output);
        Assert.DoesNotContain("Select progression", result.Output);
        Assert.Contains("Style: Pop", result.Output);
        Assert.Contains("Key: Bb", result.Output);
        Assert.Single(Directory.GetFiles(directory, "*.mid"));
    }

    [Fact]
    public async Task ArrangementOptionsAndNegativeConfirmationWork()
    {
        var args = new[] { "--mode", "arrangement", "--key", "A", "--scale", "major",
            "--bpm", "120", "--bars", "1", "--progression", "random", "--confirm" };
        var declined = await Run("", [.. args, "no"]);
        Assert.Equal(0, declined.ExitCode);
        Assert.Empty(Directory.GetFiles(directory, "*.mid"));

        var accepted = await Run("", [.. args, "yes"]);
        Assert.Equal(0, accepted.ExitCode);
        Assert.Contains("Bars: 6", accepted.Output);
        Assert.DoesNotContain("Style: 1.", accepted.Output);
        Assert.Single(Directory.GetFiles(directory, "*.mid"));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task HelpListsOptionsWithoutPrompting(string option)
    {
        var result = await Run("", option);
        Assert.Equal(0, result.ExitCode);
        foreach (var name in new[] { "--mode", "--style", "--key", "--scale", "--bpm", "--bars", "--progression", "--confirm", "--yes", "--help" })
            Assert.Contains(name, result.Output);
        Assert.Contains("i - VI - III - VII", result.Output);
        Assert.Empty(Directory.GetFiles(directory, "*.mid"));
    }

    [Theory]
    [InlineData("--bpm", "3")]
    [InlineData("--key", "H")]
    [InlineData("--scale", "unknown")]
    [InlineData("--progression", "missing")]
    [InlineData("--confirm", "maybe")]
    [InlineData("--unknown", "value")]
    [InlineData("--bars", "")]
    public async Task InvalidOptionsFailWithoutGenerating(string option, string value)
    {
        var result = await Run("", option, value);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--help", result.Error);
        Assert.Empty(Directory.GetFiles(directory, "*.mid"));
    }

    [Fact]
    public async Task IncompatibleAndDuplicateOptionsFail()
    {
        foreach (var args in new[]
        {
            new[] { "--mode", "arrangement", "--style", "acid" },
            new[] { "--mode", "arrangement", "--bars", "683" },
            new[] { "--yes", "--confirm", "no" },
            new[] { "--yes=no" },
            new[] { "--key" }
        })
        {
            var result = await Run("", args);
            Assert.NotEqual(0, result.ExitCode);
            Assert.NotEmpty(result.Error);
        }

        Assert.Empty(Directory.GetFiles(directory, "*.mid"));
    }

    [Fact]
    public async Task EndOfInputFailsWithoutGenerating()
    {
        var result = await Run("", "--mode", "single");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Input ended", result.Error);
        Assert.Empty(Directory.GetFiles(directory, "*.mid"));
    }

    private async Task<(int ExitCode, string Output, string Error)> Run(string input, params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(typeof(MidiExporter).Assembly.Location);
        foreach (var arg in args) startInfo.ArgumentList.Add(arg);
        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output, await error);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
