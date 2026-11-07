using BasslineGenerator.Api.Models;
using BasslineGenerator.Interfaces;
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
        SetChoices(schema.Properties["scaleType"], ["Major", "Minor", "Dorian", "Mixolydian"]);
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
            ["Acid"] = Example("Acid bassline", "A", "Minor", "Acid", 140, 16),
            ["Dreamy ambient"] = Example("Dreamy ambient", "D", "Dorian", "Dreamy", 95, 32),
            ["Pop"] = Example("Pop bassline", "C", "Major", "Pop", 120, 16),
            ["Rave"] = Example("Rave bassline", "E", "Minor", "Rave", 145, 64)
        };
    }

    private static OpenApiExample Example(string summary, string root, string scale, string style, int bpm, int bars) => new()
    {
        Summary = summary,
        Value = new OpenApiObject
        {
            ["rootNote"] = new OpenApiString(root),
            ["scaleType"] = new OpenApiString(scale),
            ["style"] = new OpenApiString(style),
            ["bpm"] = new OpenApiInteger(bpm),
            ["bars"] = new OpenApiInteger(bars)
        }
    };
}
