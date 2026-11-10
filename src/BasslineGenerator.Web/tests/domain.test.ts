import { describe, expect, it } from "vitest";
import { initialRequest, needsRebuild, replayWithRequest, requestFromPackage } from "../src/domain";
import type { GenerationPackage } from "../src/types";

const pkg: GenerationPackage = {
  version: 1, engineVersion: "1.0.0", seed: 7, notes: [], statistics: null,
  effectiveConfiguration: {
    ...initialRequest, seed: 7, mode: "arrangement", requestedProgression: null,
    progressionId: "x", progressionName: "X", chordSequence: ["i"],
    sections: [
      { name: "Intro", style: "Dreamy", bars: 8, behavior: { movement: 10, groove: 20, mutation: 30, tension: 40, repetition: 50 } },
      { name: "Drop", style: "Acid", bars: 8, behavior: { movement: 60, groove: 70, mutation: 80, tension: 90, repetition: 100 } },
    ],
  },
};

describe("package editing", () => {
  it("preserves section behavior and synchronizes the seed on replay", () => {
    const request = { ...requestFromPackage(pkg), seed: -42, bpm: 130, progression: "other", bars: 4 };
    const replay = replayWithRequest(pkg, request);
    expect(replay.seed).toBe(-42);
    expect(replay.effectiveConfiguration.seed).toBe(-42);
    expect(replay.effectiveConfiguration.requestedProgression).toBe("other");
    expect(replay.effectiveConfiguration.sections.map((s) => s.bars)).toEqual([4, 4]);
    expect(replay.effectiveConfiguration.sections[1].behavior).toEqual(pkg.effectiveConfiguration.sections[1].behavior);
    expect(pkg.effectiveConfiguration.sections[0].bars).toBe(8);
  });

  it("requires a fresh generation for mode and default resets", () => {
    const request = requestFromPackage(pkg);
    expect(needsRebuild(pkg, request)).toBe(false);
    expect(needsRebuild(pkg, { ...request, mode: "single" })).toBe(true);
    const overridden = { ...pkg, effectiveConfiguration: { ...pkg.effectiveConfiguration, movement: 50 } };
    expect(needsRebuild(overridden, { ...request, movement: null })).toBe(true);
  });

  it("replays custom section names and lengths without replacing behavior", () => {
    const custom: GenerationPackage = { ...pkg, effectiveConfiguration: { ...pkg.effectiveConfiguration, mode: "custom" } };
    const request = requestFromPackage(custom);
    request.sections![0] = { ...request.sections![0], name: "Opening", bars: 3 };
    expect(needsRebuild(custom, request)).toBe(false);
    const replay = replayWithRequest(custom, request);
    expect(replay.effectiveConfiguration.sections[0]).toEqual({
      ...custom.effectiveConfiguration.sections[0], name: "Opening", bars: 3,
    });
    expect(needsRebuild(custom, { ...request, sections: [...request.sections!, request.sections![0]] })).toBe(true);
    expect(needsRebuild(custom, { ...request, sections: [{ ...request.sections![0], style: "Rave" }, request.sections![1]] })).toBe(true);
  });
});
