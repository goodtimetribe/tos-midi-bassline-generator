using System.Net;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        Assert.Equal(GenerationPackage.CurrentEngineVersion, decoded!.EngineVersion);
        Assert.Equal(package.EffectiveConfiguration.Sections, decoded.EffectiveConfiguration.Sections);
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
        Assert.Equal(GenerationPackage.CurrentEngineVersion, restored.EngineVersion);
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
        Assert.False(Service().TryRegenerate(package with { EngineVersion = "999.0.0" }, out _));
        Assert.False(Service().TryRegenerate(package with { Seed = 1 }, out _));
    }

    [Fact]
    public void ShareCodeEnforcesExactJsonByteLimit()
    {
        var package = Create();
        GenerationPackage WithName(string name) => package with
        {
            EffectiveConfiguration = package.EffectiveConfiguration with
            {
                Sections = [package.EffectiveConfiguration.Sections[0] with { Name = name }]
            }
        };
        var core = WithName("") with { Notes = null, Statistics = null };
        var overhead = JsonSerializer.SerializeToUtf8Bytes(core, ShareCodec.JsonOptions).Length;
        var atLimit = WithName(new string('x', ShareCodec.MaxJsonBytes - overhead));
        var code = ShareCodec.Encode(atLimit);
        Assert.Equal(ShareCodec.MaxJsonBytes, JsonSerializer.SerializeToUtf8Bytes(
            atLimit with { Notes = null, Statistics = null }, ShareCodec.JsonOptions).Length);
        Assert.NotNull(ShareCodec.Decode(code));
        var tooLarge = WithName(new string('x', ShareCodec.MaxJsonBytes - overhead + 1));
        Assert.Throws<ShareCodeTooLargeException>(() => ShareCodec.Encode(tooLarge));
    }

    [Fact]
    public void ShareCodeEnforcesCompressedCodeLimit()
    {
        var package = Create();
        var random = new Random(123);
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var name = new string(Enumerable.Range(0, 300_000).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
        GenerationPackage WithLength(int length) => package with
        {
            EffectiveConfiguration = package.EffectiveConfiguration with
            {
                Sections = [package.EffectiveConfiguration.Sections[0] with { Name = name[..length] }]
            }
        };
        var low = 0;
        var high = name.Length;
        while (high - low > 1)
        {
            var middle = (low + high) / 2;
            try { ShareCodec.Encode(WithLength(middle)); low = middle; }
            catch (ShareCodeTooLargeException) { high = middle; }
        }
        var code = ShareCodec.Encode(WithLength(low));
        Assert.InRange(code.Length, ShareCodec.MaxCodeLength - 64, ShareCodec.MaxCodeLength);
        Assert.Equal(name[..low], ShareCodec.Decode(code)!.EffectiveConfiguration.Sections[0].Name);
        Assert.Throws<ShareCodeTooLargeException>(() => ShareCodec.Encode(WithLength(high)));
    }

    [Fact]
    public void LegacyPackagesAndShareCodesReplayAsLegacyEngine()
    {
        var package = Create();
        var node = JsonNode.Parse(JsonSerializer.Serialize(package, Json))!.AsObject();
        node.Remove("engineVersion");
        var legacyJson = node.ToJsonString();
        var legacy = JsonSerializer.Deserialize<GenerationPackage>(legacyJson, Json)!;
        Assert.Null(legacy.EngineVersion);
        Assert.True(Service().TryRegenerate(legacy, out var replay));
        Assert.Equal(package.Notes, replay.Notes);
        Assert.Equal(GenerationPackage.CurrentEngineVersion, replay.EngineVersion);
        node["notes"] = null;
        node["statistics"] = null;
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.SmallestSize, true))
            deflate.Write(Encoding.UTF8.GetBytes(node.ToJsonString()));
        var code = Convert.ToBase64String(buffer.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.True(Service().TryRegenerate(ShareCodec.Decode(code), out var decoded));
        Assert.Equal(package.Notes, decoded.Notes);
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
        Assert.Equal(GenerationPackage.CurrentEngineVersion, document.RootElement.GetProperty("engineVersion").GetString());
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
        using var direct = await client.PostAsJsonAsync("/api/generate", body);
        using var fromPackage = await client.PostAsJsonAsync("/api/package/midi", package);
        Assert.Equal(HttpStatusCode.OK, fromPackage.StatusCode);
        Assert.Equal("audio/midi", fromPackage.Content.Headers.ContentType?.MediaType);
        Assert.Equal("bassline.mid", fromPackage.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(await direct.Content.ReadAsByteArrayAsync(), await fromPackage.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task EditedPackageMidiUsesReplayAndRejectsUnsupportedVersions()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var package = Create(arrangement: true);
        var section = package.EffectiveConfiguration.Sections[0] with { Behavior = new MusicalDimensions(0, 0, 0, 0, 100) };
        var edited = package with
        {
            EffectiveConfiguration = package.EffectiveConfiguration with
            {
                Sections = [section, .. package.EffectiveConfiguration.Sections.Skip(1)]
            },
            Notes = [new NoteData("C4", 60, 0, 1, 100)]
        };
        using var replayResponse = await client.PostAsJsonAsync("/api/package/regenerate", edited);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var replay = (await replayResponse.Content.ReadFromJsonAsync<GenerationPackage>(Json))!;
        Assert.Equal(section.Behavior, replay.EffectiveConfiguration.Sections[0].Behavior);
        Assert.True(Service().TryReplay(edited, out var coreResult));
        Assert.Equal(replay.Notes, GenerationPackageService.FromResult(coreResult).Notes);
        using var expected = new MemoryStream();
        new Export.MidiExporter().Write(coreResult, expected);
        using var midi = await client.PostAsJsonAsync("/api/package/midi", edited);
        Assert.Equal(HttpStatusCode.OK, midi.StatusCode);
        Assert.Equal(expected.ToArray(), await midi.Content.ReadAsByteArrayAsync());

        foreach (var path in new[] { "/api/package/midi", "/api/package/regenerate", "/api/share" })
        {
            using var invalid = await client.PostAsJsonAsync(path, edited with { EngineVersion = "999.0.0" });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
    }

    [Fact]
    public async Task ApiRejectsPackagesThatExceedShareLimits()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var package = Create();
        var oversized = package with
        {
            EffectiveConfiguration = package.EffectiveConfiguration with
            {
                Sections = [package.EffectiveConfiguration.Sections[0] with { Name = new string('x', ShareCodec.MaxJsonBytes) }]
            }
        };
        using var response = await client.PostAsJsonAsync("/api/share", oversized);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Package is too large to share.", (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Error);
    }

    [Fact]
    public async Task ApiShareCodesNearCompressedLimitDecodeAndOversizedCodesAreRejected()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var package = Create();
        var random = new Random(123);
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var name = new string(Enumerable.Range(0, 280_000).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
        GenerationPackage WithLength(int length) => package with
        {
            EffectiveConfiguration = package.EffectiveConfiguration with
            {
                Sections = [package.EffectiveConfiguration.Sections[0] with { Name = name[..length] }]
            }
        };
        using var response = await client.PostAsJsonAsync("/api/share", WithLength(250_000));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var share = (await response.Content.ReadFromJsonAsync<ShareResponse>())!;
        Assert.InRange(share.ShareCode.Length, 245_000, ShareCodec.MaxCodeLength);
        using var decode = await client.PostAsJsonAsync("/api/share/decode", new { share.ShareCode });
        Assert.Equal(HttpStatusCode.OK, decode.StatusCode);
        var decoded = (await decode.Content.ReadFromJsonAsync<GenerationPackage>(Json))!;
        Assert.Equal(name[..250_000], decoded.EffectiveConfiguration.Sections[0].Name);
        Assert.Equal(package.Notes, decoded.Notes);

        using var oversized = await client.PostAsJsonAsync("/api/share", WithLength(name.Length));
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        Assert.Equal("Package is too large to share.", (await oversized.Content.ReadFromJsonAsync<ErrorResponse>())!.Error);
    }

    [Fact]
    public async Task LegacyPackageReplaysAndUnsupportedEngineCodeIsRejected()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var package = Create();
        var legacy = JsonNode.Parse(JsonSerializer.Serialize(package, Json))!.AsObject();
        legacy.Remove("engineVersion");
        using var response = await client.PostAsync("/api/package/midi", new StringContent(legacy.ToJsonString(), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var code = ShareCodec.Encode(package with { EngineVersion = "999.0.0" });
        using var decode = await client.PostAsJsonAsync("/api/share/decode", new { shareCode = code });
        Assert.Equal(HttpStatusCode.BadRequest, decode.StatusCode);
    }

    [Fact]
    public async Task SwaggerDocumentsPackageMidiAndEngineVersion()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/package/midi").GetProperty("post");
        Assert.Equal("binary", operation.GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("audio/midi").GetProperty("schema").GetProperty("format").GetString());
        var version = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("GenerationPackage")
            .GetProperty("properties").GetProperty("engineVersion");
        Assert.Contains("legacy engine 1.0.0", version.GetProperty("description").GetString());
    }
}
