using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using Melanchall.DryWetMidi.Core;

namespace BasslineGenerator.Export;

/// <summary>Writes generated MIDI as a type 1 file.</summary>
public sealed class MidiExporter : IMidiExporter
{
    public void Save(GenerationResult result, string path)
    {
        ArgumentNullException.ThrowIfNull(result);
        Save(result.MidiFile, path);
    }

    public void Save(MidiFile midiFile, string path)
    {
        ArgumentNullException.ThrowIfNull(midiFile);
        midiFile.Write(path, true, MidiFileFormat.MultiTrack);
    }
}
