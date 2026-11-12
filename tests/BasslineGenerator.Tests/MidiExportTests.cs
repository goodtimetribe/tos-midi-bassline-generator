using BasslineGenerator.Export;
using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;

namespace BasslineGenerator.Tests;

public class MidiExportTests
{
    [Fact]
    public void WritesToCallerOwnedStream()
    {
        var result = WorldSequencingTests.CreateGenerator().Generate(new GeneratorSettings { Seed = 1 });
        IStreamMidiExporter exporter = new MidiExporter();
        using var stream = new MemoryStream();
        exporter.Write(result, stream);
        Assert.True(stream.CanWrite);
        stream.Position = 0;
        Assert.Equal(MidiFileFormat.MultiTrack, MidiFile.Read(stream).OriginalFormat);
    }

    [Fact]
    public void WritesTypeOneTempoChannelAndLength()
    {
        var settings = new GeneratorSettings { Bars = 4, BPM = 120, Channel = 9, Velocity = 90, Groove = 0, Seed = 1 };
        var result = WorldSequencingTests.CreateGenerator().Generate(settings);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mid");
        try
        {
            IMidiExporter exporter = new MidiExporter();
            exporter.Save(result, path);
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0, 1 }, bytes[8..10]);
            var file = MidiFile.Read(path);
            Assert.Equal(MidiFileFormat.MultiTrack, file.OriginalFormat);
            Assert.Equal(MusicConstants.Ppq, Assert.IsType<TicksPerQuarterNoteTimeDivision>(file.TimeDivision).TicksPerQuarterNote);
            var tracks = file.GetTrackChunks().ToArray();
            Assert.Equal(2, tracks.Length);
            Assert.Equal(500000, tracks[0].Events.OfType<SetTempoEvent>().Single().MicrosecondsPerQuarterNote);
            Assert.All(tracks[1].Events.OfType<NoteOnEvent>(), e =>
            {
                Assert.Equal(9, (int)e.Channel);
                Assert.Equal(90, (int)e.Velocity);
            });
            Assert.Equal(4L * MusicConstants.TicksPerBar, tracks[1].Events.Sum(e => e.DeltaTime));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void BassOnlyFactoryOutputRemainsByteIdentical()
    {
        BassNote[] notes =
        [
            new BassNote(36, 0, MusicConstants.Ppq, 90),
            new BassNote(43, MusicConstants.Ppq, MusicConstants.Ppq, 80)
        ];

        var actual = Write(new MidiFileFactory().Create(notes, 120, 9, 1));
        var expected = Write(CreateLegacyBassMidiFile(notes, 120, 9, 1));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CombinedFactoryOutputHasConductorBassAndChordTracks()
    {
        var bassNotes = new[] { new BassNote(36, 0, MusicConstants.TicksPerBar, 100) };
        BassNote[] chordNotes =
        [
            new BassNote(57, 0, MusicConstants.TicksPerBar, 90),
            new BassNote(60, 0, MusicConstants.TicksPerBar, 90),
            new BassNote(64, 0, MusicConstants.TicksPerBar, 90)
        ];

        var file = ReadWritten(new MidiFileFactory().Create(bassNotes, 0, chordNotes, 1, 120, 1));
        var tracks = file.GetTrackChunks().ToArray();

        Assert.Equal(MidiFileFormat.MultiTrack, file.OriginalFormat);
        Assert.Equal(3, tracks.Length);
        Assert.Equal(500000, tracks[0].Events.OfType<SetTempoEvent>().Single().MicrosecondsPerQuarterNote);
        Assert.Equal(4, tracks[0].Events.OfType<TimeSignatureEvent>().Single().Numerator);
        Assert.All(tracks[1].Events.OfType<NoteOnEvent>(), note => Assert.Equal(0, (int)note.Channel));
        Assert.All(tracks[2].Events.OfType<NoteOnEvent>(), note => Assert.Equal(1, (int)note.Channel));
        Assert.Single(tracks[1].Events.OfType<NoteOnEvent>());
        Assert.Equal(3, tracks[2].Events.OfType<NoteOnEvent>().Count());
        Assert.All(tracks.Skip(1), track => Assert.Equal(MusicConstants.TicksPerBar, track.Events.Sum(@event => @event.DeltaTime)));
    }

    [Fact]
    public void SaveNewPreservesExistingFile()
    {
        var result = WorldSequencingTests.CreateGenerator().Generate(new GeneratorSettings { Seed = 1 });
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mid");
        try
        {
            IMidiExporter exporter = new MidiExporter();
            exporter.SaveNew(result, path);
            var original = File.ReadAllBytes(path);
            Assert.Equal(MidiFileFormat.MultiTrack, MidiFile.Read(path).OriginalFormat);

            Assert.Throws<IOException>(() => exporter.SaveNew(result.MidiFile, path));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void HumanizedNotesStayOrderedAndInsideSong()
    {
        var settings = new GeneratorSettings { Bars = 8, Seed = 8, TimingVariation = 30, VelocityVariation = 127, Swing = 0.5, AccentDownbeats = true };
        var notes = WorldSequencingTests.CreateGenerator().Generate(settings).Notes;
        Assert.All(notes, n => { Assert.InRange(n.Velocity, 1, 127); Assert.True(n.Length > 0); });
        for (var i = 1; i < notes.Count; i++) Assert.True(notes[i - 1].Start + notes[i - 1].Length <= notes[i].Start);
        Assert.True(notes[^1].Start + notes[^1].Length <= 8L * MusicConstants.TicksPerBar);
    }

    private static byte[] Write(MidiFile file)
    {
        using var stream = new MemoryStream();
        file.Write(stream, MidiFileFormat.MultiTrack);
        return stream.ToArray();
    }

    private static MidiFile ReadWritten(MidiFile file)
    {
        using var stream = new MemoryStream(Write(file));
        return MidiFile.Read(stream);
    }

    private static MidiFile CreateLegacyBassMidiFile(IReadOnlyList<BassNote> notes, int bpm, int channel, int bars)
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
        foreach (var item in events.OrderBy(@event => @event.Time).ThenBy(@event => @event.Order))
        {
            item.Event.DeltaTime = item.Time - previous;
            track.Events.Add(item.Event);
            previous = item.Time;
        }
        track.Events.Add(new MarkerEvent("End") { DeltaTime = Math.Max(0, (long)bars * MusicConstants.TicksPerBar - previous) });
        return new MidiFile(conductor, track) { TimeDivision = new TicksPerQuarterNoteTimeDivision(MusicConstants.Ppq) };
    }
}
