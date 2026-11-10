export const dimensions = ["movement", "groove", "mutation", "tension", "repetition"] as const;
export type Dimension = (typeof dimensions)[number];
export type Behavior = Record<Dimension, number>;
export type Overrides = Record<Dimension, number | null>;

export interface Section {
  name: string;
  style: string;
  bars: number;
  behavior: Behavior;
}

export interface Configuration extends Overrides {
  seed: number;
  rootNote: string;
  scaleType: string;
  style: string;
  mode: "single" | "arrangement" | "custom";
  bpm: number;
  bars: number;
  requestedProgression: string | null;
  progressionId: string;
  progressionName: string;
  chordSequence: string[];
  channel: number;
  velocity: number;
  velocityVariation: number;
  timingVariation: number;
  swing: number;
  accentDownbeats: boolean;
  sections: Section[];
}

export interface NoteData {
  note: string;
  midiNote: number;
  startBeat: number;
  durationBeats: number;
  velocity: number;
}

export interface Statistics {
  noteCount: number;
  uniqueNotes: number;
  lowestNote: string | null;
  highestNote: string | null;
  averageNoteLengthBeats: number;
  averageVelocity: number;
  octaveJumps: number;
  sectionCount: number;
  arrangementBars: number;
  arrangementBeats: number;
}

export interface GenerationPackage {
  version: number;
  engineVersion?: string | null;
  seed: number;
  effectiveConfiguration: Configuration;
  statistics: Statistics | null;
  notes: NoteData[] | null;
}

export interface Progression {
  id: string;
  name: string;
  description: string;
  chordSequence: string[];
  mood: string;
  suggestedUse: string;
}

export interface RequestSection {
  name: string;
  style: string;
  bars: number;
}

export interface GenerateRequest extends Overrides {
  rootNote: string;
  scaleType: string;
  style: string;
  bpm: number;
  bars: number;
  mode: "single" | "arrangement";
  sections?: RequestSection[];
  progression: string | null;
  seed: number | null;
  channel: number;
  velocity: number;
  velocityVariation: number;
  timingVariation: number;
  swing: number;
  accentDownbeats: boolean;
}
