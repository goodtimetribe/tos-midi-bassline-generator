import { describe, expect, it } from "vitest";
import {
  defaultRoutingProfiles, deleteProfile, loadRoutingProfiles, putProfile, resolveOutput,
  routingProfilesKey, saveRoutingProfiles, type RoutingProfile,
} from "../src/routingProfiles";

class MemoryStorage implements Storage {
  private items = new Map<string, string>();
  get length() { return this.items.size; }
  clear() { this.items.clear(); }
  getItem(key: string) { return this.items.get(key) ?? null; }
  key(index: number) { return [...this.items.keys()][index] ?? null; }
  removeItem(key: string) { this.items.delete(key); }
  setItem(key: string, value: string) { this.items.set(key, value); }
}

describe("routing profiles", () => {
  it("creates, updates, deletes, and persists profiles and the active selection", () => {
    const storage = new MemoryStorage();
    const initial = loadRoutingProfiles(storage);
    expect(initial.profiles.map((p) => p.name)).toEqual([
      "Browser Preview", "Circuit Tracks Synth 1", "Circuit Tracks Synth 2",
      "External MIDI Channel 3", "External MIDI Channel 4",
    ]);
    const profile: RoutingProfile = {
      id: "fl", name: " FL Studio ", routing: {
        destination: { kind: "midi", deviceId: "loop", deviceName: "loopMIDI" }, channel: 0, instrument: "bass",
      },
    };
    const created = putProfile(initial, profile);
    expect(created.activeId).toBe("fl");
    expect(created.profiles.at(-1)?.name).toBe("FL Studio");
    const updated = putProfile(created, { ...profile, name: "FL Studio", routing: { ...profile.routing, channel: 2 } });
    expect(updated.profiles.at(-1)?.routing.channel).toBe(2);
    expect(updated.profiles).toHaveLength(6);
    saveRoutingProfiles(storage, updated);
    expect(loadRoutingProfiles(storage)).toEqual(updated);
    const deleted = deleteProfile(loadRoutingProfiles(storage), "fl");
    saveRoutingProfiles(storage, deleted);
    expect(loadRoutingProfiles(storage)).toEqual(deleted);
    expect(deleted.activeId).toBe("browser");
    expect(deleted.profiles).toHaveLength(5);
    expect(() => putProfile(deleted, { ...profile, name: "browser preview" })).toThrow("already exists");
    expect(() => putProfile(deleted, { ...profile, name: "  " })).toThrow("valid profile");
  });

  it("does not silently accept corrupt or unsupported saved profiles", () => {
    const storage = new MemoryStorage();
    storage.setItem(routingProfilesKey, "{");
    expect(() => loadRoutingProfiles(storage)).toThrow();
    storage.setItem(routingProfilesKey, JSON.stringify({ version: 2, activeId: null, profiles: [] }));
    expect(() => loadRoutingProfiles(storage)).toThrow("Invalid routing profiles");
    expect(storage.getItem(routingProfilesKey)).toContain('"version":2');
  });

  it("resolves only a unique intended MIDI output and leaves browser routing independent", () => {
    const profiles = defaultRoutingProfiles().profiles;
    const first = { id: "one", name: "Circuit Tracks MIDI" } as MIDIOutput;
    const second = { id: "two", name: "Circuit Tracks MIDI" } as MIDIOutput;
    expect(resolveOutput(profiles[0].routing.destination, [first])).toBeNull();
    expect(resolveOutput(profiles[2].routing.destination, [first])).toBe(first);
    expect(resolveOutput(profiles[2].routing.destination, [first, second])).toBeNull();
    expect(resolveOutput(profiles[3].routing.destination, [first])).toBeNull();
    const saved = { kind: "midi" as const, deviceId: "two", deviceName: "Circuit Tracks MIDI" };
    expect(resolveOutput(saved, [first, second])).toBe(second);
    expect(resolveOutput(saved, [first])).toBe(first);
    expect(resolveOutput(saved, [])).toBeNull();
  });
});
