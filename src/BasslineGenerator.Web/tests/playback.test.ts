import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { initialRequest } from "../src/domain";
import { PlaybackTransport } from "../src/playback";
import type { GenerationPackage } from "../src/types";

class Parameter {
  value = 0;
  setValueAtTime = vi.fn((value: number, _time: number) => { this.value = value; });
  linearRampToValueAtTime = vi.fn();
}

class Node {
  connect = vi.fn((node: Node) => node);
  disconnect = vi.fn();
}

class Oscillator extends Node {
  type = "";
  frequency = new Parameter();
  onended: (() => void) | null = null;
  start = vi.fn();
  stop = vi.fn();
}

class Gain extends Node { gain = new Parameter(); }
class Filter extends Node { type = ""; frequency = new Parameter(); }

class AudioContextMock {
  static instance: AudioContextMock;
  currentTime = 0;
  destination = new Node();
  oscillators: Oscillator[] = [];
  gains: Gain[] = [];
  resume = vi.fn(() => Promise.resolve());
  close = vi.fn(() => Promise.resolve());
  constructor() { AudioContextMock.instance = this; }
  createGain() { const gain = new Gain(); this.gains.push(gain); return gain; }
  createBiquadFilter() { return new Filter(); }
  createOscillator() { const oscillator = new Oscillator(); this.oscillators.push(oscillator); return oscillator; }
}

const pkg: GenerationPackage = {
  version: 1, seed: 1, statistics: null,
  effectiveConfiguration: {
    ...initialRequest, seed: 1, bpm: 120, requestedProgression: null,
    progressionId: "x", progressionName: "X", chordSequence: [],
    sections: [
      { name: "First", style: "Acid", bars: 1, behavior: { movement: 0, groove: 0, mutation: 0, tension: 0, repetition: 0 } },
      { name: "Second", style: "Acid", bars: 1, behavior: { movement: 0, groove: 0, mutation: 0, tension: 0, repetition: 0 } },
    ],
  },
  notes: [
    { note: "C2", midiNote: 36, startBeat: 4, durationBeats: 1, velocity: 127 },
    { note: "A1", midiNote: 33, startBeat: 0, durationBeats: 2, velocity: 64 },
  ],
};

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal("AudioContext", AudioContextMock);
});
afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("preview transport", () => {
  it("schedules sorted notes against the audio clock and finishes after all sections", async () => {
    const transport = new PlaybackTransport(() => {});
    transport.load(pkg);
    await transport.play();
    const context = AudioContextMock.instance;
    expect(context.oscillators).toHaveLength(1);
    expect(context.oscillators[0].frequency.value).toBeCloseTo(55);
    expect(context.oscillators[0].start).toHaveBeenCalledWith(0);
    expect(context.oscillators[0].stop).toHaveBeenCalledWith(1);
    expect(context.gains[1].gain.linearRampToValueAtTime).toHaveBeenCalledWith(64 / 127 * 0.22, 0.01);
    context.currentTime = 1.9;
    vi.advanceTimersByTime(25);
    expect(context.oscillators).toHaveLength(2);
    expect(context.oscillators[1].start).toHaveBeenCalledWith(2);
    context.currentTime = 3;
    vi.advanceTimersByTime(25);
    expect(transport.state).toBe("playing");
    context.currentTime = 4;
    vi.advanceTimersByTime(25);
    expect(transport.state).toBe("stopped");
    expect(transport.beat).toBe(0);
    transport.dispose();
  });

  it("pauses and resumes sustained notes, adjusts volume, and resets on restart and load", async () => {
    const transport = new PlaybackTransport(() => {});
    transport.load(pkg);
    transport.setVolume(75);
    await transport.play();
    const context = AudioContextMock.instance;
    transport.setVolume(30);
    expect(context.gains[0].gain.setValueAtTime).toHaveBeenLastCalledWith(0.3, 0);
    context.currentTime = 0.25;
    transport.pause();
    expect(transport.beat).toBe(0.5);
    expect(context.oscillators[0].disconnect).not.toHaveBeenCalled();
    expect(context.gains[1].disconnect).toHaveBeenCalled();
    context.currentTime = 1;
    await transport.play();
    expect(context.oscillators[1].start).toHaveBeenCalledWith(1);
    expect(context.oscillators[1].stop).toHaveBeenCalledWith(1.75);
    context.currentTime = 1.5;
    await transport.restart();
    expect(transport.beat).toBe(0);
    expect(context.oscillators[2].start).toHaveBeenCalledWith(1.5);
    transport.load(pkg);
    expect(transport.state).toBe("stopped");
    expect(transport.beat).toBe(0);
    transport.dispose();
  });

  it("does not start after a pending audio resume is stopped", async () => {
    const transport = new PlaybackTransport(() => {});
    transport.load(pkg);
    const first = transport.play();
    await first;
    transport.stop();
    const context = AudioContextMock.instance;
    let resume!: () => void;
    context.resume.mockImplementationOnce(() => new Promise<void>((resolve) => { resume = resolve; }));
    const pending = transport.play();
    transport.stop();
    resume();
    await pending;
    expect(transport.state).toBe("stopped");
    expect(context.oscillators).toHaveLength(1);
    transport.dispose();
  });

  it("only schedules the look-ahead window and silences future scheduled notes on stop", async () => {
    const transport = new PlaybackTransport(() => {});
    transport.load({
      ...pkg,
      notes: Array.from({ length: 10000 }, (_, i) => ({
        note: "A1", midiNote: 33, startBeat: i / 4, durationBeats: 0.2, velocity: 100,
      })),
    });
    await transport.play();
    const context = AudioContextMock.instance;
    expect(context.oscillators).toHaveLength(2);
    expect(context.oscillators[1].start).toHaveBeenCalledWith(0.125);
    transport.stop();
    expect(context.gains[2].disconnect).toHaveBeenCalled();
    expect(context.oscillators[1].stop).toHaveBeenLastCalledWith();
    transport.dispose();
  });
});
