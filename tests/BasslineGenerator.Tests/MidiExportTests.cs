using BasslineGenerator.Export;
using BasslineGenerator.Interfaces;
using BasslineGenerator.Models;
using BasslineGenerator.MusicTheory;
using Melanchall.DryWetMidi.Core;

namespace BasslineGenerator.Tests;

public class MidiExportTests
{
    [Fact]
    public void WritesTypeOneTempoChannelAndLength()
    {
        var settings = new GeneratorSettings { Bars = 4, BPM = 120, Channel = 9, Velocity = 90, Seed = 1 };
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
}
