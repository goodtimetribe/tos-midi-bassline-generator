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
        if (context.Type == typeof(SectionRequest))
        {
            SetSuggestions(schema.Properties["style"], presets.Select(p => p.World.ToString()));
            schema.Required = new HashSet<string> { "name", "style", "bars" };
            schema.Properties["bars"].Minimum = 1;
            schema.Properties["bars"].Maximum = 512;
            return;
        }
        if (context.Type != typeof(GenerateRequest)) return;

        schema.AdditionalPropertiesAllowed = true;
        SetSuggestions(schema.Properties["rootNote"], ["C", "C#", "Db", "D", "D#", "Eb", "E", "F", "F#", "Gb", "G", "G#", "Ab", "A", "A#", "Bb", "B"]);
        var scale = schema.Properties["scaleType"];
        SetSuggestions(scale, ScaleLibrary.All.Select(s => s.Name).Append("Natural Minor"));
        scale.Description = "Scale type (case-insensitive); Natural Minor is an alias for Minor.\n\n" + string.Join("\n\n",
            ScaleLibrary.All.Select(s => $"- **{s.Name}**: {s.Mood} Intervals: {string.Join(", ", s.Intervals)}."));
        SetSuggestions(schema.Properties["style"], presets.Select(p => p.World.ToString()));
        SetChoices(schema.Properties["mode"], GenerationModes.All);
        var progression = schema.Properties["progression"];
        SetSuggestions(progression, ChordProgressionLibrary.All.Select(p => p.DisplayName));
        progression.Description = "Choose a case-sensitive name, ID, or legacy chord sequence; omit for a random progression. " +
            "Chord tones are constrained to the selected scale, so non-diatonic chords may be adjusted.\n\n" +
            string.Join("\n\n", ChordProgressionLibrary.All.Select(p =>
                $"- **{p.DisplayName}** (`{string.Join(" - ", p.ChordSequence)}`): {p.Description} Use: {p.SuggestedUse} Mood: {p.Mood}. ID: `{p.Id}`."));
        var details = new OpenApiArray();
        foreach (var p in ChordProgressionLibrary.All)
            details.Add(new OpenApiObject
            {
                ["name"] = new OpenApiString(p.DisplayName),
                ["id"] = new OpenApiString(p.Id),
                ["legacy"] = new OpenApiString(p.Name),
                ["sequence"] = new OpenApiString(string.Join(" - ", p.ChordSequence)),
                ["description"] = new OpenApiString(p.Description),
                ["suggestedUse"] = new OpenApiString(p.SuggestedUse),
                ["mood"] = new OpenApiString(p.Mood)
            });
        progression.Extensions["x-progression-details"] = details;
    }

    private static void SetChoices(OpenApiSchema schema, IEnumerable<string> values)
    {
        schema.Enum = values.Select(value => (IOpenApiAny)new OpenApiString(value)).ToList();
    }

    private static void SetSuggestions(OpenApiSchema schema, IEnumerable<string> values)
    {
        var choices = new OpenApiArray();
        foreach (var value in values) choices.Add(new OpenApiString(value));
        schema.Extensions["x-suggested-values"] = choices;
    }
}

public sealed class GenerateExamplesFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.Name != "Generate" || context.MethodInfo.DeclaringType != typeof(Controllers.BasslineController))
            return;

        var json = operation.RequestBody.Content["application/json"];
        var responseContent = operation.Responses["200"].Content;
        if (!responseContent.TryGetValue("audio/midi", out var midi))
            responseContent["audio/midi"] = midi = new OpenApiMediaType();
        midi.Schema = new OpenApiSchema { Type = "string", Format = "binary" };
        operation.Responses["200"].Headers["X-Generation-Seed"] = new OpenApiHeader
        {
            Description = "Actual signed 32-bit seed used; returned for both MIDI and JSON responses.",
            Schema = new OpenApiSchema { Type = "integer", Format = "int32" }
        };
        json.Examples = new Dictionary<string, OpenApiExample>
        {
            ["Acid"] = Example("Phrygian acid bassline with overrides", "A", "Phrygian", "Acid", 140, 16, true),
            ["Dreamy ambient"] = Example("Lydian dreamy ambient", "D", "Lydian", "Dreamy", 95, 32),
            ["Pop"] = Example("Major pentatonic full arrangement", "C", "Major Pentatonic", "Pop", 120, 16, arrangement: true),
            ["Rave"] = Example("Harmonic minor rave bassline", "E", "Harmonic Minor", "Rave", 145, 64),
            ["Custom sections"] = CustomSectionsExample()
        };
    }

    private static OpenApiExample CustomSectionsExample()
    {
        var example = Example("Seeded custom arrangement", "C#", "Minor", "Acid", 140, 4);
        var value = (OpenApiObject)example.Value;
        value["seed"] = new OpenApiInteger(12345);
        value["sections"] = new OpenApiArray
        {
            new OpenApiObject { ["name"] = new OpenApiString("Intro"), ["style"] = new OpenApiString("Dreamy"), ["bars"] = new OpenApiInteger(4) },
            new OpenApiObject { ["name"] = new OpenApiString("Drop"), ["style"] = new OpenApiString("Acid"), ["bars"] = new OpenApiInteger(8) }
        };
        return example;
    }

    private static OpenApiExample Example(string summary, string root, string scale, string style, int bpm, int bars, bool overrides = false, bool arrangement = false)
    {
        var value = new OpenApiObject
        {
            ["rootNote"] = new OpenApiString(root),
            ["scaleType"] = new OpenApiString(scale),
            ["style"] = new OpenApiString(style),
            ["bpm"] = new OpenApiInteger(bpm),
            ["bars"] = new OpenApiInteger(bars)
        };
        if (arrangement)
        {
            value["mode"] = new OpenApiString(GenerationModes.Arrangement);
            value["progression"] = new OpenApiString("Pop Anthem");
        }
        if (overrides)
        {
            value["seed"] = new OpenApiInteger(12345);
            value["movement"] = new OpenApiInteger(70);
            value["groove"] = new OpenApiInteger(80);
            value["mutation"] = new OpenApiInteger(35);
            value["tension"] = new OpenApiInteger(65);
            value["repetition"] = new OpenApiInteger(85);
        }
        return new OpenApiExample { Summary = summary, Value = value };
    }
}
