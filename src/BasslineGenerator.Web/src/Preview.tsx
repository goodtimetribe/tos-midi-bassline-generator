import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from "react";
import PianoRoll from "./PianoRoll";
import { MidiDevices } from "./midi";
import { PlaybackTransport, type PlaybackState } from "./playback";
import type { GenerationPackage } from "./types";

export interface PreviewHandle { stop(): void }

const volumeKey = "bassline-preview-volume";
const channelKey = "bassline-preview-midi-channel";

const Preview = forwardRef<PreviewHandle, { pkg: GenerationPackage; disabled: boolean }>(function Preview({ pkg, disabled }, ref) {
  const [state, setState] = useState<PlaybackState>("stopped");
  const [error, setError] = useState("");
  const [storageError, setStorageError] = useState(false);
  const [channelStorageError, setChannelStorageError] = useState(false);
  const [outputs, setOutputs] = useState<MIDIOutput[]>([]);
  const [discovered, setDiscovered] = useState(false);
  const [destination, setDestination] = useState("");
  const [refreshing, setRefreshing] = useState(false);
  const selectedOutput = useRef<MIDIOutput | null>(null);
  const devices = useRef<MidiDevices | null>(null);
  const [volume, setVolume] = useState(() => {
    try {
      const saved = sessionStorage.getItem(volumeKey);
      const value = saved === null ? 75 : Number(saved);
      return Number.isFinite(value) && value >= 0 && value <= 100 ? value : 75;
    } catch {
      return 75;
    }
  });
  const [channel, setChannel] = useState(() => {
    try {
      const saved = sessionStorage.getItem(channelKey);
      const value = saved === null ? 0 : Number(saved);
      return saved !== "" && Number.isInteger(value) && value >= 0 && value <= 15 ? value : 0;
    } catch {
      return 0;
    }
  });
  const [transport] = useState(() => {
    const instance = new PlaybackTransport(setState, setError);
    instance.setMidiChannel(channel);
    return instance;
  });

  useImperativeHandle(ref, () => ({ stop: () => transport.stop() }), [transport]);
  useEffect(() => { transport.load(pkg); }, [transport, pkg]);
  useEffect(() => () => transport.dispose(), [transport]);
  useEffect(() => { transport.setVolume(volume); }, [transport, volume]);
  useEffect(() => {
    const manager = new MidiDevices((available) => {
      setOutputs(available);
      setDiscovered(true);
      if (selectedOutput.current && !available.includes(selectedOutput.current)) {
        transport.setDestination(null);
        selectedOutput.current = null;
        setDestination("");
        setError("The selected MIDI output disconnected or changed. Playback stopped; select an output again.");
      }
    });
    devices.current = manager;
    return () => { manager.dispose(); devices.current = null; };
  }, [transport]);

  const refreshDevices = async () => {
    setError("");
    setRefreshing(true);
    try {
      await devices.current?.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : "MIDI devices could not be refreshed.");
    } finally {
      setRefreshing(false);
    }
  };
  const changeDestination = (id: string) => {
    const output = outputs.find((device) => device.id === id) ?? null;
    transport.setDestination(output);
    selectedOutput.current = output;
    setDestination(output?.id ?? "");
    setError("");
  };

  const changeVolume = (value: number) => {
    setVolume(value);
    transport.setVolume(value);
    try {
      sessionStorage.setItem(volumeKey, String(value));
      setStorageError(false);
    } catch {
      setStorageError(true);
    }
  };
  const changeChannel = (value: number) => {
    transport.setMidiChannel(value);
    setChannel(value);
    try {
      sessionStorage.setItem(channelKey, String(value));
      setChannelStorageError(false);
    } catch {
      setChannelStorageError(true);
    }
  };
  const start = (restart: boolean) => {
    setError("");
    void (restart ? transport.restart() : transport.play()).catch(() => {
      setError(selectedOutput.current
        ? "MIDI playback could not start. Check the output device and browser permissions."
        : "Audio preview could not start. Check your browser audio settings and try again.");
    });
  };

  return <section className="card preview" aria-label="Bassline preview">
    <div className="transport" role="group" aria-label="Playback controls">
      <div><p className="eyebrow">PREVIEW TRANSPORT</p><h2>Listen</h2></div>
      <div className="transport-buttons">
        <button type="button" className="primary" onClick={() => start(false)} disabled={disabled || state === "playing" || !pkg.notes?.length}>Play</button>
        <button type="button" onClick={() => transport.pause()} disabled={disabled || state !== "playing"}>Pause</button>
        <button type="button" onClick={() => transport.stop()}>Stop</button>
        <button type="button" onClick={() => start(true)} disabled={disabled || !pkg.notes?.length}>Restart</button>
      </div>
      <label>Output
        <select value={destination} onChange={(event) => changeDestination(event.target.value)} aria-label="Playback output">
          <option value="">Browser Synth</option>
          {outputs.map((output) => <option key={output.id} value={output.id}>{output.name || "Unnamed MIDI output"}</option>)}
        </select>
      </label>
      {destination && <label>MIDI Channel
        <select value={channel} onChange={(event) => changeChannel(Number(event.target.value))}>
          {Array.from({ length: 16 }, (_, index) => <option key={index} value={index}>{index + 1}</option>)}
        </select>
      </label>}
      <button type="button" onClick={() => void refreshDevices()} disabled={refreshing}>
        {refreshing ? "Refreshing..." : "Refresh Devices"}
      </button>
      <label className="volume">Volume <span>{volume}%</span>
        <input type="range" min="0" max="100" value={volume} aria-label="Master volume" aria-valuetext={`${volume}%`}
          disabled={!!destination} onChange={(event) => changeVolume(Number(event.target.value))} />
      </label>
      <p className="transport-state" role="status">{state === "playing" ? "Playing" : state === "paused" ? "Paused" : "Stopped"}</p>
    </div>
    {error && <p className="error" role="alert">{error}</p>}
    {discovered && outputs.length === 0 && <p className="hint" role="status">No MIDI outputs found. Connect a device or virtual port and refresh.</p>}
    {storageError && <p className="warning" role="alert">Volume could not be saved for this browser session.</p>}
    {channelStorageError && <p className="warning" role="alert">MIDI channel could not be saved for this browser session.</p>}
    <PianoRoll pkg={pkg} transport={transport} playbackState={state} />
  </section>;
});

export default Preview;
