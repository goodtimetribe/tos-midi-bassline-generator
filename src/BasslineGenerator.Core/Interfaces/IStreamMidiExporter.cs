using BasslineGenerator.Models;

namespace BasslineGenerator.Interfaces;

public interface IStreamMidiExporter
{
    void Write(GenerationResult result, Stream stream);
}
