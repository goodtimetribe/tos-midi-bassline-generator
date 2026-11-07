using Melanchall.DryWetMidi.Core;
using BasslineGenerator.Models;

namespace BasslineGenerator.Interfaces;

public interface IMidiExporter
{
    void Save(GenerationResult result, string path);
    void Save(MidiFile midiFile, string path);
    void SaveNew(GenerationResult result, string path);
    void SaveNew(MidiFile midiFile, string path);
}
