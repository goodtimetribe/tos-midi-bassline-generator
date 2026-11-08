using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using BasslineGenerator.Api.Models;
using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;
using Melanchall.DryWetMidi.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace BasslineGenerator.Tests;

public class ApiTests
{
    private static readonly object ValidRequest = new
    {
        rootNote = "A",
        scaleType = "Minor",
        style = "Acid",
        bpm = 140,
        bars = 16
    };

    [Fact]
    public async Task HealthAndPresetsReturnExpectedData()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var health = await client.GetFromJsonAsync<HealthResponse>("/api/health");
        Assert.Equal(new HealthResponse("healthy", "1.0.0"), health);
        Assert.Equal(new[] { "Dreamy", "Pop", "Acid", "Rave" },
            await client.GetFromJsonAsync<string[]>("/api/presets"));
    }

    [Fact]
    public async Task ProgressionEndpointSerializesTheEntireLibrary()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/progressions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(ChordProgressionLibrary.All.Count, items.Length);
        for (var i = 0; i < items.Length; i++)
        {
            var p = ChordProgressionLibrary.All[i];
            var item = items[i];
            Assert.Equal(p.Id, item.GetProperty("id").GetString());
            Assert.Equal(p.DisplayName, item.GetProperty("name").GetString());
            Assert.Equal(p.Description, item.GetProperty("description").GetString());
            Assert.Equal(p.ChordSequence, item.GetProperty("chordSequence").EnumerateArray().Select(c => c.GetString()));
            Assert.Equal(p.Mood, item.GetProperty("mood").GetString());
            Assert.Equal(p.SuggestedUse, item.GetProperty("suggestedUse").GetString());
        }
    }

    [Theory]
    [InlineData("Synthwave")]
    [InlineData("machine-world")]
    [InlineData("i - bVII - bVI - bVII")]
    public async Task ProgressionNamesIdsAndLegacyValuesAreAccepted(string progression)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/generate", new
        {
            rootNote = "A", scaleType = "Minor", style = "Acid", bpm = 140, bars = 1, progression
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(30, 1)]
    [InlineData(300, 256)]
    public async Task GenerateReturnsValidMidiInMemory(int bpm, int bars)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/generate", new
        {
            rootNote = "A",
            scaleType = "Minor",
            style = "Acid",
            bpm,
            bars
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("audio/midi", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("bassline.mid", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(int.TryParse(response.Headers.GetValues("X-Generation-Seed").Single(), out _));
        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        var midi = MidiFile.Read(stream);
        Assert.Equal(MidiFileFormat.MultiTrack, midi.OriginalFormat);
        Assert.NotEmpty(midi.GetTrackChunks().SelectMany(t => t.Events).OfType<NoteOnEvent>());
        Assert.Equal((long)Math.Round(60_000_000.0 / bpm),
            midi.GetTrackChunks().First().Events.OfType<SetTempoEvent>().Single().MicrosecondsPerQuarterNote);
        Assert.Equal(bars * 4L * 480, midi.GetTrackChunks().Last().Events.Sum(e => e.DeltaTime));
    }

    [Fact]
    public async Task JsonGenerationResponseReturnsReplayableEffectiveConfiguration()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = JsonContent.Create(new
            {
                rootNote = "A", scaleType = "Minor", style = "Acid", bpm = 140, bars = 2,
                movement = 90, progression = "Synthwave"
            })
        };
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var configuration = root.GetProperty("configuration");
        var seed = configuration.GetProperty("seed").GetInt32();
        Assert.InRange(seed, 0, int.MaxValue);
        Assert.Equal("A", configuration.GetProperty("rootNote").GetString());
        Assert.Equal("Minor", configuration.GetProperty("scaleType").GetString());
        Assert.Equal("Acid", configuration.GetProperty("style").GetString());
        Assert.Equal("single", configuration.GetProperty("mode").GetString());
        Assert.Equal(seed, root.GetProperty("request").GetProperty("seed").GetInt32());
        Assert.Equal(90, configuration.GetProperty("movement").GetInt32());
        Assert.Equal("synthwave", configuration.GetProperty("progressionId").GetString());
        Assert.Equal("Synthwave", configuration.GetProperty("progressionName").GetString());
        Assert.Equal(new[] { "i", "VI", "III", "VII" },
            configuration.GetProperty("chordSequence").EnumerateArray().Select(chord => chord.GetString()));
        var behavior = Assert.Single(configuration.GetProperty("sections").EnumerateArray())
            .GetProperty("behavior");
        Assert.Equal(90, behavior.GetProperty("movement").GetInt32());
        Assert.Equal(70, behavior.GetProperty("groove").GetInt32());
        Assert.Equal(50, behavior.GetProperty("mutation").GetInt32());
        Assert.Equal(80, behavior.GetProperty("tension").GetInt32());
        Assert.Equal(85, behavior.GetProperty("repetition").GetInt32());
        Assert.True(root.GetProperty("statistics").GetProperty("noteCount").GetInt32() > 0);

        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = new StringContent(root.GetProperty("request").GetRawText(), Encoding.UTF8, "application/json")
        };
        replayRequest.Headers.Accept.ParseAdd("audio/midi");
        using var replay = await client.SendAsync(replayRequest);
        Assert.Equal("audio/midi", replay.Content.Headers.ContentType?.MediaType);
        Assert.Equal(root.GetProperty("midiSha256").GetString(),
            Convert.ToHexString(SHA256.HashData(await replay.Content.ReadAsByteArrayAsync())));
    }

    [Theory]
    [InlineData("single", 512, 512)]
    [InlineData("arrangement", 512, 3072)]
    public async Task MaximumCoreLengthsGenerateMidi(string mode, int bars, int expectedBars)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/generate", new
        {
            rootNote = "A", scaleType = "Minor", style = "Dreamy", bpm = 140, bars, mode, seed = 12345
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        var midi = MidiFile.Read(stream);
        Assert.Equal(expectedBars * 4L * 480, midi.GetTrackChunks().Last().Events.Sum(e => e.DeltaTime));
    }

    [Theory]
    [InlineData("Major", "Dreamy", "A")]
    [InlineData("Minor", "Pop", "B")]
    [InlineData("Dorian", "Acid", "C")]
    [InlineData("Mixolydian", "Rave", "D")]
    [InlineData("Lydian", "Dreamy", "A")]
    [InlineData("Phrygian", "Acid", "A")]
    [InlineData("Harmonic Minor", "Rave", "A")]
    [InlineData("Melodic Minor", "Acid", "A")]
    [InlineData("Major Pentatonic", "Pop", "A")]
    [InlineData("Minor Pentatonic", "Pop", "A")]
    [InlineData("Blues", "Acid", "A")]
    [InlineData("Minor", "Acid", "E")]
    [InlineData("Minor", "Acid", "F")]
    [InlineData("Minor", "Acid", "G")]
    [InlineData("Natural Minor", "acid", "C#")]
    [InlineData("phrygian", "Acid", "bb")]
    public async Task SupportedMusicalSettingsGenerateMidi(string scaleType, string style, string rootNote)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/generate", new { rootNote, scaleType, style, bpm = 140, bars = 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("audio/midi", response.Content.Headers.ContentType?.MediaType);
        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        var midi = MidiFile.Read(stream);
        var notes = midi.GetTrackChunks().SelectMany(t => t.Events).OfType<NoteOnEvent>().ToArray();
        Assert.NotEmpty(notes);
        var allowed = ScaleLibrary.GetScale(rootNote, scaleType);
        Assert.All(notes, note => Assert.Contains((int)note.NoteNumber % 12, allowed));
    }

    [Theory]
    [InlineData("movement")]
    [InlineData("groove")]
    [InlineData("mutation")]
    [InlineData("tension")]
    [InlineData("repetition")]
    public async Task MusicalDimensionsValidateEachField(string name)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        foreach (var value in new[] { "0", "100", "null", "-1", "101", "1.5", "\"private input\"" })
        {
            var body = $$"""{"rootNote":"A","scaleType":"Minor","style":"Acid","bpm":140,"bars":1,"{{name}}":{{value}}}""";
            using var response = await client.PostAsync("/api/generate", new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.Equal(value is "0" or "100" or "null" ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
            if (response.StatusCode == HttpStatusCode.BadRequest)
                Assert.DoesNotContain("private input", await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task GenerateForwardsOptionalDimensions()
    {
        var generator = new CapturingGenerator();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBassGenerator>();
                services.AddSingleton<IBassGenerator>(generator);
            }));
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/generate", new
        {
            rootNote = "A",
            scaleType = "Minor",
            style = "Acid",
            bpm = 140,
            bars = 1,
            movement = 70,
            groove = 80,
            mutation = 35,
            tension = 65,
            repetition = 85
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(generator.Settings);
        Assert.Equal((70, 80, 35, 65, 85), (generator.Settings.Movement, generator.Settings.Groove,
            generator.Settings.Mutation, generator.Settings.Tension, generator.Settings.Repetition));
    }

    [Fact]
    public async Task GenerateForwardsModesAndProgressions()
    {
        var generator = new CapturingGenerator();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBassGenerator>();
                services.AddSingleton<IBassGenerator>(generator);
            }));
        using var client = factory.CreateClient();

        foreach (var progression in ChordProgressionLibrary.All)
        {
            using var response = await client.PostAsJsonAsync("/api/generate", new
            {
                rootNote = "A",
                scaleType = "Minor",
                style = "Acid",
                bpm = 140,
                bars = 1,
                mode = "single",
                progression = progression.Name
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(progression.Name, generator.Settings!.Progression);
            Assert.Null(generator.Settings.Sections);
        }

        using var arrangement = await client.PostAsJsonAsync("/api/generate", new
        {
            rootNote = "A",
            scaleType = "Minor",
            style = "Acid",
            bpm = 140,
            bars = 2,
            mode = "arrangement",
            progression = "i - VI - III - VII"
        });
        Assert.Equal(HttpStatusCode.OK, arrangement.StatusCode);
        Assert.Equal("i - VI - III - VII", generator.Settings!.Progression);
        Assert.Equal(Generation.BasslineGenerator.FullArrangement(2), generator.Settings.Sections);
        using var stream = new MemoryStream(await arrangement.Content.ReadAsByteArrayAsync());
        var midi = MidiFile.Read(stream);
        Assert.Equal(12 * 4L * 480, midi.GetTrackChunks().Last().Events.Sum(e => e.DeltaTime));

        using var defaults = await client.PostAsJsonAsync("/api/generate", new
        {
            rootNote = "A",
            scaleType = "Minor",
            style = "Acid",
            bpm = 140,
            bars = 1,
            mode = (string?)null,
            progression = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, defaults.StatusCode);
        Assert.Null(generator.Settings!.Progression);
        Assert.Null(generator.Settings.Sections);
        Assert.Null(generator.Settings.Seed);
        Assert.Equal((0, 100, 0, 0, 0.0, false), (generator.Settings.Channel, generator.Settings.Velocity,
            generator.Settings.VelocityVariation, generator.Settings.TimingVariation, generator.Settings.Swing, generator.Settings.AccentDownbeats));
    }

    [Fact]
    public async Task SeedAndAllCoreOptionsAreForwardedAndReproduceMidi()
    {
        var generator = new CapturingGenerator();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBassGenerator>();
                services.AddSingleton<IBassGenerator>(generator);
            }));
        using var client = factory.CreateClient();
        var request = new
        {
            rootNote = "C#",
            scaleType = "major",
            style = "Acid",
            bpm = 140,
            bars = 1,
            seed = -12345,
            channel = 15,
            velocity = 80,
            velocityVariation = 10,
            timingVariation = 5,
            swing = 0.25,
            accentDownbeats = true,
            sections = new[] { new { name = "Intro", style = "dreamy", bars = 2 }, new { name = "Drop", style = "Acid", bars = 3 } }
        };
        using var first = await client.PostAsJsonAsync("/api/generate", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var settings = generator.Settings!;
        Assert.Equal("Major", settings.ScaleType);
        Assert.Equal((-12345, 15, 80, 10, 5, 0.25, true),
            (settings.Seed, settings.Channel, settings.Velocity, settings.VelocityVariation, settings.TimingVariation, settings.Swing, settings.AccentDownbeats));
        Assert.Equal(new[] { new SongSection("Intro", SongWorld.Dreamy, 2), new SongSection("Drop", SongWorld.Acid, 3) }, settings.Sections);
        var bytes = await first.Content.ReadAsByteArrayAsync();
        using var coreStream = new MemoryStream();
        new Export.MidiExporter().Write(new Generation.BasslineGenerator().Generate(settings), coreStream);
        Assert.Equal(coreStream.ToArray(), bytes);
        using var second = await client.PostAsJsonAsync("/api/generate", request);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(bytes, await second.Content.ReadAsByteArrayAsync());
        using var changed = await client.PostAsJsonAsync("/api/generate", new
        {
            rootNote = "C#",
            scaleType = "major",
            style = "Acid",
            bpm = 140,
            bars = 1,
            seed = -12344,
            channel = 15,
            velocity = 80,
            velocityVariation = 10,
            timingVariation = 5,
            swing = 0.25,
            accentDownbeats = true,
            request.sections
        });
        Assert.NotEqual(bytes, await changed.Content.ReadAsByteArrayAsync());
        using var stream = new MemoryStream(bytes);
        var midi = MidiFile.Read(stream);
        Assert.Equal(5 * 4L * 480, midi.GetTrackChunks().Last().Events.Sum(e => e.DeltaTime));
        Assert.All(midi.GetTrackChunks().SelectMany(t => t.Events).OfType<NoteOnEvent>(), e => Assert.Equal(15, (int)e.Channel));
    }

    [Theory]
    [InlineData("channel", "0", "15", "-1", "16")]
    [InlineData("velocity", "1", "127", "0", "128")]
    [InlineData("velocityVariation", "0", "127", "-1", "128")]
    [InlineData("timingVariation", "0", "30", "-1", "31")]
    [InlineData("swing", "0", "0.5", "-0.01", "0.51")]
    [InlineData("seed", "-2147483648", "2147483647", "-2147483649", "2147483648")]
    [InlineData("accentDownbeats", "true", "false", "1", "\"private input\"")]
    public async Task CoreOptionBoundariesAreValidated(string name, string minimum, string maximum, string below, string above)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        foreach (var (value, valid) in new[] { (minimum, true), (maximum, true), ("null", true), (below, false), (above, false) })
        {
            var body = $$"""{"rootNote":"A","scaleType":"Minor","style":"Acid","bpm":140,"bars":1,"{{name}}":{{value}}}""";
            using var response = await client.PostAsync("/api/generate", new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.Equal(valid ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
            Assert.DoesNotContain("private input", await response.Content.ReadAsStringAsync());
        }
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[null]")]
    [InlineData("[{\"name\":\"Intro\",\"style\":\"private input\",\"bars\":1}]")]
    [InlineData("[{\"name\":\"Intro\",\"style\":\"Acid\",\"bars\":0}]")]
    [InlineData("[{\"name\":\"Intro\",\"style\":\"Acid\",\"bars\":513}]")]
    [InlineData("[{\"style\":\"Acid\",\"bars\":1}]")]
    public async Task InvalidCustomSectionsReturnSafeBadRequest(string sections)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var body = $$"""{"rootNote":"A","scaleType":"Minor","style":"Acid","bpm":140,"bars":1,"sections":{{sections}}}""";
        using var response = await client.PostAsync("/api/generate", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("private input", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CustomSectionsCannotExceedTotalBarLimit()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var sections = Enumerable.Range(0, 9)
            .Select(i => new { name = $"Section {i}", style = "Acid", bars = 512 });
        using var response = await client.PostAsJsonAsync("/api/generate", new
        {
            rootNote = "A", scaleType = "Minor", style = "Acid", bpm = 140, bars = 1, sections
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"rootNote\":\"H\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":16}", "Root note")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"unknown\",\"style\":\"Acid\",\"bpm\":140,\"bars\":16}", "Scale type")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Unknown\",\"bpm\":140,\"bars\":16}", "Style")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":29,\"bars\":16}", "BPM")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":301,\"bars\":16}", "BPM")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":0}", "Bars")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":513}", "Bars")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":513,\"mode\":\"arrangement\"}", "Bars")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":1,\"mode\":\"arrangement\",\"sections\":[{\"name\":\"Intro\",\"style\":\"Acid\",\"bars\":1}]}", "Sections")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140}", "Bars")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":1,\"mode\":\"Arrangement\"}", "Mode")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":1,\"mode\":\"private input\"}", "Mode")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":1,\"progression\":\"private input\"}", "Progression")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":1,\"progression\":\"I - vi - iii - vii\"}", "Progression")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":1,\"mode\":42}", "Invalid request")]
    [InlineData("{\"bpm\":\"private input\"}", "Invalid request")]
    [InlineData("{", "Invalid request")]
    [InlineData("null", "Invalid request")]
    public async Task InvalidRequestsReturnJsonWithoutEchoingInput(string body, string expected)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/generate", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains(expected, error!.Error);
        Assert.DoesNotContain("private input", error.Error);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task UnexpectedErrorsReturnSafeJson()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBassGenerator>();
                services.AddTransient<IBassGenerator, ThrowingGenerator>();
            }));
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/generate", ValidRequest);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(new ErrorResponse("An unexpected error occurred."), await response.Content.ReadFromJsonAsync<ErrorResponse>());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task RateLimitUsesConfiguredWindow()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:PermitLimit"] = "2",
                ["RateLimit:WindowSeconds"] = "60"
            })));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        using var rejected = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(new ErrorResponse("Too many requests."), await rejected.Content.ReadFromJsonAsync<ErrorResponse>());
    }

    [Fact]
    public async Task DefaultRateLimitAllowsExactly100Requests()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        for (var i = 0; i < 100; i++)
        {
            using var response = await client.GetAsync("/api/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var rejected = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task RateLimitSeparatesClientAddresses()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:PermitLimit"] = "1",
                ["RateLimit:WindowSeconds"] = "60"
            }));
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, TestAddressFilter>());
        });
        using var client = factory.CreateClient();
        async Task<HttpStatusCode> Request(string address)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
            request.Headers.Add("X-Test-Address", address);
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }
        Assert.Equal(HttpStatusCode.OK, await Request("192.0.2.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Request("192.0.2.1"));
        Assert.Equal(HttpStatusCode.OK, await Request("192.0.2.2"));
    }

    [Fact]
    public async Task ApplicationLogsDoNotContainUserInputOrExceptionDetails()
    {
        using var logs = new CapturingLoggerProvider();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBassGenerator>();
                services.AddTransient<IBassGenerator, ThrowingGenerator>();
            });
        });
        using var client = factory.CreateClient();
        using var invalid = await client.PostAsJsonAsync("/api/generate", new
        {
            rootNote = "A",
            scaleType = "Minor",
            style = "private input",
            bpm = 140,
            bars = 16
        });
        using var failed = await client.PostAsJsonAsync("/api/generate", ValidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Contains(logs.Messages, message => message.Contains("Request validation failed."));
        Assert.Contains(logs.Messages, message => message.Contains("An unhandled exception occurred."));
        Assert.All(logs.Messages, message => Assert.DoesNotContain("private input", message));
    }

    [Theory]
    [InlineData("http://localhost:3000", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task CorsOnlyAllowsConfiguredOrigins(string origin, bool allowed)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/generate");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task SwaggerIsAvailableAndDocumentsMidi(string environment)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment(environment));
        using var client = factory.CreateClient();
        using var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        Assert.Contains("bassline-form", await ui.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var generate = document.RootElement.GetProperty("paths").GetProperty("/api/generate").GetProperty("post");
        Assert.True(generate.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
        Assert.True(generate.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("audio/midi", out _));
        Assert.True(generate.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("application/json", out _));
        Assert.True(generate.GetProperty("responses").GetProperty("200").GetProperty("headers").TryGetProperty("X-Generation-Seed", out _));
        Assert.True(generate.GetProperty("responses").GetProperty("400").GetProperty("content").TryGetProperty("application/json", out _));
        Assert.False(generate.GetProperty("responses").GetProperty("400").GetProperty("content").TryGetProperty("audio/midi", out _));

        foreach (var path in new[] { "analyze", "variation", "arrangement" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/{path}", ValidRequest)).StatusCode);
    }

    [Fact]
    public async Task SwaggerDescribesContractAndProvidesRunnableExamples()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var root = document.RootElement;
        var info = root.GetProperty("info");
        Assert.Equal("Bassline Generator API", info.GetProperty("title").GetString());
        Assert.Equal("1.0.0", info.GetProperty("version").GetString());
        Assert.Contains("Privacy-focused", info.GetProperty("description").GetString());
        Assert.Equal("GPL-3.0", info.GetProperty("license").GetProperty("name").GetString());

        var paths = root.GetProperty("paths");
        var health = paths.GetProperty("/api/health").GetProperty("get");
        var presets = paths.GetProperty("/api/presets").GetProperty("get");
        var progressions = paths.GetProperty("/api/progressions").GetProperty("get");
        var generate = paths.GetProperty("/api/generate").GetProperty("post");
        Assert.Equal("System", health.GetProperty("tags")[0].GetString());
        Assert.Equal("Generation", presets.GetProperty("tags")[0].GetString());
        Assert.Equal("Generation", generate.GetProperty("tags")[0].GetString());
        Assert.Contains("in memory", generate.GetProperty("description").GetString());
        foreach (var operation in new[] { health, presets, progressions, generate })
        {
            Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
            foreach (var status in new[] { "200", "429", "500" })
                Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
        }
        var responses = generate.GetProperty("responses");
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.Equal("binary", responses.GetProperty("200").GetProperty("content").GetProperty("audio/midi")
            .GetProperty("schema").GetProperty("format").GetString());
        Assert.True(responses.GetProperty("200").GetProperty("content").TryGetProperty("application/json", out _));
        Assert.Equal("GenerationResponse", responses.GetProperty("200").GetProperty("content").GetProperty("application/json")
            .GetProperty("schema").GetProperty("$ref").GetString()!.Split('/').Last());

        var schema = root.GetProperty("components").GetProperty("schemas").GetProperty("GenerateRequest");
        Assert.Equal(new[] { "bars", "bpm", "rootNote", "scaleType", "style" },
            schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).OrderBy(s => s).ToArray());
        var properties = schema.GetProperty("properties");
        foreach (var name in new[] { "rootNote", "scaleType", "style", "bpm", "bars" })
        {
            Assert.False(string.IsNullOrWhiteSpace(properties.GetProperty(name).GetProperty("description").GetString()));
            Assert.True(properties.GetProperty(name).TryGetProperty("example", out _));
        }
        void CheckChoices(string name, string[] expected) => Assert.Equal(expected,
            properties.GetProperty(name).GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        void CheckSuggestions(string name, string[] expected) => Assert.Equal(expected,
            properties.GetProperty(name).GetProperty("x-suggested-values").EnumerateArray().Select(e => e.GetString()));
        CheckSuggestions("rootNote", ["C", "C#", "Db", "D", "D#", "Eb", "E", "F", "F#", "Gb", "G", "G#", "Ab", "A", "A#", "Bb", "B"]);
        CheckSuggestions("scaleType", ["Major", "Minor", "Dorian", "Mixolydian", "Lydian", "Phrygian",
            "Harmonic Minor", "Melodic Minor", "Major Pentatonic", "Minor Pentatonic", "Blues", "Natural Minor"]);
        var scaleDescription = properties.GetProperty("scaleType").GetProperty("description").GetString()!;
        Assert.Contains("Bright, floating, cinematic", scaleDescription);
        Assert.Contains("Dark, tense, exotic", scaleDescription);
        Assert.Contains("0, 3, 5, 6, 7, 10", scaleDescription);
        CheckSuggestions("style", ["Dreamy", "Pop", "Acid", "Rave"]);
        CheckChoices("mode", ["single", "arrangement"]);
        CheckSuggestions("progression", ChordProgressionLibrary.All.Select(p => p.DisplayName).ToArray());
        var progressionSchema = properties.GetProperty("progression");
        Assert.False(progressionSchema.TryGetProperty("enum", out _));
        var details = progressionSchema.GetProperty("x-progression-details").EnumerateArray().ToArray();
        Assert.Equal(ChordProgressionLibrary.All.Count, details.Length);
        foreach (var p in ChordProgressionLibrary.All)
        {
            var item = Assert.Single(details, item => item.GetProperty("id").GetString() == p.Id);
            Assert.Equal(p.DisplayName, item.GetProperty("name").GetString());
            Assert.Equal(string.Join(" - ", p.ChordSequence), item.GetProperty("sequence").GetString());
            Assert.Equal(p.Description, item.GetProperty("description").GetString());
            Assert.Equal(p.SuggestedUse, item.GetProperty("suggestedUse").GetString());
            Assert.Contains(p.DisplayName, progressionSchema.GetProperty("description").GetString());
        }
        foreach (var name in new[] { "mode", "progression" })
        {
            Assert.False(string.IsNullOrWhiteSpace(properties.GetProperty(name).GetProperty("description").GetString()));
            Assert.True(properties.GetProperty(name).TryGetProperty("example", out _));
        }
        Assert.Equal(30, properties.GetProperty("bpm").GetProperty("minimum").GetInt32());
        Assert.Equal(300, properties.GetProperty("bpm").GetProperty("maximum").GetInt32());
        Assert.Equal(1, properties.GetProperty("bars").GetProperty("minimum").GetInt32());
        Assert.Equal(512, properties.GetProperty("bars").GetProperty("maximum").GetInt32());
        var sectionBars = root.GetProperty("components").GetProperty("schemas").GetProperty("SectionRequest")
            .GetProperty("properties").GetProperty("bars");
        Assert.Equal(1, sectionBars.GetProperty("minimum").GetInt32());
        Assert.Equal(512, sectionBars.GetProperty("maximum").GetInt32());
        foreach (var name in new[] { "seed", "channel", "velocity", "velocityVariation", "timingVariation", "swing", "accentDownbeats", "sections" })
            Assert.False(string.IsNullOrWhiteSpace(properties.GetProperty(name).GetProperty("description").GetString()));
        foreach (var name in new[] { "movement", "groove", "mutation", "tension", "repetition" })
        {
            var property = properties.GetProperty(name);
            Assert.Equal("integer", property.GetProperty("type").GetString());
            Assert.Equal(0, property.GetProperty("minimum").GetInt32());
            Assert.Equal(100, property.GetProperty("maximum").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(property.GetProperty("description").GetString()));
            Assert.True(property.TryGetProperty("example", out _));
        }

        var examples = generate.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("examples");
        Assert.Equal(5, examples.EnumerateObject().Count());
        foreach (var name in new[] { "Acid", "Dreamy ambient", "Pop", "Rave", "Custom sections" })
        {
            using var response = await client.PostAsync("/api/generate",
                new StringContent(examples.GetProperty(name).GetProperty("value").GetRawText(), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("audio/midi", response.Content.Headers.ContentType?.MediaType);
        }
        Assert.Contains("bassline-form", await client.GetStringAsync("/swagger/index.html"));
        Assert.Contains("Generate Sample Bassline", info.GetProperty("description").GetString());
    }

    private sealed class ThrowingGenerator : IBassGenerator
    {
        public GenerationResult Generate(GeneratorSettings settings) => throw new InvalidOperationException("private input");
    }

    private sealed class CapturingGenerator : IBassGenerator
    {
        public GeneratorSettings? Settings { get; private set; }
        public GenerationResult Generate(GeneratorSettings settings)
        {
            Settings = settings;
            return new Generation.BasslineGenerator().Generate(settings);
        }
    }

    private sealed class TestAddressFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers["X-Test-Address"].ToString());
                await nextMiddleware();
            });
            next(app);
        };
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentBag<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(ConcurrentBag<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception) + exception);
    }
}
