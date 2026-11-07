using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;

namespace BasslineGenerator.Export;

/// <summary>Builds and writes a type 1 MIDI file with separate tempo and note tracks.</summary>
public sealed class MidiExporter : IMidiExporter
{
    public MidiFile Create(IReadOnlyList<BassNote> notes, int bpm, int channel, int bars)
    {
        var conductor = new TrackChunk(
            new SetTempoEvent((long)Math.Round(60_000_000.0 / bpm)),
            new TimeSignatureEvent(4, 4));
        var events = new List<(long Time, int Order, MidiEvent Event)>();
        foreach (var note in notes)
        {
            events.Add((note.Start, 1, new NoteOnEvent((SevenBitNumber)note.Pitch, (SevenBitNumber)note.Velocity) { Channel = (FourBitNumber)channel }));
            events.Add((note.Start + note.Length, 0, new NoteOffEvent((SevenBitNumber)note.Pitch, (SevenBitNumber)0) { Channel = (FourBitNumber)channel }));
        }
        var track = new TrackChunk();
        long previous = 0;
        foreach (var item in events.OrderBy(e => e.Time).ThenBy(e => e.Order))
        {
            item.Event.DeltaTime = item.Time - previous;
            track.Events.Add(item.Event);
            previous = item.Time;
        }
        track.Events.Add(new MarkerEvent("End") { DeltaTime = Math.Max(0, (long)bars * MusicConstants.TicksPerBar - previous) });
        return new MidiFile(conductor, track) { TimeDivision = new TicksPerQuarterNoteTimeDivision(MusicConstants.Ppq) };
    }

    public void Save(MidiFile midiFile, string path) => midiFile.Write(path, true, MidiFileFormat.MultiTrack);
}
