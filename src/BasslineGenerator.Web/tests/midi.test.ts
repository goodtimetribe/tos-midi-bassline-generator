import { afterEach, describe, expect, it, vi } from "vitest";
import { MidiDevices } from "../src/midi";

class Access extends EventTarget {
  outputs = new Map<string, { id: string; state: string }>();
}

afterEach(() => vi.unstubAllGlobals());

describe("MIDI device discovery", () => {
  it("requests access, refreshes connected outputs, and follows device changes", async () => {
    const access = new Access();
    const output = { id: "circuit", state: "connected" };
    access.outputs.set(output.id, output);
    access.outputs.set("offline", { id: "offline", state: "disconnected" });
    const request = vi.fn().mockResolvedValue(access);
    vi.stubGlobal("navigator", { requestMIDIAccess: request });
    const changes = vi.fn();
    const devices = new MidiDevices(changes);
    await devices.refresh();
    expect(request).toHaveBeenCalledTimes(1);
    expect(changes).toHaveBeenLastCalledWith([output]);
    output.state = "disconnected";
    access.dispatchEvent(new Event("statechange"));
    expect(changes).toHaveBeenLastCalledWith([]);
    await devices.refresh();
    expect(request).toHaveBeenCalledTimes(1);
    devices.dispose();
    access.dispatchEvent(new Event("statechange"));
    expect(changes).toHaveBeenCalledTimes(3);
  });

  it("reports unsupported browsers and denied permission", async () => {
    vi.stubGlobal("navigator", {});
    const devices = new MidiDevices(() => {});
    await expect(devices.refresh()).rejects.toThrow("not supported");
    vi.stubGlobal("navigator", { requestMIDIAccess: vi.fn().mockRejectedValue(new Error("denied")) });
    await expect(devices.refresh()).rejects.toThrow("denied or unavailable");
  });

  it("does not subscribe or report devices after disposal during a permission request", async () => {
    let resolve!: (access: Access) => void;
    vi.stubGlobal("navigator", { requestMIDIAccess: () => new Promise<Access>((done) => { resolve = done; }) });
    const changes = vi.fn();
    const devices = new MidiDevices(changes);
    const pending = devices.refresh();
    devices.dispose();
    const access = new Access();
    resolve(access);
    await pending;
    access.dispatchEvent(new Event("statechange"));
    expect(changes).not.toHaveBeenCalled();
  });
});
