using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using Melanchall.DryWetMidi.Core;

namespace BasslineGenerator.Export;

/// <summary>Writes generated MIDI as a type 1 file.</summary>
public sealed class MidiExporter : IMidiExporter, IStreamMidiExporter
{
    /// <summary>Writes MIDI to a caller-owned stream without closing it.</summary>
    public void Write(GenerationResult result, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(stream);
        result.MidiFile.Write(stream, MidiFileFormat.MultiTrack);
    }

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

    public void SaveNew(GenerationResult result, string path)
    {
        ArgumentNullException.ThrowIfNull(result);
        SaveNew(result.MidiFile, path);
    }

    public void SaveNew(MidiFile midiFile, string path)
    {
        ArgumentNullException.ThrowIfNull(midiFile);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        midiFile.Write(stream, MidiFileFormat.MultiTrack);
    }
}
