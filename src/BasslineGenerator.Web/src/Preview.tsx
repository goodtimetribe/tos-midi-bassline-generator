import { forwardRef, useCallback, useEffect, useImperativeHandle, useRef, useState } from "react";
import PianoRoll from "./PianoRoll";
import { MidiDevices } from "./midi";
import { PlaybackTransport, type PlaybackState } from "./playback";
import {
  applyRouting, defaultRoutingProfiles, deleteProfile, loadRoutingProfiles, putProfile, resolveOutput, saveRoutingProfiles,
  routingProfilesKey, type Routing, type RoutingProfiles,
} from "./routingProfiles";
import type { GenerationPackage } from "./types";

export interface PreviewHandle { stop(): void }

const volumeKey = "bassline-preview-volume";
const channelKey = "bassline-preview-midi-channel";

function initialProfiles() {
  try {
    const data = loadRoutingProfiles(localStorage);
    let channelError = false;
    if (localStorage.getItem(routingProfilesKey) === null) {
      try {
        const saved = sessionStorage.getItem(channelKey);
        const channel = saved === null ? 0 : Number(saved);
        if (saved !== "" && Number.isInteger(channel) && channel >= 0 && channel <= 15)
          data.profiles[0].routing.channel = channel;
      } catch { channelError = true; }
    }
    return { data, error: "", channelError };
  }
  catch { return { data: defaultRoutingProfiles(), error: "Routing profiles could not be loaded. Saved profiles have not been changed.", channelError: false }; }
}

const Preview = forwardRef<PreviewHandle, { pkg: GenerationPackage; disabled: boolean }>(function Preview({ pkg, disabled }, ref) {
  const [initial] = useState(initialProfiles);
  const [profiles, setProfiles] = useState(initial.data);
  const initialProfile = initial.data.profiles.find((p) => p.id === initial.data.activeId);
  const [routing, setRouting] = useState<Routing>(() => initialProfile?.routing ?? defaultRoutingProfiles().profiles[0].routing);
  const [profileName, setProfileName] = useState(initialProfile?.name ?? "");
  const [profileError, setProfileError] = useState(initial.error);
  const [state, setState] = useState<PlaybackState>("stopped");
  const [error, setError] = useState("");
  const [storageError, setStorageError] = useState(false);
  const [outputs, setOutputs] = useState<MIDIOutput[]>([]);
  const [discovered, setDiscovered] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const devices = useRef<MidiDevices | null>(null);
  const refreshPending = useRef<MidiDevices | null>(null);
  const [volume, setVolume] = useState(() => {
    try {
      const saved = sessionStorage.getItem(volumeKey);
      const value = saved === null ? 75 : Number(saved);
      return Number.isFinite(value) && value >= 0 && value <= 100 ? value : 75;
    } catch { return 75; }
  });
  const [transport] = useState(() => new PlaybackTransport(setState, setError));
  const output = resolveOutput(routing.destination, outputs);
  const unavailable = routing.destination.kind === "midi" && !output;

  useImperativeHandle(ref, () => ({ stop: () => transport.stop() }), [transport]);
  useEffect(() => { transport.load(pkg); }, [transport, pkg]);
  useEffect(() => () => transport.dispose(), [transport]);
  useEffect(() => { transport.setVolume(volume); }, [transport, volume]);
  useEffect(() => {
    const manager = new MidiDevices((available) => {
      setOutputs(available);
      setDiscovered(true);
    });
    devices.current = manager;
    return () => { manager.dispose(); devices.current = null; };
  }, []);

  const refreshDevices = useCallback(async () => {
    const manager = devices.current;
    if (!manager || refreshPending.current === manager) return;
    refreshPending.current = manager;
    setRefreshing(true);
    try {
      await manager.refresh();
    } catch (e) {
      if (devices.current === manager)
        setError(e instanceof Error ? e.message : "MIDI devices could not be refreshed.");
    } finally {
      if (refreshPending.current === manager) {
        refreshPending.current = null;
        setRefreshing(false);
      }
    }
  }, []);

  useEffect(() => {
    if (routing.destination.kind === "midi") void refreshDevices();
  }, [routing.destination, refreshDevices]);
  useEffect(() => {
    applyRouting(transport, routing, output);
  }, [transport, output, routing]);

  const persist = (next: RoutingProfiles) => {
    setProfiles(next);
    try {
      saveRoutingProfiles(localStorage, next);
      setProfileError("");
    } catch {
      setProfileError("Routing profiles could not be saved in this browser.");
    }
  };
  const selectProfile = (id: string) => {
    const profile = profiles.profiles.find((p) => p.id === id);
    if (!profile) return;
    setRouting(profile.routing);
    setProfileName(profile.name);
    setError("");
    persist({ ...profiles, activeId: id });
  };
  const saveProfile = (update: boolean) => {
    try {
      const id = update ? profiles.activeId : crypto.randomUUID();
      if (!id) throw new Error("Select a profile to update.");
      const next = putProfile(profiles, { id, name: profileName, routing });
      persist(next);
      setProfileName(next.profiles.find((p) => p.id === id)!.name);
    } catch (e) {
      setProfileError(e instanceof Error ? e.message : "The profile could not be saved.");
    }
  };
  const removeProfile = () => {
    if (!profiles.activeId) return;
    const next = deleteProfile(profiles, profiles.activeId);
    const selected = next.profiles.find((p) => p.id === next.activeId);
    setRouting(selected?.routing ?? defaultRoutingProfiles().profiles[0].routing);
    setProfileName(selected?.name ?? "");
    setError("");
    persist(next);
  };
  const changeDestination = (id: string) => {
    const device = outputs.find((item) => item.id === id);
    if (id && !device) return;
    setRouting((old) => ({ ...old, destination: device
      ? { kind: "midi", deviceId: device.id, deviceName: device.name || null }
      : { kind: "browser" } }));
    setError("");
  };
  const changeVolume = (value: number) => {
    setVolume(value);
    transport.setVolume(value);
    try { sessionStorage.setItem(volumeKey, String(value)); setStorageError(false); }
    catch { setStorageError(true); }
  };
  const changeChannel = (value: number) => {
    setRouting((old) => ({ ...old, channel: value }));
  };
  const start = (restart: boolean) => {
    if (unavailable) {
      setError("The profile's MIDI output is unavailable. Connect it, refresh devices, or select an output.");
      return;
    }
    setError("");
    void (restart ? transport.restart() : transport.play()).catch(() => {
      setError(output
        ? "MIDI playback could not start. Check the output device and browser permissions."
        : "Audio preview could not start. Check your browser audio settings and try again.");
    });
  };

  return <section className="card preview" aria-label="Bassline preview">
    <p className="hint">Preview plays bass only. The Bass + Chords MIDI export includes a separate chord track.</p>
    <div className="routing-profiles" role="group" aria-label="Routing profiles">
      <label>Profile
        <select value={profiles.activeId ?? ""} onChange={(event) => selectProfile(event.target.value)}>
          {!profiles.activeId && <option value="">No profile selected</option>}
          {profiles.profiles.map((profile) => <option key={profile.id} value={profile.id}>{profile.name}</option>)}
        </select>
      </label>
      <label>Profile name
        <input value={profileName} onChange={(event) => setProfileName(event.target.value)} maxLength={100} />
      </label>
      <div className="transport-buttons">
        <button type="button" onClick={() => saveProfile(false)}>Save Profile</button>
        <button type="button" onClick={() => saveProfile(true)} disabled={!profiles.activeId}>Update Profile</button>
        <button type="button" onClick={removeProfile} disabled={!profiles.activeId}>Delete Profile</button>
      </div>
      <p className="hint">Routing edits apply immediately; update the profile to keep them.</p>
    </div>
    <div className="transport" role="group" aria-label="Playback controls">
      <div><p className="eyebrow">PREVIEW TRANSPORT</p><h2>Listen</h2></div>
      <div className="transport-buttons">
        <button type="button" className="primary" onClick={() => start(false)} disabled={disabled || unavailable || state === "playing" || !pkg.notes?.length}>Play</button>
        <button type="button" onClick={() => transport.pause()} disabled={disabled || state !== "playing"}>Pause</button>
        <button type="button" onClick={() => transport.stop()}>Stop</button>
        <button type="button" onClick={() => start(true)} disabled={disabled || unavailable || !pkg.notes?.length}>Restart</button>
      </div>
      <label>Output
        <select value={unavailable ? "__unavailable__" : output?.id ?? ""} onChange={(event) => changeDestination(event.target.value)} aria-label="Playback output">
          <option value="">Browser Synth</option>
          {unavailable && <option value="__unavailable__" disabled>MIDI output unavailable - select a device</option>}
          {outputs.map((device) => <option key={device.id} value={device.id}>{device.name || "Unnamed MIDI output"}</option>)}
        </select>
      </label>
      {routing.destination.kind === "midi" && <label>MIDI Channel
        <select value={routing.channel} onChange={(event) => changeChannel(Number(event.target.value))}>
          {Array.from({ length: 16 }, (_, index) => <option key={index} value={index}>{index + 1}</option>)}
        </select>
      </label>}
      <label>Preview Instrument
        <select value={routing.instrument} disabled={routing.destination.kind === "midi"} aria-label="Preview instrument">
          <option value="bass">Bass Synth</option>
        </select>
      </label>
      <button type="button" onClick={() => void refreshDevices()} disabled={refreshing}>
        {refreshing ? "Refreshing..." : "Refresh Devices"}
      </button>
      <label className="volume">Volume <span>{volume}%</span>
        <input type="range" min="0" max="100" value={volume} aria-label="Master volume" aria-valuetext={`${volume}%`}
          disabled={routing.destination.kind === "midi"} onChange={(event) => changeVolume(Number(event.target.value))} />
      </label>
      <p className="transport-state" role="status">{state === "playing" ? "Playing" : state === "paused" ? "Paused" : "Stopped"}</p>
    </div>
    {unavailable && <p className="warning" role="status">MIDI output unavailable or ambiguous. Connect the device and refresh, or select an output.</p>}
    {error && <p className="error" role="alert">{error}</p>}
    {profileError && <p className="warning" role="alert">{profileError}</p>}
    {discovered && outputs.length === 0 && <p className="hint" role="status">No MIDI outputs found. Connect a device or virtual port and refresh.</p>}
    {storageError && <p className="warning" role="alert">Volume could not be saved for this browser session.</p>}
    {initial.channelError && <p className="warning" role="alert">The previous MIDI channel could not be loaded from this browser session.</p>}
    <PianoRoll pkg={pkg} transport={transport} playbackState={state} />
  </section>;
});

export default Preview;
