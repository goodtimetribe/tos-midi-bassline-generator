using Melanchall.DryWetMidi.Core;

namespace BasslineGenerator.Interfaces;

public interface IMidiExporter
{
    void Save(MidiFile midiFile, string path);
    void SaveNew(MidiFile midiFile, string path);
}
