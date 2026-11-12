import { useCallback, useEffect, useRef, useState, type ChangeEvent, type FormEvent } from "react";
import { api, download } from "./api";
import { initialRequest, isPackage, needsRebuild, replayWithRequest, requestFromPackage, roots, scales } from "./domain";
import Preview, { type PreviewHandle } from "./Preview";
import Modal from "./Modal";
import { dimensions, type Dimension, type GenerateRequest, type GenerationPackage, type Progression } from "./types";

type Catalogs = { presets: string[]; progressions: Progression[]; roots: string[]; scales: string[] };
type Mode = "single" | "arrangement" | "custom";
const chordExportChannel = 1; // Zero-based MIDI channel 2, matching the API.
const message = (error: unknown) => error instanceof Error ? error.message : "Something went wrong.";

export default function App() {
  const [catalogs, setCatalogs] = useState<Catalogs | null>(null);
  const [catalogError, setCatalogError] = useState("");
  const [request, setRequest] = useState<GenerateRequest>(initialRequest);
  const [result, setResult] = useState<GenerationPackage | null>(null);
  const [draftPackage, setDraftPackage] = useState<GenerationPackage | null>(null);
  const [dirty, setDirty] = useState(false);
  const [resetDefaults, setResetDefaults] = useState(false);
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");
  const [status, setStatus] = useState("");
  const [code, setCode] = useState("");
  const [sharedCode, setSharedCode] = useState("");
  const [shareOpen, setShareOpen] = useState(false);
  const [loadOpen, setLoadOpen] = useState(false);
  const [sectionIndex, setSectionIndex] = useState(0);
  const fileInput = useRef<HTMLInputElement>(null);
  const preview = useRef<PreviewHandle>(null);
  const initialFragmentHandled = useRef(false);
  const busyRef = useRef(false);

  const loadCatalogs = useCallback(async () => {
    setCatalogError("");
    try {
      const [presets, progressions] = await Promise.all([api.presets(), api.progressions()]);
      setCatalogs({ presets, progressions, roots, scales });
    } catch (e) { setCatalogError(message(e)); }
  }, []);

  useEffect(() => { void loadCatalogs(); }, [loadCatalogs]);
  useEffect(() => {
    const handler = (event: BeforeUnloadEvent) => {
      if (dirty || (result && !saved)) { event.preventDefault(); }
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [dirty, result, saved]);

  const accept = useCallback((pkg: GenerationPackage, source: string) => {
    if (!isPackage(pkg)) throw new Error("The API returned an invalid package.");
    preview.current?.stop();
    setResult(pkg); setDraftPackage(structuredClone(pkg)); setRequest(requestFromPackage(pkg));
    setDirty(false); setResetDefaults(false); setSaved(false); setSectionIndex(0); setSharedCode("");
    setStatus(source); setError("");
  }, []);

  const run = useCallback(async (label: string, operation: () => Promise<void>) => {
    if (busyRef.current) return;
    busyRef.current = true;
    setBusy(label); setError(""); setStatus("");
    try { await operation(); } catch (e) { setError(message(e)); }
    finally { busyRef.current = false; setBusy(""); }
  }, []);

  useEffect(() => {
    if (initialFragmentHandled.current) return;
    initialFragmentHandled.current = true;
    const params = new URLSearchParams(window.location.hash.slice(1));
    const shareCode = params.get("share");
    if (shareCode) {
      setCode(shareCode);
      void run("Loading shared bassline", async () => {
        accept(await api.decode(shareCode), "Shared bassline loaded.");
      });
    }
  }, [accept, run]);

  const update = <K extends keyof GenerateRequest>(key: K, value: GenerateRequest[K]) => {
    setRequest((old) => ({ ...old, [key]: value }));
    setDirty(true);
  };
  const updateDimension = (dimension: Dimension, value: number | null) => {
    update(dimension, value);
    if (result && value === null) setResetDefaults(true);
    if (value !== null) setDraftPackage((old) => old && ({
      ...old, effectiveConfiguration: { ...old.effectiveConfiguration,
        sections: old.effectiveConfiguration.sections.map((s) => ({ ...s, behavior: { ...s.behavior, [dimension]: value } })),
      },
    }));
  };
  const updateSectionBehavior = (dimension: Dimension, value: number) => {
    setDraftPackage((old) => old && ({
      ...old, effectiveConfiguration: { ...old.effectiveConfiguration,
        sections: old.effectiveConfiguration.sections.map((section, index) => index === sectionIndex
          ? { ...section, behavior: { ...section.behavior, [dimension]: value } } : section),
      },
    }));
    setDirty(true);
  };
  const confirmReplacement = () => (!result && !dirty) || (!!result && saved && !dirty) ||
    window.confirm("Replace your current work? Save the current package first if needed. Unapplied settings are not included in the saved package.");

  const generate = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!catalogs || busyRef.current) return;
    if (request.sections && request.sections.reduce((sum, section) => sum + section.bars, 0) > 4096) {
      setError("Custom sections cannot exceed 4096 bars in total.");
      return;
    }
    const rebuild = result && (resetDefaults || needsRebuild(result, request));
    if (rebuild && !window.confirm("This change rebuilds the arrangement using style defaults and may discard per-section behavior edits. Continue?")) return;
    if (result && !saved && !window.confirm("This replaces your unsaved result. Save it first if you want to keep it. Continue?")) return;
    preview.current?.stop();
    void run("Generating bassline", async () => {
      const submitted = result && request.seed === null
        ? { ...request, seed: crypto.getRandomValues(new Int32Array(1))[0] }
        : request;
      const pkg = result && draftPackage && !rebuild
        ? await api.replay(replayWithRequest(draftPackage, submitted))
        : await api.generate(submitted);
      accept(pkg, "Bassline generated.");
    });
  };

  const mode: Mode = request.sections ? "custom" : request.mode;
  const changeMode = (value: Mode) => {
    setRequest((old) => {
      if (value === "custom") {
        return { ...old, mode: "single", sections: old.sections ?? [{ name: "Section 1", style: old.style, bars: old.bars }] };
      }
      const { sections: _sections, ...rest } = old;
      return { ...rest, mode: value };
    });
    setDirty(true);
  };
  const updateCustom = (index: number, patch: Partial<NonNullable<GenerateRequest["sections"]>[number]>) => {
    setRequest((old) => ({ ...old, sections: old.sections?.map((s, i) => i === index ? { ...s, ...patch } : s) }));
    setDirty(true);
  };
  const moveCustom = (index: number, direction: number) => {
    const sections = [...(request.sections ?? [])];
    const destination = index + direction;
    if (destination < 0 || destination >= sections.length) return;
    [sections[index], sections[destination]] = [sections[destination], sections[index]];
    update("sections", sections);
    setResetDefaults(true);
  };
  const save = () => {
    if (!result) return;
    try {
      download(new Blob([JSON.stringify(result, null, 2)], { type: "application/json" }), "bassline-package.json");
      setSaved(true); setStatus("Package download started. Keep the file to reload it later.");
    } catch (e) { setError(message(e)); }
  };
  const loadFile = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file || !confirmReplacement()) return;
    void run("Loading package", async () => {
      if (file.size > 50 * 1024 * 1024) throw new Error("Package file exceeds the 50 MB import limit.");
      let parsed: unknown;
      try { parsed = JSON.parse(await file.text()) as unknown; }
      catch { throw new Error("The selected file is not valid JSON."); }
      if (!isPackage(parsed)) throw new Error("The selected file is not a valid generation package.");
      accept(await api.replay(parsed), "Package loaded and replayed.");
    });
  };
  const decode = () => {
    if (!code.trim() || !confirmReplacement()) return;
    void run("Loading share code", async () => {
      accept(await api.decode(code.trim()), "Shared bassline loaded.");
      setLoadOpen(false);
    });
  };
  const share = () => {
    if (!result) return;
    setShareOpen(true);
    setSharedCode("");
    void run("Creating share code", async () => setSharedCode((await api.share(result)).shareCode));
  };
  const copy = (value: string) => void run("Copying", async () => {
    await navigator.clipboard.writeText(value);
    setStatus("Copied to clipboard.");
  });
  const renderMidi = (combined: boolean) => {
    if (!result) return;
    void run("Rendering MIDI", async () => {
      const midi = combined ? await api.combinedMidi(result) : await api.midi(result);
      download(midi, combined ? "bassline-and-chords.mid" : "bassline.mid");
      setStatus("MIDI download started. Open it in your DAW to audition.");
    });
  };

  const activeSection = draftPackage?.effectiveConfiguration.sections[sectionIndex];
  const selectedProgression = catalogs?.progressions.find((p) => p.id === request.progression || p.name === request.progression);
  const midiActions = <>
    <button type="button" disabled={!result || !!busy} onClick={() => renderMidi(false)}>Download Bass MIDI</button>
    <button type="button" className="primary" disabled={!result || !!busy} onClick={() => renderMidi(true)}>Download Bass + Chords MIDI</button>
  </>;

  return <div className="app">
    <header className="topbar"><div><p className="eyebrow">GOOD TIME TRIBE</p><h1>Bassline Generator</h1></div>
      <nav aria-label="Package actions">
        <button type="button" disabled={!!busy} onClick={() => fileInput.current?.click()}>Load package</button>
        <input ref={fileInput} type="file" accept=".json,application/json" hidden onChange={loadFile} />
        <button type="button" disabled={!!busy} onClick={() => setLoadOpen(true)}>Load code</button>
      </nav>
    </header>
    <div className="announcements" aria-live="polite">
      {busy && <p className="notice">{busy}…</p>}
      {status && <p className="notice">{status}</p>}
      {error && <p className="error" role="alert">{error}</p>}
      {catalogError && <p className="error" role="alert">Could not load choices: {catalogError} <button type="button" onClick={() => void loadCatalogs()}>Retry</button></p>}
    </div>
    <main className="workspace">
      <form className="settings card" onSubmit={generate}>
        <fieldset disabled={!!busy || !catalogs}>
          <div className="section-title"><div><p className="eyebrow">SHAPE YOUR SOUND</p><h2>Settings</h2></div></div>
          <div className="grid-two">
            <label>Root note<select value={request.rootNote} onChange={(e) => update("rootNote", e.target.value)}>{catalogs?.roots.map((x) => <option key={x}>{x}</option>)}</select></label>
            <label>Scale<select value={request.scaleType} onChange={(e) => update("scaleType", e.target.value)}>{catalogs?.scales.map((x) => <option key={x}>{x}</option>)}</select></label>
            <label>Style<select value={request.style} onChange={(e) => update("style", e.target.value)}>{catalogs?.presets.map((x) => <option key={x}>{x}</option>)}</select></label>
            <label>Tempo <span className="hint">BPM</span><input type="number" min="30" max="300" required value={request.bpm} onChange={(e) => update("bpm", e.target.valueAsNumber)} /></label>
            <label>Mode<select value={mode} onChange={(e) => changeMode(e.target.value as Mode)}><option value="single">Single section</option><option value="arrangement">Arrangement</option><option value="custom">Custom sections</option></select></label>
            {mode !== "custom" && <label>Bars {mode === "arrangement" && <span className="hint">per section</span>}<input type="number" min="1" max="512" required value={request.bars} onChange={(e) => update("bars", e.target.valueAsNumber)} /></label>}
          </div>
          <label>Progression<select value={request.progression ?? ""} onChange={(e) => update("progression", e.target.value || null)}>
            <option value="">Random for scale</option>
            {request.progression && !catalogs?.progressions.some((p) => p.id === request.progression) &&
              <option value={request.progression}>Loaded: {result?.effectiveConfiguration.progressionName ?? request.progression}</option>}
            {catalogs?.progressions.map((p) => <option value={p.id} key={p.id}>{p.name}</option>)}
          </select></label>
          {selectedProgression && <p className="hint">{selectedProgression.chordSequence.join(" · ")} — {selectedProgression.description}</p>}
          {mode === "custom" && <div className="custom-sections"><h3>Sections</h3>
            {request.sections?.map((section, index) => <div className="section-editor" key={index}>
              <label>Name<input required value={section.name} onChange={(e) => updateCustom(index, { name: e.target.value })} /></label>
              <label>Style<select value={section.style} onChange={(e) => updateCustom(index, { style: e.target.value })}>{catalogs?.presets.map((x) => <option key={x}>{x}</option>)}</select></label>
              <label>Bars<input type="number" min="1" max="512" required value={section.bars} onChange={(e) => updateCustom(index, { bars: e.target.valueAsNumber })} /></label>
              <button type="button" disabled={index === 0} onClick={() => moveCustom(index, -1)} aria-label={`Move ${section.name} up`}>↑</button>{" "}
              <button type="button" disabled={index === request.sections!.length - 1} onClick={() => moveCustom(index, 1)} aria-label={`Move ${section.name} down`}>↓</button>{" "}
              <button type="button" disabled={request.sections!.length === 1} onClick={() => { update("sections", request.sections!.filter((_, i) => i !== index)); }}>Remove</button>
            </div>)}
            <button type="button" onClick={() => update("sections", [...(request.sections ?? []), { name: `Section ${(request.sections?.length ?? 0) + 1}`, style: request.style, bars: 4 }])}>Add section</button>
            <p className="hint">{request.sections?.reduce((sum, s) => sum + s.bars, 0)} bars total (maximum 4096).</p>
          </div>}
          <details><summary>Behavior</summary><p className="hint">Apply an override to all sections, or use the style default.</p>
            {dimensions.map((dimension) => <label key={dimension} className="dimension">
              <span>{dimension} {request[dimension] === null ? "· style default" : `· ${request[dimension]}`}</span>
              <input aria-label={dimension} type="range" min="0" max="100" value={request[dimension] ?? 50} onChange={(e) => updateDimension(dimension, Number(e.target.value))} />
              <button type="button" onClick={() => updateDimension(dimension, null)}>Use style default</button>
            </label>)}
          </details>
          {activeSection && <details><summary>Section behavior</summary>
            <label>Section<select value={sectionIndex} onChange={(e) => setSectionIndex(Number(e.target.value))}>
              {draftPackage?.effectiveConfiguration.sections.map((s, i) => <option key={i} value={i}>{s.name} · {s.style}</option>)}
            </select></label>
            {dimensions.map((dimension) => <label className="dimension" key={dimension}>
              <span>{dimension} · {activeSection.behavior[dimension]}</span>
              <input type="range" min="0" max="100" value={activeSection.behavior[dimension]} onChange={(e) => updateSectionBehavior(dimension, Number(e.target.value))} />
            </label>)}
            <p className="hint">Changes apply to this section when replaying. Changing style or mode rebuilds defaults.</p>
          </details>}
          <details><summary>MIDI &amp; humanization</summary>
            <div className="grid-two">
              {([
                ["channel", "MIDI channel (1–16)", 1, 16, 1],
                ["velocity", "Velocity", 1, 127, 0],
                ["velocityVariation", "Velocity variation", 0, 127, 0],
                ["timingVariation", "Timing variation (ticks)", 0, 30, 0],
                ["swing", "Swing", 0, 0.5, 0],
              ] as const).map(([key, label, min, max, offset]) => <label key={key}>{label}<input type="number" required min={min} max={max} step={key === "swing" ? "0.01" : "1"} value={request[key] + offset} onChange={(e) => update(key, e.target.valueAsNumber - offset)} /></label>)}
            </div>
            <label className="check"><input type="checkbox" checked={request.accentDownbeats} onChange={(e) => update("accentDownbeats", e.target.checked)} /> Accent downbeats</label>
          </details>
          <details><summary>Seed &amp; variation</summary>
            <label>Seed <span className="hint">blank = random on new generation</span><input type="number" min="-2147483648" max="2147483647" value={request.seed ?? ""} onChange={(e) => update("seed", e.target.value === "" ? null : e.target.valueAsNumber)} /></label>
            <button type="button" onClick={() => update("seed", crypto.getRandomValues(new Int32Array(1))[0])}>New seed</button>
            <p className="hint">Keep the seed to compare settings. Reproduction requires a compatible engine.</p>
          </details>
          <button className="primary generate" type="submit">{result ? "Regenerate bassline" : "Generate bassline"}</button>
        </fieldset>
      </form>
      <div className="result">
        {!result ? <section className="empty card"><p className="eyebrow">YOUR NEXT IDEA STARTS HERE</p><h2>Make room for the low end.</h2><p>Choose your settings and generate a bassline. Listen to the preview, then take the MIDI into your DAW.</p><div className="actions">{midiActions}</div></section> : <>
          <section className="card result-header"><div><p className="eyebrow">GENERATED BASSLINE</p>
            <h2>{result.effectiveConfiguration.rootNote} {result.effectiveConfiguration.scaleType}</h2>
            <p>{result.effectiveConfiguration.bpm} BPM · {result.statistics?.arrangementBars ?? "—"} bars · {result.effectiveConfiguration.progressionName}</p>
            <p className="hint">Seed {result.seed} · Engine {result.engineVersion ?? "legacy 1.0.0"}</p></div>
            <div className="actions"><button type="button" disabled={!!busy} onClick={save}>Save package</button><button type="button" disabled={!!busy} onClick={share}>Share</button>{midiActions}</div>
          </section>
          {dirty && <p className="warning">Settings changed. The visualization, save, share, and MIDI actions use the previous result. Regenerate to apply changes.</p>}
          {result.effectiveConfiguration.channel === chordExportChannel && <p className="warning" role="status">
            Bass and chords both use MIDI channel 2 in the Bass + Chords export. They may play on the same instrument when routed to the same MIDI destination. Export is still available; change the bass channel and regenerate, or route the tracks separately in your DAW.
          </p>}
          <Preview ref={preview} pkg={result} disabled={busy === "Generating bassline"} />
          <section className="card"><p className="eyebrow">THE DETAILS</p><h2>Package overview</h2>
            <div className="stats"><div><strong>{result.statistics?.noteCount ?? "—"}</strong><span>Notes</span></div><div><strong>{result.statistics?.uniqueNotes ?? "—"}</strong><span>Unique pitches</span></div><div><strong>{result.statistics?.lowestNote ?? "—"}–{result.statistics?.highestNote ?? "—"}</strong><span>Range</span></div><div><strong>{result.statistics?.averageVelocity ?? "—"}</strong><span>Avg. velocity</span></div><div><strong>{result.statistics?.averageNoteLengthBeats ?? "—"}</strong><span>Avg. beats</span></div></div>
            <p>Progression: {result.effectiveConfiguration.chordSequence.join(" · ")}</p>
            <details><summary>Effective sections &amp; package</summary><p>Format {result.version} · Engine {result.engineVersion ?? "legacy 1.0.0"} · MIDI channel {result.effectiveConfiguration.channel + 1}</p>
              {result.effectiveConfiguration.sections.map((s, i) => <p key={i}><strong>{s.name}</strong> · {s.style} · {s.bars} bars · {dimensions.map((d) => `${d} ${s.behavior[d]}`).join(" · ")}</p>)}
            </details>
          </section>
        </>}
      </div>
    </main>
    {shareOpen && <Modal titleId="share-title" onClose={() => setShareOpen(false)}>
      <h2 id="share-title">Share this bassline</h2><p>Anyone with this code can recreate the settings. Codes do not include the note snapshot.</p>
      {error && <p className="error" role="alert">{error}</p>}
      {sharedCode && <><label>Share code<textarea readOnly value={sharedCode} rows={4} /></label>
        <div className="actions"><button type="button" disabled={!!busy} onClick={() => copy(sharedCode)}>Copy code</button><button type="button" disabled={!!busy} onClick={() => copy(`${window.location.origin}${window.location.pathname}#share=${sharedCode}`)}>Copy link</button></div>
        <p className="hint">Long links may not work everywhere. Use the code or save a package instead.</p></>}
      <button type="button" onClick={() => setShareOpen(false)}>Close</button>
    </Modal>}
    {loadOpen && <Modal titleId="load-title" onClose={() => setLoadOpen(false)}>
      <h2 id="load-title">Load a share code</h2><label>Share code<textarea value={code} onChange={(e) => setCode(e.target.value)} rows={5} /></label>
      {error && <p className="error" role="alert">{error}</p>}
      <div className="actions"><button type="button" className="primary" disabled={!!busy || !code.trim()} onClick={decode}>Load bassline</button><button type="button" onClick={() => setLoadOpen(false)}>Cancel</button></div>
    </Modal>}
  </div>;
}
