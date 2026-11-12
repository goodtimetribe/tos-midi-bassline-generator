export type RoutingDestination =
  | { kind: "browser" }
  | { kind: "midi"; deviceId: string | null; deviceName: string | null; nameContains?: string };

export interface Routing {
  destination: RoutingDestination;
  channel: number;
  instrument: "bass";
}

export interface RoutingProfile {
  id: string;
  name: string;
  routing: Routing;
}

export interface RoutingProfiles {
  version: 1;
  activeId: string | null;
  profiles: RoutingProfile[];
}

export const routingProfilesKey = "bassline-routing-profiles";

const browser = (): Routing => ({ destination: { kind: "browser" }, channel: 0, instrument: "bass" });
const midi = (channel: number, nameContains?: string): Routing => ({
  destination: { kind: "midi", deviceId: null, deviceName: null, ...(nameContains ? { nameContains } : {}) },
  channel, instrument: "bass",
});

export function defaultRoutingProfiles(): RoutingProfiles {
  return {
    version: 1, activeId: "browser",
    profiles: [
      { id: "browser", name: "Browser Preview", routing: browser() },
      { id: "circuit-1", name: "Circuit Tracks Synth 1", routing: midi(0, "Circuit Tracks") },
      { id: "circuit-2", name: "Circuit Tracks Synth 2", routing: midi(1, "Circuit Tracks") },
      { id: "midi-3", name: "External MIDI Channel 3", routing: midi(2) },
      { id: "midi-4", name: "External MIDI Channel 4", routing: midi(3) },
    ],
  };
}

function isProfile(value: unknown): value is RoutingProfile {
  if (!value || typeof value !== "object") return false;
  const p = value as Partial<RoutingProfile>;
  const r = p.routing;
  if (typeof p.id !== "string" || !p.id || typeof p.name !== "string" || !p.name.trim()
    || !r || !Number.isInteger(r.channel) || r.channel < 0 || r.channel > 15 || r.instrument !== "bass"
    || !r.destination || typeof r.destination !== "object") return false;
  const d = r.destination;
  return d.kind === "browser" || (d.kind === "midi"
    && (d.deviceId === null || typeof d.deviceId === "string")
    && (d.deviceName === null || typeof d.deviceName === "string")
    && (d.nameContains === undefined || typeof d.nameContains === "string"));
}

export function loadRoutingProfiles(storage: Storage): RoutingProfiles {
  const saved = storage.getItem(routingProfilesKey);
  if (saved === null) return defaultRoutingProfiles();
  const value: unknown = JSON.parse(saved);
  if (!value || typeof value !== "object") throw new Error("Invalid routing profiles.");
  const data = value as Partial<RoutingProfiles>;
  if (data.version !== 1 || !Array.isArray(data.profiles) || !data.profiles.every(isProfile)
    || new Set(data.profiles.map((profile) => profile.id)).size !== data.profiles.length
    || !(data.activeId === null || typeof data.activeId === "string" && data.profiles.some((p) => p.id === data.activeId)))
    throw new Error("Invalid routing profiles.");
  return { version: 1, activeId: data.activeId, profiles: data.profiles };
}

export function saveRoutingProfiles(storage: Storage, profiles: RoutingProfiles): void {
  storage.setItem(routingProfilesKey, JSON.stringify(profiles));
}

export function putProfile(data: RoutingProfiles, profile: RoutingProfile): RoutingProfiles {
  if (!isProfile(profile)) throw new Error("Enter a valid profile name and routing.");
  const name = profile.name.trim();
  if (data.profiles.some((p) => p.id !== profile.id && p.name.toLowerCase() === name.toLowerCase()))
    throw new Error("A profile with that name already exists.");
  const exists = data.profiles.some((p) => p.id === profile.id);
  return { ...data, activeId: profile.id, profiles: exists
    ? data.profiles.map((p) => p.id === profile.id ? { ...profile, name } : p)
    : [...data.profiles, { ...profile, name }] };
}

export function deleteProfile(data: RoutingProfiles, id: string): RoutingProfiles {
  const profiles = data.profiles.filter((p) => p.id !== id);
  return { ...data, profiles, activeId: data.activeId === id ? profiles[0]?.id ?? null : data.activeId };
}

export function resolveOutput(destination: RoutingDestination, outputs: MIDIOutput[]): MIDIOutput | null {
  if (destination.kind === "browser") return null;
  const byId = outputs.find((output) => destination.deviceId && output.id === destination.deviceId);
  if (byId) return byId;
  const matches = outputs.filter((output) => destination.deviceName
    ? output.name === destination.deviceName
    : destination.nameContains && output.name?.toLowerCase().includes(destination.nameContains.toLowerCase()));
  return matches.length === 1 ? matches[0] : null;
}

export function applyRouting(
  transport: { pause(): void; setRouting(output: MIDIOutput | null, channel: number): void },
  routing: Routing,
  output: MIDIOutput | null,
): void {
  if (routing.destination.kind === "midi" && !output) transport.pause();
  transport.setRouting(output, routing.channel);
}
