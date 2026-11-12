using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;

namespace BasslineGenerator.Export;

/// <summary>Builds a MIDI file with separate tempo and note tracks.</summary>
public sealed class MidiFileFactory
{
    public MidiFile Create(IReadOnlyList<BassNote> notes, int bpm, int channel, int bars)
    {
        var conductor = new TrackChunk(
            new SetTempoEvent((long)Math.Round(60_000_000.0 / bpm)),
            new TimeSignatureEvent(4, 4));
        var track = CreateNoteTrack(notes, channel, bars);
        return new MidiFile(conductor, track) { TimeDivision = new TicksPerQuarterNoteTimeDivision(MusicConstants.Ppq) };
    }

    /// <summary>Builds a Type 1 MIDI file with separate bass and chord note tracks.</summary>
    public MidiFile Create(IReadOnlyList<BassNote> bassNotes, int bassChannel,
        IReadOnlyList<BassNote> chordNotes, int chordChannel, int bpm, int bars)
    {
        var conductor = new TrackChunk(
            new SetTempoEvent((long)Math.Round(60_000_000.0 / bpm)),
            new TimeSignatureEvent(4, 4));
        var bassTrack = CreateNoteTrack(bassNotes, bassChannel, bars);
        var chordTrack = CreateNoteTrack(chordNotes, chordChannel, bars);
        return new MidiFile(conductor, bassTrack, chordTrack)
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(MusicConstants.Ppq)
        };
    }

    private static TrackChunk CreateNoteTrack(IReadOnlyList<BassNote> notes, int channel, int bars)
    {
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
        return track;
    }
}
