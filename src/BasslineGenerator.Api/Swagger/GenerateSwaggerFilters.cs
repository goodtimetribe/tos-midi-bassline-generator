using BasslineGenerator.Api.Models;
using BasslineGenerator.Interfaces;
using BasslineGenerator.MusicTheory;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BasslineGenerator.Api.Swagger;

public sealed class GenerateRequestSchemaFilter(IEnumerable<IPreset> presets) : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(GenerateRequest)) return;

        schema.AdditionalPropertiesAllowed = true;
        SetChoices(schema.Properties["rootNote"], ["A", "B", "C", "D", "E", "F", "G"]);
        var scale = schema.Properties["scaleType"];
        SetChoices(scale, ScaleLibrary.All.Select(s => s.Name));
        scale.Description = "Scale type (case-sensitive):\n\n" + string.Join("\n\n",
            ScaleLibrary.All.Select(s => $"- **{s.Name}**: {s.Mood} Intervals: {string.Join(", ", s.Intervals)}."));
        SetChoices(schema.Properties["style"], presets.Select(p => p.World.ToString()));
    }

    private static void SetChoices(OpenApiSchema schema, IEnumerable<string> values)
    {
        schema.Enum = values.Select(value => (IOpenApiAny)new OpenApiString(value)).ToList();
    }
}

public sealed class GenerateExamplesFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.Name != "Generate" || context.MethodInfo.DeclaringType != typeof(Controllers.BasslineController))
            return;

        var json = operation.RequestBody.Content["application/json"];
        operation.Responses["200"].Content["audio/midi"].Schema = new OpenApiSchema { Type = "string", Format = "binary" };
        json.Examples = new Dictionary<string, OpenApiExample>
        {
            ["Acid"] = Example("Phrygian acid bassline with overrides", "A", "Phrygian", "Acid", 140, 16, true),
            ["Dreamy ambient"] = Example("Lydian dreamy ambient", "D", "Lydian", "Dreamy", 95, 32),
            ["Pop"] = Example("Major pentatonic pop bassline", "C", "Major Pentatonic", "Pop", 120, 16),
            ["Rave"] = Example("Harmonic minor rave bassline", "E", "Harmonic Minor", "Rave", 145, 64)
        };
    }

    private static OpenApiExample Example(string summary, string root, string scale, string style, int bpm, int bars, bool overrides = false)
    {
        var value = new OpenApiObject
        {
            ["rootNote"] = new OpenApiString(root),
            ["scaleType"] = new OpenApiString(scale),
            ["style"] = new OpenApiString(style),
            ["bpm"] = new OpenApiInteger(bpm),
            ["bars"] = new OpenApiInteger(bars)
        };
        if (overrides)
        {
            value["movement"] = new OpenApiInteger(70);
            value["groove"] = new OpenApiInteger(80);
            value["mutation"] = new OpenApiInteger(35);
            value["tension"] = new OpenApiInteger(65);
            value["repetition"] = new OpenApiInteger(85);
        }
        return new OpenApiExample { Summary = summary, Value = value };
    }
}
