using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BasslineGenerator.Api.Models;
using BasslineGenerator.Generation;
using BasslineGenerator.Models;
using BasslineGenerator.Packaging;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BasslineGenerator.Tests;

public class PackageTests
{
    private static readonly object Request = new { rootNote = "A", scaleType = "Minor", style = "Acid", bpm = 140, bars = 8 };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static GenerationPackageService Service() => new(new Generation.BasslineGenerator());

    private static GenerationPackage Create(int? seed = 12345, bool arrangement = false) => Service().Create(new GeneratorSettings
    {
        Seed = seed, Bars = 4, Style = SongWorld.Acid, Movement = arrangement ? 60 : null,
        IsArrangement = arrangement,
        Sections = arrangement ? Generation.BasslineGenerator.FullArrangement(2) : null
    });

    [Fact]
    public void SameSeedReproducesSameResult()
    {
        Assert.Equal(Create().Notes, Create().Notes);
        Assert.NotEqual(Create().Notes, Create(999).Notes);
    }

    [Fact]
    public void OmittedSeedIsRandomAndReported()
    {
        var package = Create(null);
        Assert.Equal(package.Seed, package.EffectiveConfiguration.Seed);
        Assert.True(Service().TryRegenerate(package, out var again));
        Assert.Equal(package.Notes, again.Notes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShareCodeRoundTripsAndIsDeterministic(bool arrangement)
    {
        var package = Create(arrangement: arrangement);
        var code = ShareCodec.Encode(package);
        Assert.Equal(code, ShareCodec.Encode(package with { Notes = null, Statistics = null }));
        Assert.Matches("^[A-Za-z0-9_-]+$", code);
        var decoded = ShareCodec.Decode(code);
        Assert.Equal(package.EffectiveConfiguration.Sections, decoded!.EffectiveConfiguration.Sections);
        Assert.True(Service().TryRegenerate(decoded, out var regenerated));
        Assert.Equal(package.Notes, regenerated.Notes);
        Assert.Equal(package.Statistics, regenerated.Statistics);
        Assert.Equal(package.EffectiveConfiguration.Mode, regenerated.EffectiveConfiguration.Mode);
    }

    [Fact]
    public void PackageSerializesAndDeserializes()
    {
        var package = Create();
        var restored = JsonSerializer.Deserialize<GenerationPackage>(JsonSerializer.Serialize(package, Json), Json)!;
        Assert.Equal(1, restored.Version);
        Assert.Equal(package.Notes, restored.Notes);
        Assert.Equal(package.EffectiveConfiguration.Sections, restored.EffectiveConfiguration.Sections);
        Assert.True(Service().TryRegenerate(restored, out var again));
        Assert.Equal(package.Notes, again.Notes);
    }

    [Fact]
    public void VisualizationDataMatchesGeneratedNotes()
    {
        var result = new Generation.BasslineGenerator().Generate(new GeneratorSettings { Seed = 7, Bars = 4 });
        var package = GenerationPackageService.FromResult(result);
        Assert.Equal(result.Notes.Count, package.Notes!.Count);
        for (var i = 0; i < result.Notes.Count; i++)
        {
            var (source, note) = (result.Notes[i], package.Notes[i]);
            Assert.Equal(source.Pitch, note.MidiNote);
            Assert.Equal(source.Start / 480.0, note.StartBeat);
            Assert.Equal(source.Length / 480.0, note.DurationBeats);
            Assert.Equal(source.Velocity, note.Velocity);
        }
        Assert.Equal("A2", MusicTheory.NoteUtilities.NoteName(45));
        var stats = package.Statistics!;
        Assert.Equal(result.Notes.Count, stats.NoteCount);
        Assert.Equal(result.Notes.Select(n => n.Pitch).Distinct().Count(), stats.UniqueNotes);
        Assert.Equal(1, stats.SectionCount);
        Assert.Equal(4, stats.ArrangementBars);
    }

    [Fact]
    public void EditedSectionBehaviorIsHonoredAndUnsupportedVersionRejected()
    {
        var package = Create();
        var section = package.EffectiveConfiguration.Sections[0] with { Behavior = new MusicalDimensions(0, 0, 0, 0, 100) };
        var edited = package with { EffectiveConfiguration = package.EffectiveConfiguration with { Sections = [section] } };
        Assert.True(Service().TryRegenerate(edited, out var result));
        Assert.Equal(section.Behavior, result.EffectiveConfiguration.Sections[0].Behavior);
        Assert.False(Service().TryRegenerate(package with { Version = 2 }, out _));
        Assert.False(Service().TryRegenerate(package with { Seed = 1 }, out _));
    }

    [Fact]
    public void ShareDecodeRejectsGarbage()
    {
        Assert.Null(ShareCodec.Decode("!!not-a-code"));
        Assert.Null(ShareCodec.Decode("AAAA"));
        Assert.Null(ShareCodec.Decode(null));
    }

    [Fact]
    public async Task ApiPackageShareDecodeFlowAndLegacyEndpointStillWork()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var packageResponse = await client.PostAsJsonAsync("/api/generate/package", new { rootNote = "A", scaleType = "Minor", style = "Acid", bpm = 140, bars = 8 });
        Assert.Equal(HttpStatusCode.OK, packageResponse.StatusCode);
        var text = await packageResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        var first = document.RootElement.GetProperty("notes")[0];
        Assert.Equal(["note", "midiNote", "startBeat", "durationBeats", "velocity"],
            first.EnumerateObject().Select(p => p.Name));
        var package = JsonSerializer.Deserialize<GenerationPackage>(text, Json)!;

        using var shareResponse = await client.PostAsJsonAsync("/api/share", package);
        var share = (await shareResponse.Content.ReadFromJsonAsync<ShareResponse>())!;
        using var decodeResponse = await client.PostAsJsonAsync("/api/share/decode", new { shareCode = share.ShareCode });
        var decoded = (await decodeResponse.Content.ReadFromJsonAsync<GenerationPackage>(Json))!;
        Assert.Equal(package.Notes, decoded.Notes);
        Assert.Equal(package.Statistics, decoded.Statistics);

        using var regen = await client.PostAsJsonAsync("/api/package/regenerate", package);
        Assert.Equal(package.Notes, (await regen.Content.ReadFromJsonAsync<GenerationPackage>(Json))!.Notes);

        using var bad = await client.PostAsJsonAsync("/api/share/decode", new { shareCode = "bogus" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        using var badVersion = await client.PostAsJsonAsync("/api/share", package with { Version = 9 });
        Assert.Equal(HttpStatusCode.BadRequest, badVersion.StatusCode);

        using var midi = await client.PostAsJsonAsync("/api/generate", new { rootNote = "A", scaleType = "Minor", style = "Acid", bpm = 140, bars = 8, seed = package.Seed });
        Assert.Equal("audio/midi", midi.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PackageReplaysTheSameMidiAsGenerate()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var body = new { rootNote = "E", scaleType = "Phrygian", style = "Rave", bpm = 150, bars = 4, mode = "arrangement", seed = 5, velocityVariation = 8, swing = 0.2 };
        using var packageResponse = await client.PostAsJsonAsync("/api/generate/package", body);
        var package = (await packageResponse.Content.ReadFromJsonAsync<GenerationPackage>(Json))!;
        Assert.Equal(6, package.Statistics!.SectionCount);
        Assert.True(Service().TryRegenerate(package, out var replay));
        Assert.Equal(package.Notes, replay.Notes);
    }
}
