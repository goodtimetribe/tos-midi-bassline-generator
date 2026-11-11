import type { NoteData } from "./types";
import type { PreviewInstrument } from "./playback";

// TypeScript's DOM definitions omit the Web MIDI output queue method.
declare global {
  interface MIDIOutput {
    clear(): void;
  }
}

export class MidiDevices {
  private access: MIDIAccess | null = null;
  private disposed = false;

  constructor(private onChange: (outputs: MIDIOutput[]) => void) {}

  async refresh() {
    if (!navigator.requestMIDIAccess) {
      throw new Error("Web MIDI is not supported in this browser. Use a compatible browser in a secure context.");
    }
    if (!this.access) {
      let access: MIDIAccess;
      try {
        access = await navigator.requestMIDIAccess();
      } catch {
        throw new Error("MIDI access was denied or unavailable. Check your browser permissions and try again.");
      }
      if (this.disposed) return;
      this.access = access;
      access.addEventListener("statechange", this.update);
    }
    this.update();
  }

  private update = () => {
    if (this.access) this.onChange(Array.from(this.access.outputs.values()).filter((output) => output.state === "connected"));
  };

  dispose() {
    this.disposed = true;
    this.access?.removeEventListener("statechange", this.update);
  }
}

export class MidiInstrument implements PreviewInstrument {
  private pitches = new Map<number, Set<number>>();

  constructor(private output: MIDIOutput, private context: AudioContext, private channel = 0) {}

  setChannel(channel: number) {
    this.channel = channel;
  }

  play(note: NoteData, start: number, duration: number) {
    if (this.output.state !== "connected") throw new Error("MIDI output disconnected.");
    const timestamp = performance.now() + Math.max(0, start - this.context.currentTime) * 1000;
    const channel = this.channel;
    if (!this.pitches.has(channel)) this.pitches.set(channel, new Set());
    this.pitches.get(channel)!.add(note.midiNote);
    this.output.send([0x90 | channel, note.midiNote, note.velocity], timestamp);
    this.output.send([0x80 | channel, note.midiNote, 0], timestamp + duration * 1000);
  }

  silence() {
    this.output.clear();
    for (const [channel, pitches] of this.pitches) {
      for (const pitch of pitches) this.output.send([0x80 | channel, pitch, 0]);
    }
    this.pitches.clear();
  }
}
