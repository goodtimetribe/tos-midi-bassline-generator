using BasslineGenerator.Api.Models;
using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace BasslineGenerator.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class BasslineController(
    IBassGenerator generator,
    IStreamMidiExporter exporter,
    IEnumerable<IPreset> presets,
    ILogger<BasslineController> logger) : ControllerBase
{
    /// <summary>Returns API health and version information.</summary>
    /// <response code="200">The API is healthy.</response>
    /// <response code="429">The rate limit was exceeded.</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpGet("health")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status500InternalServerError)]
    public ActionResult<HealthResponse> Health() => Ok(new HealthResponse("healthy", "1.0.0"));

    /// <summary>Returns available bassline generation styles.</summary>
    /// <response code="200">The available case-sensitive style names.</response>
    /// <response code="429">The rate limit was exceeded.</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpGet("presets")]
    [ProducesResponseType<string[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status500InternalServerError)]
    public ActionResult<string[]> Presets() => Ok(presets.Select(p => p.World.ToString()).ToArray());

    /// <summary>Returns the curated chord progression library.</summary>
    /// <response code="200">Progression names, chord sequences, moods and suggested uses.</response>
    /// <response code="429">The rate limit was exceeded.</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpGet("progressions")]
    [ProducesResponseType<ProgressionResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status500InternalServerError)]
    public ActionResult<ProgressionResponse[]> Progressions() => Ok(ChordProgressionLibrary.All.Select(p =>
        new ProgressionResponse(p.Id, p.DisplayName, p.Description, p.ChordSequence, p.Mood, p.SuggestedUse)).ToArray());

    /// <summary>Generates a MIDI bassline.</summary>
    /// <remarks>Generates a MIDI file entirely in memory and returns it directly to the caller. Send Accept: application/json for the effective configuration and statistics; use its seed to reproduce the MIDI. No generated musical content is stored.</remarks>
    /// <param name="request">Musical settings for the bassline.</param>
    /// <response code="200">The generated MIDI file or effective configuration and statistics.</response>
    /// <response code="400">The request is invalid.</response>
    /// <response code="429">The rate limit was exceeded.</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpPost("generate")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK, "audio/midi")]
    [ProducesResponseType<GenerationResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests, "application/json")]
    public IActionResult Generate([FromBody] GenerateRequest request)
    {
        var preset = presets.FirstOrDefault(p => string.Equals(p.World.ToString(), request.Style?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            logger.LogWarning("Request validation failed.");
            return BadRequest(new ErrorResponse("Style must be one of the available presets."));
        }

        var settings = new GeneratorSettings
        {
            RootNote = request.RootNote!,
            ScaleType = request.ScaleType!.Trim().Equals("Natural Minor", StringComparison.OrdinalIgnoreCase)
                ? "Minor"
                : ScaleLibrary.All.First(scale => scale.Name.Equals(request.ScaleType.Trim(), StringComparison.OrdinalIgnoreCase)).Name,
            Style = preset.World,
            BPM = request.Bpm!.Value,
            Bars = request.Bars!.Value,
            Movement = request.Movement,
            Groove = request.Groove,
            Mutation = request.Mutation,
            Tension = request.Tension,
            Repetition = request.Repetition,
            Progression = request.Progression,
            Seed = request.Seed,
            IsArrangement = request.Mode == GenerationModes.Arrangement,
            Channel = request.Channel ?? 0,
            Velocity = request.Velocity ?? 100,
            VelocityVariation = request.VelocityVariation ?? 0,
            TimingVariation = request.TimingVariation ?? 0,
            Swing = request.Swing ?? 0,
            AccentDownbeats = request.AccentDownbeats ?? false,
            Sections = request.Sections is not null
                ? request.Sections.Select(section => new SongSection(section!.Name!, Enum.Parse<SongWorld>(section.Style!.Trim(), true), section.Bars)).ToArray()
                : request.Mode == GenerationModes.Arrangement
                ? Generation.BasslineGenerator.FullArrangement(request.Bars.Value)
                : null
        };
        // The result and stream are request-local; no MIDI or settings are persisted.
        var result = generator.Generate(settings);
        using var stream = new MemoryStream();
        exporter.Write(result, stream);
        var midi = stream.ToArray();
        Response.Headers["X-Generation-Seed"] = result.Configuration.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        logger.LogInformation("Generation succeeded.");
        if (Request.GetTypedHeaders().Accept?.Any(mediaType =>
                mediaType.Quality.GetValueOrDefault(1) > 0
                && string.Equals(mediaType.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase)) == true)
        {
            request.Seed ??= result.Configuration.Seed;
            return Ok(new GenerationResponse(request, result.Configuration, result.Statistics,
                result.Motif.Name, result.Progression, result.BarCount, Convert.ToHexString(SHA256.HashData(midi))));
        }
        return File(midi, "audio/midi", "bassline.mid");
    }
}
