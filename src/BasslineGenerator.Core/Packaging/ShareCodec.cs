using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BasslineGenerator.Packaging;

/// <summary>
/// Deterministic, stateless URL-safe encoding of a package's reproducible core
/// (version, seed and effective configuration): canonical JSON, deflated, then base64url.
/// </summary>
public static class ShareCodec
{
    public const int MaxCodeLength = 262_144;
    private const int MaxJsonBytes = 1_048_576;

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string Encode(GenerationPackage package)
    {
        var core = new GenerationPackage(package.Version, package.Seed, package.EffectiveConfiguration);
        var json = JsonSerializer.SerializeToUtf8Bytes(core, JsonOptions);
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, true)) deflate.Write(json);
        return Base64UrlEncode(output.ToArray());
    }

    /// <summary>Decodes a share code, returning null for malformed, oversized or incompatible input.</summary>
    public static GenerationPackage? Decode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength) return null;
        try
        {
            var bytes = Base64UrlDecode(code.Trim());
            using var input = new MemoryStream(bytes);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var json = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = deflate.Read(buffer)) > 0)
            {
                if (json.Length + read > MaxJsonBytes) return null;
                json.Write(buffer, 0, read);
            }
            return JsonSerializer.Deserialize<GenerationPackage>(json.ToArray(), JsonOptions);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidDataException)
        {
            return null;
        }
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
