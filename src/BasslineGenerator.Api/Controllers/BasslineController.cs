using BasslineGenerator.Api.Models;
using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using Microsoft.AspNetCore.Mvc;

namespace BasslineGenerator.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class BasslineController(
    IBassGenerator generator,
    IStreamMidiExporter exporter,
    IEnumerable<IPreset> presets,
    ILogger<BasslineController> logger) : ControllerBase
{
    /// <summary>Returns the API health and version.</summary>
    [HttpGet("health")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Health() => Ok(new HealthResponse("healthy", "1.0.0"));

    /// <summary>Lists available bassline styles.</summary>
    [HttpGet("presets")]
    [ProducesResponseType<string[]>(StatusCodes.Status200OK)]
    public ActionResult<string[]> Presets() => Ok(presets.Select(p => p.World.ToString()).ToArray());

    /// <summary>Generates and downloads a MIDI bassline without storing it.</summary>
    [HttpPost("generate")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "audio/midi")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests, "application/json")]
    public IActionResult Generate([FromBody] GenerateRequest request)
    {
        var preset = presets.FirstOrDefault(p => string.Equals(p.World.ToString(), request.Style, StringComparison.Ordinal));
        if (preset is null)
        {
            logger.LogWarning("Request validation failed.");
            return BadRequest(new ErrorResponse("Style must be one of the available presets."));
        }

        var settings = new GeneratorSettings
        {
            RootNote = request.RootNote!,
            ScaleType = request.ScaleType!,
            Style = preset.World,
            BPM = request.Bpm!.Value,
            Bars = request.Bars!.Value
        };
        // The result and stream are request-local; no MIDI or settings are persisted.
        var result = generator.Generate(settings);
        using var stream = new MemoryStream();
        exporter.Write(result, stream);
        logger.LogInformation("Generation succeeded.");
        return File(stream.ToArray(), "audio/midi", "bassline.mid");
    }
}
