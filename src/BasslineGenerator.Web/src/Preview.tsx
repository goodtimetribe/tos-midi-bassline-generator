import { forwardRef, useEffect, useImperativeHandle, useState } from "react";
import PianoRoll from "./PianoRoll";
import { PlaybackTransport, type PlaybackState } from "./playback";
import type { GenerationPackage } from "./types";

export interface PreviewHandle { stop(): void }

const volumeKey = "bassline-preview-volume";

const Preview = forwardRef<PreviewHandle, { pkg: GenerationPackage; disabled: boolean }>(function Preview({ pkg, disabled }, ref) {
  const [state, setState] = useState<PlaybackState>("stopped");
  const [error, setError] = useState("");
  const [storageError, setStorageError] = useState(false);
  const [volume, setVolume] = useState(() => {
    try {
      const saved = sessionStorage.getItem(volumeKey);
      const value = saved === null ? 75 : Number(saved);
      return Number.isFinite(value) && value >= 0 && value <= 100 ? value : 75;
    } catch {
      return 75;
    }
  });
  const [transport] = useState(() => new PlaybackTransport(setState));

  useImperativeHandle(ref, () => ({ stop: () => transport.stop() }), [transport]);
  useEffect(() => { transport.load(pkg); }, [transport, pkg]);
  useEffect(() => () => transport.dispose(), [transport]);
  useEffect(() => { transport.setVolume(volume); }, [transport, volume]);

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
  const start = (restart: boolean) => {
    setError("");
    void (restart ? transport.restart() : transport.play()).catch(() => {
      setError("Audio preview could not start. Check your browser audio settings and try again.");
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
      <label className="volume">Volume <span>{volume}%</span>
        <input type="range" min="0" max="100" value={volume} aria-label="Master volume" aria-valuetext={`${volume}%`}
          onChange={(event) => changeVolume(Number(event.target.value))} />
      </label>
      <p className="transport-state" role="status">{state === "playing" ? "Playing" : state === "paused" ? "Paused" : "Stopped"}</p>
    </div>
    {error && <p className="error" role="alert">{error}</p>}
    {storageError && <p className="warning" role="alert">Volume could not be saved for this browser session.</p>}
    <PianoRoll pkg={pkg} transport={transport} playbackState={state} />
  </section>;
});

export default Preview;
