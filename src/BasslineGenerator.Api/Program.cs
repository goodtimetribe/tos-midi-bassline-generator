using System.Threading.RateLimiting;
using BasslineGenerator.Api.Configuration;
using BasslineGenerator.Api.Models;
using BasslineGenerator.Api.Swagger;
using BasslineGenerator.Export;
using BasslineGenerator.Generation;
using BasslineGenerator.Interfaces;
using BasslineGenerator.Presets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Framework request logging can include request data or addresses; retain only our fixed messages.
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.None);
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("Cors"));
builder.Services.AddOptions<RateLimitSettings>()
    .Bind(builder.Configuration.GetSection("RateLimit"))
    .Validate(s => s.PermitLimit > 0 && s.WindowSeconds > 0, "Rate limits must be positive.")
    .ValidateOnStart();
var cors = builder.Configuration.GetSection("Cors").Get<CorsSettings>() ?? new CorsSettings();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(cors.AllowedOrigins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("X-Generation-Seed")));

builder.Services.AddSingleton<IPreset, DreamyPreset>();
builder.Services.AddSingleton<IPreset, PopPreset>();
builder.Services.AddSingleton<IPreset, AcidPreset>();
builder.Services.AddSingleton<IPreset, RavePreset>();
builder.Services.AddTransient<IRhythmGenerator, RhythmGenerator>();
builder.Services.AddTransient<BassMovementGenerator>();
builder.Services.AddTransient<SectionGenerator>();
builder.Services.AddTransient<MotifGenerator>();
builder.Services.AddTransient<MidiFileFactory>();
builder.Services.AddTransient<IBassGenerator, BasslineGenerator.Generation.BasslineGenerator>();
builder.Services.AddTransient<IMidiExporter, MidiExporter>();
builder.Services.AddTransient<IStreamMidiExporter, MidiExporter>();

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Validation").LogWarning("Request validation failed.");
        // Never echo parser errors or supplied values back to logs or responses.
        var error = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .Select(entry => entry.Key.ToLowerInvariant() switch
            {
                "rootnote" => "Root note must be a supported note name.",
                "scaletype" => "Scale type must be one of the documented scale types.",
                "style" => "Style is required and must be an available preset.",
                "bpm" => "BPM must be an integer between 30 and 300.",
                "bars" => "Bars must be an integer between 1 and 512.",
                "mode" => "Mode must be single or arrangement.",
                "progression" => "Progression must be one of the documented progression names.",
                "sections" => "Sections must contain 1–512 bars each and at most 4096 bars total, and cannot be combined with arrangement mode.",
                "seed" => "Seed must be a signed 32-bit integer.",
                "channel" => "Channel must be an integer between 0 and 15.",
                "velocity" => "Velocity must be an integer between 1 and 127.",
                "velocityvariation" => "Velocity variation must be an integer between 0 and 127.",
                "timingvariation" => "Timing variation must be an integer between 0 and 30.",
                "swing" => "Swing must be between 0 and 0.5.",
                "accentdownbeats" => "Accent downbeats must be a boolean.",
                "movement" or "groove" or "mutation" or "tension" or "repetition"
                    => "Musical dimensions must be integers between 0 and 100.",
                _ => "Invalid request body or field value."
            }).FirstOrDefault() ?? "Invalid request body or field value.";
        return new BadRequestObjectResult(new ErrorResponse(error));
    };
});

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;
        // IP addresses are used only as transient, in-memory rate-limit keys, never logged or saved.
        var key = context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new ErrorResponse("Too many requests."), token);
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bassline Generator API",
        Version = typeof(Program).Assembly.GetName().Version!.ToString(3),
        Description = "Privacy-focused MIDI bassline generation service.\n\n" +
            "Features: in-memory MIDI generation, no user accounts, no file persistence, Swagger documentation, and a REST API.\n\n" +
            "## Generate Sample Bassline\n\n" +
            "Use **Generation → POST /api/generate** and select **Try it out**. Choose a preset example, " +
            "adjust the musical settings, and execute the request to download a MIDI file. " +
            "The example selector includes Dreamy, Pop, Acid, and Rave.",
        Contact = new OpenApiContact { Name = "GitHub repository", Url = new Uri("https://github.com/goodtimetribe/tos-midi-bassline-generator") },
        License = new OpenApiLicense { Name = "GPL-3.0", Url = new Uri("https://www.gnu.org/licenses/gpl-3.0.html") }
    });
    options.TagActionsBy(api => [api.ActionDescriptor.RouteValues["action"] == "Health" ? "System" : "Generation"]);
    options.SchemaFilter<GenerateRequestSchemaFilter>();
    options.OperationFilter<GenerateExamplesFilter>();
    var xml = Path.Combine(AppContext.BaseDirectory, "BasslineGenerator.Api.xml");
    if (File.Exists(xml)) options.IncludeXmlComments(xml);
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    // Disable client and intermediary caching of requests' generated content.
    context.Response.Headers.CacheControl = "no-store";
    try
    {
        app.Logger.LogInformation("Request received.");
        await next(context);
    }
    catch (Exception)
    {
        // Exception messages can contain user input, so log only a fixed message.
        app.Logger.LogError("An unhandled exception occurred.");
        if (context.Response.HasStarted) throw;
        context.Response.Clear();
        context.Response.Headers.CacheControl = "no-store";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new ErrorResponse("An unexpected error occurred."));
    }
});
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    using var script = typeof(Program).Assembly.GetManifestResourceStream("BasslineGenerator.Api.Swagger.generate-form.js")
        ?? throw new InvalidOperationException("Swagger form script is missing.");
    using var reader = new StreamReader(script);
    options.HeadContent = $"<script>{reader.ReadToEnd()}</script>";
    options.DocumentTitle = "Bassline Generator API";
    options.DisplayRequestDuration();
});
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.MapControllers();
app.Run();

public partial class Program;
