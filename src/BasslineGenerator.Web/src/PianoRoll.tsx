import { useEffect, useRef, useState } from "react";
import type { PlaybackState, PlaybackTransport } from "./playback";
import type { GenerationPackage, NoteData } from "./types";

interface Props { pkg: GenerationPackage; transport: PlaybackTransport; playbackState: PlaybackState }

export default function PianoRoll({ pkg, transport, playbackState }: Props) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const playhead = useRef<HTMLDivElement>(null);
  const [startBar, setStartBar] = useState(0);
  const [barsVisible, setBarsVisible] = useState(8);
  const [selected, setSelected] = useState<NoteData | null>(null);
  const notes = pkg.notes ?? [];
  const sections = pkg.effectiveConfiguration.sections;
  const totalBars = sections.reduce((total, section) => total + section.bars, 0);
  const maxStart = Math.max(0, totalBars - barsVisible);
  const start = Math.min(startBar, maxStart);
  const low = notes.length ? Math.max(0, notes.reduce((a, n) => Math.min(a, n.midiNote), 127) - 1) : 35;
  const high = notes.length ? Math.min(127, notes.reduce((a, n) => Math.max(a, n.midiNote), 0) + 1) : 48;

  useEffect(() => { setStartBar(0); setSelected(null); }, [pkg]);
  useEffect(() => {
    if (playbackState === "stopped") setStartBar(0);
  }, [playbackState]);
  useEffect(() => {
    let frame = 0;
    const update = () => {
      const beat = transport.beat;
      const relative = (beat - start * 4) / (barsVisible * 4);
      if (playhead.current) {
        playhead.current.style.display = relative >= 0 && relative <= 1 ? "block" : "none";
        playhead.current.style.left = `calc(52px + ${relative * 100}% - ${relative * 52}px)`;
      }
      if (playbackState === "playing") {
        if (relative >= 1 || relative < 0) setStartBar(Math.floor(beat / (barsVisible * 4)) * barsVisible);
        frame = requestAnimationFrame(update);
      }
    };
    update();
    return () => cancelAnimationFrame(frame);
  }, [transport, playbackState, start, barsVisible]);
  useEffect(() => {
    const element = canvas.current;
    if (!element) return;
    const draw = () => {
      const width = Math.max(320, element.clientWidth);
      const height = Math.max(260, Math.min(600, (high - low + 1) * 20));
      const ratio = window.devicePixelRatio || 1;
      element.width = width * ratio;
      element.height = height * ratio;
      element.style.height = `${height}px`;
      const ctx = element.getContext("2d");
      if (!ctx) return;
      ctx.scale(ratio, ratio);
      ctx.fillStyle = "#141a29";
      ctx.fillRect(0, 0, width, height);
      const axis = 52, top = 26;
      const gridWidth = width - axis, rowHeight = (height - top) / (high - low + 1);
      ctx.font = "11px system-ui";
      for (let pitch = low; pitch <= high; pitch++) {
        const y = top + (high - pitch) * rowHeight;
        ctx.fillStyle = pitch % 12 === 0 ? "#202b40" : pitch % 2 ? "#192233" : "#1c2638";
        ctx.fillRect(axis, y, gridWidth, rowHeight);
        ctx.fillStyle = "#abb7c9";
        if (rowHeight >= 13 || pitch % 12 === 0) ctx.fillText(`${["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"][pitch % 12]}${Math.floor(pitch / 12) - 1}`, 5, y + Math.min(rowHeight - 2, 13));
      }
      for (let bar = 0; bar <= barsVisible; bar++) {
        const x = axis + bar * gridWidth / barsVisible;
        ctx.strokeStyle = "#41516b";
        ctx.beginPath(); ctx.moveTo(x, top); ctx.lineTo(x, height); ctx.stroke();
        ctx.fillStyle = "#c0cad8";
        if (start + bar < totalBars) ctx.fillText(`${start + bar + 1}`, x + 3, 17);
      }
      for (const note of notes) {
        const beat = note.startBeat - start * 4;
        if (beat + note.durationBeats <= 0 || beat >= barsVisible * 4) continue;
        const x = axis + Math.max(0, beat) / (barsVisible * 4) * gridWidth;
        const end = axis + Math.min(barsVisible * 4, beat + note.durationBeats) / (barsVisible * 4) * gridWidth;
        const y = top + (high - note.midiNote) * rowHeight;
        ctx.fillStyle = `rgba(82, 222, 181, ${0.35 + note.velocity / 127 * 0.65})`;
        ctx.fillRect(x, y + 1, Math.max(1, end - x), Math.max(1, rowHeight - 2));
      }
    };
    const observer = new ResizeObserver(draw);
    observer.observe(element);
    draw();
    return () => observer.disconnect();
  }, [notes, low, high, start, barsVisible, totalBars]);

  const visibleNotes = notes.filter((n) => n.startBeat + n.durationBeats > start * 4 && n.startBeat < (start + barsVisible) * 4);
  return <section className="roll">
    <div className="section-title"><div><p className="eyebrow">NOTE VIEW</p><h2>Piano roll</h2></div>
      <div className="inline"><label>Bars shown <select value={barsVisible} onChange={(e) => setBarsVisible(Number(e.target.value))}>
        {[4, 8, 16, 32, 64].map((n) => <option key={n}>{n}</option>)}
      </select></label></div>
    </div>
    <div className="sections" aria-label="Arrangement sections">
      {sections.map((section, index) => {
        const bar = sections.slice(0, index).reduce((sum, s) => sum + s.bars, 0);
        return <button key={index} type="button" onClick={() => setStartBar(bar)} title={`Go to bar ${bar + 1}`}>
          {section.name} <span>{section.bars} bars</span>
        </button>;
      })}
    </div>
    <div className="roll-canvas">
      <canvas ref={canvas} aria-hidden="true" />
      <div ref={playhead} className="playhead" aria-hidden="true" />
    </div>
    <div className="roll-nav">
      <button type="button" onClick={() => setStartBar(Math.max(0, start - barsVisible))} disabled={start === 0}>Previous</button>
      <label>Start bar <input type="number" min={1} max={maxStart + 1} value={start + 1}
        onChange={(e) => setStartBar(Math.max(0, Math.min(maxStart, Number(e.target.value) - 1)))} /></label>
      <span>Bars {start + 1}–{Math.min(totalBars, start + barsVisible)} of {totalBars}</span>
      <button type="button" onClick={() => setStartBar(Math.min(maxStart, start + barsVisible))} disabled={start >= maxStart}>Next</button>
    </div>
    <p className="hint">Notes are read-only. Brighter notes have higher velocity. The preview instrument may sound different from your DAW.</p>
    <details><summary>Visible notes ({visibleNotes.length})</summary>
      <div className="note-list"><table><thead><tr><th>Note</th><th>Start beat</th><th>Length</th><th>Velocity</th></tr></thead>
        <tbody>{visibleNotes.map((n, i) => <tr key={i} onClick={() => setSelected(n)}>
          <td><button className="text-button" type="button" onClick={() => setSelected(n)}>{n.note} ({n.midiNote})</button></td>
          <td>{n.startBeat.toFixed(2)}</td><td>{n.durationBeats.toFixed(2)}</td><td>{n.velocity}</td>
        </tr>)}</tbody></table></div>
      {selected && <p>Selected: {selected.note}, beat {selected.startBeat}, length {selected.durationBeats}, velocity {selected.velocity}.</p>}
    </details>
  </section>;
}
