using BasslineGenerator.Models;

namespace BasslineGenerator.Interfaces;

public interface IBassGenerator
{
    GenerationResult Generate(GeneratorSettings settings);
}
