using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using BasslineGenerator.Api.Models;
using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
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
        rootNote = "A", scaleType = "Minor", style = "Acid", bpm = 140, bars = 16
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

    [Theory]
    [InlineData(40, 1)]
    [InlineData(250, 256)]
    public async Task GenerateReturnsValidMidiInMemory(int bpm, int bars)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/generate", new
        {
            rootNote = "A", scaleType = "Minor", style = "Acid", bpm, bars
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("audio/midi", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("bassline.mid", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        var midi = MidiFile.Read(stream);
        Assert.Equal(MidiFileFormat.MultiTrack, midi.OriginalFormat);
        Assert.NotEmpty(midi.GetTrackChunks().SelectMany(t => t.Events).OfType<NoteOnEvent>());
        Assert.Equal((long)Math.Round(60_000_000.0 / bpm),
            midi.GetTrackChunks().First().Events.OfType<SetTempoEvent>().Single().MicrosecondsPerQuarterNote);
        Assert.Equal(bars * 4L * 480, midi.GetTrackChunks().Last().Events.Sum(e => e.DeltaTime));
    }

    [Theory]
    [InlineData("Major", "Dreamy", "A")]
    [InlineData("Minor", "Pop", "B")]
    [InlineData("Dorian", "Acid", "C")]
    [InlineData("Mixolydian", "Rave", "D")]
    [InlineData("Minor", "Acid", "E")]
    [InlineData("Minor", "Acid", "F")]
    [InlineData("Minor", "Acid", "G")]
    public async Task SupportedMusicalSettingsGenerateMidi(string scaleType, string style, string rootNote)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/generate", new { rootNote, scaleType, style, bpm = 140, bars = 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("audio/midi", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("{\"rootNote\":\"C#\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":16}", "Root note")]
    [InlineData("{\"rootNote\":\"A\\n\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":16}", "Root note")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Natural Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":16}", "Scale type")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Unknown\",\"bpm\":140,\"bars\":16}", "Style")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":39,\"bars\":16}", "BPM")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":251,\"bars\":16}", "BPM")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":0}", "Bars")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140,\"bars\":257}", "Bars")]
    [InlineData("{\"rootNote\":\"A\",\"scaleType\":\"Minor\",\"style\":\"Acid\",\"bpm\":140}", "Bars")]
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
                ["RateLimit:PermitLimit"] = "2", ["RateLimit:WindowSeconds"] = "60"
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
                ["RateLimit:PermitLimit"] = "1", ["RateLimit:WindowSeconds"] = "60"
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
            rootNote = "A", scaleType = "Minor", style = "private input", bpm = 140, bars = 16
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

    [Fact]
    public async Task SwaggerIsDevelopmentOnlyAndDocumentsMidi()
    {
        await using var development = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = development.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var generate = document.RootElement.GetProperty("paths").GetProperty("/api/generate").GetProperty("post");
        Assert.True(generate.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
        Assert.True(generate.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("audio/midi", out _));
        Assert.False(generate.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("application/json", out _));
        Assert.True(generate.GetProperty("responses").GetProperty("400").GetProperty("content").TryGetProperty("application/json", out _));
        Assert.False(generate.GetProperty("responses").GetProperty("400").GetProperty("content").TryGetProperty("audio/midi", out _));

        await using var production = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var productionClient = production.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await productionClient.GetAsync("/swagger/index.html")).StatusCode);
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
        var generate = paths.GetProperty("/api/generate").GetProperty("post");
        Assert.Equal("System", health.GetProperty("tags")[0].GetString());
        Assert.Equal("Generation", presets.GetProperty("tags")[0].GetString());
        Assert.Equal("Generation", generate.GetProperty("tags")[0].GetString());
        Assert.Contains("in memory", generate.GetProperty("description").GetString());
        foreach (var operation in new[] { health, presets, generate })
        {
            Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
            foreach (var status in new[] { "200", "429", "500" })
                Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
        }
        var responses = generate.GetProperty("responses");
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.Equal("binary", responses.GetProperty("200").GetProperty("content").GetProperty("audio/midi")
            .GetProperty("schema").GetProperty("format").GetString());

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
        CheckChoices("rootNote", ["A", "B", "C", "D", "E", "F", "G"]);
        CheckChoices("scaleType", ["Major", "Minor", "Dorian", "Mixolydian"]);
        CheckChoices("style", ["Dreamy", "Pop", "Acid", "Rave"]);
        Assert.Equal(40, properties.GetProperty("bpm").GetProperty("minimum").GetInt32());
        Assert.Equal(250, properties.GetProperty("bpm").GetProperty("maximum").GetInt32());
        Assert.Equal(1, properties.GetProperty("bars").GetProperty("minimum").GetInt32());
        Assert.Equal(256, properties.GetProperty("bars").GetProperty("maximum").GetInt32());

        var examples = generate.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("examples");
        Assert.Equal(4, examples.EnumerateObject().Count());
        foreach (var name in new[] { "Acid", "Dreamy ambient", "Pop", "Rave" })
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
