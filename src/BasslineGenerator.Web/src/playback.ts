import type { GenerationPackage, NoteData } from "./types";
import { MidiInstrument } from "./midi";

export type PlaybackState = "stopped" | "playing" | "paused";

// The transport owns timing; the instrument only needs a note and an audio time.
export interface PreviewInstrument {
  play(note: NoteData, start: number, duration: number): void;
  silence(): void;
}

class BassInstrument implements PreviewInstrument {
  private voices = new Set<{ oscillator: OscillatorNode; gain: GainNode }>();

  constructor(private context: AudioContext, private output: AudioNode) {}

  play(note: NoteData, start: number, duration: number) {
    const oscillator = this.context.createOscillator();
    const filter = this.context.createBiquadFilter();
    const gain = this.context.createGain();
    const voice = { oscillator, gain };
    const attack = Math.min(0.01, duration / 3);
    const release = Math.min(0.04, duration / 3);
    const level = note.velocity / 127 * 0.22;
    oscillator.type = "sawtooth";
    oscillator.frequency.value = 440 * Math.pow(2, (note.midiNote - 69) / 12);
    filter.type = "lowpass";
    filter.frequency.value = 700;
    gain.gain.setValueAtTime(0, start);
    gain.gain.linearRampToValueAtTime(level, start + attack);
    gain.gain.setValueAtTime(level, start + duration - release);
    gain.gain.linearRampToValueAtTime(0, start + duration);
    oscillator.connect(filter).connect(gain).connect(this.output);
    this.voices.add(voice);
    oscillator.onended = () => {
      oscillator.disconnect(); filter.disconnect(); gain.disconnect();
      this.voices.delete(voice);
    };
    oscillator.start(start);
    oscillator.stop(start + duration);
  }

  silence() {
    for (const { oscillator, gain } of this.voices) {
      gain.disconnect();
      oscillator.stop();
    }
    this.voices.clear();
  }
}

export class PlaybackTransport {
  private context: AudioContext | null = null;
  private output: GainNode | null = null;
  private instrument: PreviewInstrument | null = null;
  private notes: NoteData[] = [];
  private secondsPerBeat = 0;
  private duration = 0;
  private offset = 0;
  private startedAt = 0;
  private nextNote = 0;
  private timer: ReturnType<typeof setInterval> | null = null;
  private revision = 0;
  private volume = 75;
  private midiOutput: MIDIOutput | null = null;
  private midiChannel = 0;
  state: PlaybackState = "stopped";

  constructor(private onStateChange: (state: PlaybackState) => void, private onError: (message: string) => void = () => {}) {}

  setDestination(output: MIDIOutput | null) {
    this.setRouting(output, this.midiChannel);
  }

  setRouting(output: MIDIOutput | null, channel: number) {
    if (!Number.isInteger(channel) || channel < 0 || channel > 15) throw new RangeError("MIDI channel must be between 0 and 15.");
    if (this.midiOutput === output && this.midiChannel === channel) return;
    const playing = this.state === "playing";
    if (playing) this.offset = this.position();
    this.clearScheduled();
    this.midiOutput = output;
    this.midiChannel = channel;
    this.instrument = null;
    if (playing && this.context) {
      try {
        this.beginPlayback();
      } catch {
        this.clearScheduled();
        this.setState("paused");
        this.onError(output ? "MIDI playback failed. Check the output device and try again."
          : "Audio preview failed. Check your browser audio settings and try again.");
      }
    }
  }

  setMidiChannel(channel: number) {
    if (!Number.isInteger(channel) || channel < 0 || channel > 15) throw new RangeError("MIDI channel must be between 0 and 15.");
    this.midiChannel = channel;
    if (this.instrument instanceof MidiInstrument) this.instrument.setChannel(channel);
  }

  load(pkg: GenerationPackage) {
    this.stop();
    this.notes = [...(pkg.notes ?? [])].sort((a, b) => a.startBeat - b.startBeat);
    this.secondsPerBeat = 60 / pkg.effectiveConfiguration.bpm;
    const bars = pkg.effectiveConfiguration.sections.reduce((sum, section) => sum + section.bars, 0);
    const lastNote = this.notes.reduce((end, note) => Math.max(end, note.startBeat + note.durationBeats), 0);
    this.duration = Math.max(bars * 4, lastNote) * this.secondsPerBeat;
  }

  setVolume(volume: number) {
    this.volume = volume;
    if (this.output && this.context) this.output.gain.setValueAtTime(volume / 100, this.context.currentTime);
  }

  get beat() {
    if (!this.secondsPerBeat) return 0;
    return this.position() / this.secondsPerBeat;
  }

  private position() {
    return this.state === "playing" && this.context
      ? Math.min(this.duration, this.offset + this.context.currentTime - this.startedAt)
      : this.offset;
  }

  async play() {
    if (this.state === "playing" || !this.duration) return;
    const revision = ++this.revision;
    if (!this.context) {
      this.context = new AudioContext();
      this.output = this.context.createGain();
      this.output.gain.value = this.volume / 100;
      this.output.connect(this.context.destination);
    }
    await this.context.resume();
    if (revision !== this.revision) return;
    if (this.midiOutput && this.midiOutput.state !== "connected") {
      throw new Error("MIDI output disconnected.");
    }
    try {
      this.beginPlayback();
    } catch (error) {
      this.stop();
      throw error;
    }
  }

  private beginPlayback() {
    if (!this.context || !this.output) throw new Error("Audio context is unavailable.");
    if (this.midiOutput && this.midiOutput.state !== "connected") throw new Error("MIDI output disconnected.");
    this.instrument = this.midiOutput
      ? new MidiInstrument(this.midiOutput, this.context, this.midiChannel)
      : new BassInstrument(this.context, this.output);
    this.startedAt = this.context.currentTime;
    this.nextNote = 0;
    while (this.nextNote < this.notes.length && this.notes[this.nextNote].startBeat * this.secondsPerBeat < this.offset) {
      const note = this.notes[this.nextNote++];
      const remaining = (note.startBeat + note.durationBeats) * this.secondsPerBeat - this.offset;
      if (remaining > 0) this.instrument.play(note, this.startedAt, remaining);
    }
    this.setState("playing");
    this.timer = setInterval(() => this.schedule(), 25);
    this.schedule();
  }

  pause() {
    if (this.state !== "playing") {
      ++this.revision;
      return;
    }
    this.offset = this.position();
    this.clearScheduled();
    this.setState("paused");
  }

  stop() {
    this.clearScheduled();
    this.offset = 0;
    this.setState("stopped");
  }

  async restart() {
    this.stop();
    await this.play();
  }

  dispose() {
    this.stop();
    if (this.context) void this.context.close();
    this.context = null;
  }

  private clearScheduled() {
    ++this.revision;
    if (this.timer !== null) clearInterval(this.timer);
    this.timer = null;
    try {
      this.instrument?.silence();
    } catch {
      this.onError(this.midiOutput
        ? "MIDI output could not be silenced. Check the device connection."
        : "Audio preview could not be silenced.");
    }
  }

  private setState(state: PlaybackState) {
    this.state = state;
    this.onStateChange(state);
  }

  private schedule() {
    if (!this.context || !this.instrument || this.state !== "playing") return;
    try {
      this.scheduleNotes();
    } catch {
      this.pause();
      this.onError(this.midiOutput
        ? "MIDI playback failed. Check the output device and try again."
        : "Audio preview failed. Check your browser audio settings and try again.");
    }
  }

  private scheduleNotes() {
    if (!this.context || !this.instrument) return;
    const position = this.position();
    if (position >= this.duration) {
      this.stop();
      return;
    }
    const horizon = position + 0.15;
    while (this.nextNote < this.notes.length && this.notes[this.nextNote].startBeat * this.secondsPerBeat < horizon) {
      const note = this.notes[this.nextNote++];
      const noteStart = note.startBeat * this.secondsPerBeat;
      const remaining = (note.startBeat + note.durationBeats) * this.secondsPerBeat - Math.max(position, noteStart);
      if (remaining > 0) this.instrument.play(note, this.context.currentTime + Math.max(0, noteStart - position), remaining);
    }
  }
}
