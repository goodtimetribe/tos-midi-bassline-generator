using System.Diagnostics;
using Melanchall.DryWetMidi.Core;

namespace BasslineGenerator.Tests;

public class CliOutputTests
{
    [Fact]
    public async Task RepeatedGenerationsPreserveExistingFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bassline-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var oldPath = Path.Combine(directory, "bassline.mid");
            await File.WriteAllTextAsync(oldPath, "existing file");
            var firstPath = await Generate(directory);
            var firstBytes = await File.ReadAllBytesAsync(firstPath);
            var secondPath = await Generate(directory);

            Assert.NotEqual(firstPath, secondPath);
            Assert.Equal(directory, Path.GetDirectoryName(firstPath));
            Assert.Equal(directory, Path.GetDirectoryName(secondPath));
            Assert.Equal(firstBytes, await File.ReadAllBytesAsync(firstPath));
            Assert.Equal("existing file", await File.ReadAllTextAsync(oldPath));
            Assert.Equal(MidiFileFormat.MultiTrack, MidiFile.Read(secondPath).OriginalFormat);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static async Task<string> Generate(string directory)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--runtimeconfig");
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "BasslineGenerator.Tests.runtimeconfig.json"));
        startInfo.ArgumentList.Add("--depsfile");
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "BasslineGenerator.Tests.deps.json"));
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "BasslineGenerator.Cli.dll"));
        using var process = Process.Start(startInfo)!;
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync("1\n1\nA\n2\n120\n1\n0\nY\n");
        process.StandardInput.Close();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        Assert.True(process.ExitCode == 0, error);
        const string prefix = "Output File: ";
        var line = Assert.Single(output.Split('\n'), line => line.StartsWith(prefix));
        return line[prefix.Length..].Trim();
    }
}
