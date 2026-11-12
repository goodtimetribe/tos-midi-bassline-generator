import { afterEach, describe, expect, it, vi } from "vitest";
import { api } from "../src/api";
import { initialRequest } from "../src/domain";
import type { GenerationPackage } from "../src/types";

const pkg: GenerationPackage = {
  version: 1, engineVersion: "1.0.0", seed: 7, notes: [], statistics: null,
  effectiveConfiguration: {
    ...initialRequest, seed: 7, requestedProgression: null,
    progressionId: "x", progressionName: "X", chordSequence: ["i"], sections: [],
  },
};

afterEach(() => vi.unstubAllGlobals());

describe("API client", () => {
  it("posts share codes as JSON", async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ version: 1 }), {
      status: 200, headers: { "content-type": "application/json" },
    }));
    vi.stubGlobal("fetch", fetch);
    await api.decode("test-code");
    expect(fetch).toHaveBeenCalledWith("/api/share/decode", expect.objectContaining({
      method: "POST", body: JSON.stringify({ shareCode: "test-code" }),
    }));
  });

  it("reports rate limits without retrying", async () => {
    const fetch = vi.fn().mockResolvedValue(new Response("", { status: 429 }));
    vi.stubGlobal("fetch", fetch);
    await expect(api.presets()).rejects.toThrow("Too many requests");
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it("surfaces safe API errors", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({ error: "Package is invalid." }), { status: 400 })));
    await expect(api.decode("bad")).rejects.toThrow("Package is invalid.");
  });

  it("posts the package and returns the combined MIDI", async () => {
    const fetch = vi.fn().mockResolvedValue(new Response("midi data", {
      status: 200, headers: { "content-type": "audio/midi" },
    }));
    vi.stubGlobal("fetch", fetch);
    const blob = await api.combinedMidi(pkg);
    expect(await blob.text()).toBe("midi data");
    expect(fetch).toHaveBeenCalledWith("/api/package/combined-midi", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "audio/midi" },
      body: JSON.stringify(pkg),
    });
  });

  it("rejects combined export errors and non-MIDI responses", async () => {
    vi.stubGlobal("fetch", vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ error: "Package is invalid." }), { status: 400 }))
      .mockResolvedValueOnce(new Response("not MIDI", { headers: { "content-type": "application/json" } })));
    await expect(api.combinedMidi(pkg)).rejects.toThrow("Package is invalid.");
    await expect(api.combinedMidi(pkg)).rejects.toThrow("The server did not return a MIDI file.");
  });
});
