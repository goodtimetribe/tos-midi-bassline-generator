// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../src/App";
import { initialRequest } from "../src/domain";
import type { GenerationPackage } from "../src/types";

vi.mock("../src/Preview", () => ({ default: () => null }));

const pkg: GenerationPackage = {
  version: 1, engineVersion: "1.0.0", seed: 7, notes: [], statistics: null,
  effectiveConfiguration: {
    ...initialRequest, seed: 7, requestedProgression: null,
    progressionId: "x", progressionName: "X", chordSequence: ["i"],
    sections: [{ name: "Section", style: "Acid", bars: 8,
      behavior: { movement: 10, groove: 20, mutation: 30, tension: 40, repetition: 50 } }],
  },
};

const json = (value: unknown, status = 200) =>
  new Response(JSON.stringify(value), { status, headers: { "content-type": "application/json" } });

let container: HTMLDivElement;
let root: Root;
let fetchMock: ReturnType<typeof vi.fn>;

function button(name: string): HTMLButtonElement {
  const found = [...container.querySelectorAll("button")].find((item) => item.textContent === name);
  if (!found) throw new Error(`Button not found: ${name}`);
  return found;
}

async function generate() {
  await act(async () => {
    container.querySelector("form")!.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
  });
}

beforeEach(async () => {
  vi.stubGlobal("IS_REACT_ACT_ENVIRONMENT", true);
  fetchMock = vi.fn((path: string) => {
    if (path === "/api/presets") return Promise.resolve(json(["Acid"]));
    if (path === "/api/progressions") return Promise.resolve(json([]));
    if (path === "/api/generate/package") return Promise.resolve(json(pkg));
    throw new Error(`Unexpected request: ${path}`);
  });
  vi.stubGlobal("fetch", fetchMock);
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  await act(async () => root.render(<App />));
});

afterEach(async () => {
  await act(async () => root.unmount());
  container.remove();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("MIDI export actions", () => {
  it("disables both exports until a package is generated", async () => {
    expect(button("Download Bass MIDI").disabled).toBe(true);
    expect(button("Download Bass + Chords MIDI").disabled).toBe(true);
    await generate();
    expect(button("Download Bass MIDI").disabled).toBe(false);
    expect(button("Download Bass + Chords MIDI").disabled).toBe(false);
  });

  it("downloads combined MIDI and disables exports while rendering", async () => {
    await generate();
    let resolveResponse!: (response: Response) => void;
    fetchMock.mockImplementationOnce(() => new Promise<Response>((resolve) => { resolveResponse = resolve; }));
    const createUrl = vi.fn().mockReturnValue("blob:test");
    vi.stubGlobal("URL", Object.assign(class extends URL {}, { createObjectURL: createUrl }));
    vi.spyOn(window, "setTimeout").mockImplementation(() => 1);
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (this: HTMLAnchorElement) {
      expect(this.download).toBe("bassline-and-chords.mid");
      expect(this.href).toBe("blob:test");
    });

    await act(async () => button("Download Bass + Chords MIDI").click());
    expect(button("Download Bass MIDI").disabled).toBe(true);
    expect(button("Download Bass + Chords MIDI").disabled).toBe(true);
    expect(container.textContent).toContain("Rendering MIDI");
    expect(fetchMock).toHaveBeenLastCalledWith("/api/package/combined-midi", expect.objectContaining({
      method: "POST", body: JSON.stringify(pkg),
    }));
    await act(async () => resolveResponse(new Response("midi", { headers: { "content-type": "audio/midi" } })));
    expect(createUrl).toHaveBeenCalledOnce();
    expect(click).toHaveBeenCalledOnce();
    expect(container.textContent).toContain("MIDI download started");
    expect(button("Download Bass + Chords MIDI").disabled).toBe(false);
  });

  it("keeps the bass-only download separate", async () => {
    await generate();
    fetchMock.mockResolvedValueOnce(new Response("bass", { headers: { "content-type": "audio/midi" } }));
    vi.stubGlobal("URL", Object.assign(class extends URL {}, { createObjectURL: vi.fn().mockReturnValue("blob:bass") }));
    vi.spyOn(window, "setTimeout").mockImplementation(() => 1);
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (this: HTMLAnchorElement) {
      expect(this.download).toBe("bassline.mid");
    });
    await act(async () => button("Download Bass MIDI").click());
    expect(fetchMock).toHaveBeenLastCalledWith("/api/package/midi", expect.objectContaining({ body: JSON.stringify(pkg) }));
    expect(click).toHaveBeenCalledOnce();
  });

  it("shows a combined export failure without downloading", async () => {
    await generate();
    fetchMock.mockResolvedValueOnce(json({ error: "Package is invalid." }, 400));
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
    await act(async () => button("Download Bass + Chords MIDI").click());
    expect(container.querySelector('[role="alert"]')?.textContent).toContain("Package is invalid.");
    expect(click).not.toHaveBeenCalled();
    expect(button("Download Bass + Chords MIDI").disabled).toBe(false);
  });
});
