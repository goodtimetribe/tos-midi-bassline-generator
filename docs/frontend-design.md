# Bassline Generator: MVP frontend design

## Product direction

**A desktop-first creative workspace for making a bassline, inspecting it, refining its parameters, and taking MIDI into a DAW.** The app is a client of the existing stateless API; no API implementation or backend changes are assumed.

The central creative loop is **set the musical context → generate → inspect → adjust → regenerate → download MIDI**. A musician can save the generated package as a local JSON file, reopen it, or share a portable code. The workspace favors a few understandable controls first, with detailed behavior and MIDI controls available when needed.

The MVP does **not** play audio: the API produces MIDI, not sound. Users audition the downloaded MIDI in their DAW. The piano roll is a visualization, not a note editor; changing parameters and regenerating creates new notes. Accounts, cloud projects, autosave, collaboration, direct note editing, and a browser synthesizer are outside the MVP.

See [Frontend Readiness Review](frontend-readiness-review.md) for the API assessment and contract references. This document makes concrete product and interaction choices for the application.

## Key journeys

1. **Start a bassline:** Select key, scale, style, BPM, and length. Optionally select a progression or leave it random. Generate, inspect the piano roll and metadata, then download MIDI.
2. **Refine an idea:** Change movement, groove, mutation, tension, repetition, or other settings. The existing result remains visible as “previous result”; select **Regenerate** to apply the draft. Keep the seed to compare settings, or choose **New seed** for another variation.
3. **Build an arrangement:** Choose the built-in arrangement or edit a custom list of named sections. Inspect section boundaries and effective behavior, then regenerate the whole result.
4. **Save and return:** Download a package JSON file. Later, load it from disk and replay it to validate compatibility before using it as the active result. Exported JSON preserves its note snapshot even if a future API no longer supports replay.
5. **Share an idea:** Create a share code for the current result. Copy the code or a link containing it in the URL fragment. A recipient pastes the code or opens the link; the API decodes it into a fresh package.
6. **Return to the DAW:** Download MIDI from the current generated package; after further edits, regenerate and download again. The MIDI always corresponds to the displayed current result, not an unapplied draft.

## Workspace and screen layouts

### Desktop: one primary workspace

The generator is the home screen and the only required page. At typical desktop widths, a persistent left settings rail and a wider result workspace remain visible together.

```text
+--------------------------------------------------------------------------------+
| Bassline Generator                         Load package  Load code  Help       |
+---------------------------+----------------------------------------------------+
| GENERATE                  | RESULT                     [Save] [Share] [MIDI]   |
| Key [A]  Scale [Minor]    | A Minor · Acid · 140 BPM · 8 bars                 |
| Style [Acid] BPM [140]    | Seed 12345 · Synthwave · Current / Draft changed  |
| Length [8] bars           +----------------------------------------------------+
| Progression [Random]      | Arrangement / bar overview                        |
| Mode [Single v]           | [---------------- 8 bars -----------------------]  |
|                           +----------------------------------------------------+
| [Behavior v]              | Piano roll             [zoom -] [zoom +] [fit]    |
| Movement  [----o-----]    | pitch | 1    | 2    | 3    | 4    | ...          |
| Groove    [------o---]    | A2    | ==== |   == | ==== |   == |              |
| ...                       | E2    |      | ==   |      | ==   |              |
| [MIDI & humanize v]        +----------------------------------------------------+
| [Seed / variation v]      | Notes 48 · Range E2–A3 · Avg velocity 100          |
|                           | Progression details · Sections · Package details  |
| [Generate/Regenerate]     |                                                      |
+---------------------------+----------------------------------------------------+
```

- The settings rail scrolls independently; **Generate** or **Regenerate** stays reachable at its bottom. Advanced groups start collapsed; controls display plain-language descriptions and current numeric values.
- The result header summarizes the **generated** key, tempo, style/sections, progression, seed, and state. No result actions are enabled before there is a result.
- On a dirty draft, a visible banner says “Settings changed; visualization and downloads are from the previous result.” **Regenerate** applies edits. Save, Share, and MIDI remain explicitly labeled as actions on the previous generated result; offer **Regenerate first** rather than silently exporting the draft.
- Selecting a section in the overview scrolls or zooms the piano roll to it and reveals its settings. Metadata and statistics are in a compact, expandable inspector below the roll; package/engine version is available without dominating the screen.
- Before first generation, the result area shows a short prompt and disabled result actions rather than an empty chart. Loading and errors appear beside the affected action while the last successful result remains visible.

### File and share surfaces

- **Load package:** standard file picker for a local JSON package. Show filename and a compatibility/loading state. On failure, retain the current workspace and offer a retry or another file. If replay is unsupported but the file has a usable note snapshot, optionally show it in a clearly labeled read-only historical preview; keep the original file available for re-export and do not claim it can render MIDI.
- **Save package:** download the current complete package as a `.json` file, including its note and statistics snapshot. Use a sensible filename, such as `bassline-A-minor-12345.json`. This is a manual download, not cloud storage or browser autosave.
- **Share dialog:** show **Copy code** and **Copy link**, plus a disclosure that anyone holding the code can recreate the settings. Link uses a URL fragment. A copied code represents the current generated result, not unapplied edits. Show errors without replacing the result; offer JSON export if the code is too large or a link is impractical.
- **Load code:** a multiline text field and **Load** action. On a fragment link, decode after the app loads; allow a user to retry or paste a code if decoding fails. Avoid putting the code in analytics, logs, or error reports.
- Before loading a new package/code or starting a new generation that would replace an unsaved result or draft, prompt to save the current result, discard, or cancel. Explain that saving the result does not save unapplied draft changes; let the user regenerate first or cancel.

### Responsive behavior

At tablet widths, the settings rail becomes a collapsible side drawer and the visualization gets the remaining width. On phones, use stacked **Settings** and **Result** panels, a sticky Generate/Regenerate action, and a horizontally scrollable roll with preserved pitch labels. Keep save, share, and MIDI in an accessible result action menu; never rely on hover for note details. Do not hide editing or loading features on small screens.

## UI architecture and component hierarchy

Use a client-rendered application with a typed API boundary, a domain/state layer, presentation components, and a visualization layer. A TypeScript-based UI is recommended; the choice of component framework is not a product requirement.

```text
App
├── AppHeader
├── WorkspaceController
│   ├── GenerationSettingsPanel
│   │   ├── MusicalContextFields
│   │   ├── StyleAndProgressionPicker
│   │   ├── ArrangementEditor
│   │   ├── BehaviorControls
│   │   ├── MidiAndHumanizationControls
│   │   ├── SeedControls
│   │   └── GenerateAction
│   └── ResultWorkspace
│       ├── ResultHeader / DirtyStatus
│       ├── ResultActions
│       ├── ArrangementOverview
│       ├── PianoRoll
│       │   ├── BarRuler / PitchAxis
│       │   ├── NoteViewport
│       │   └── NoteInspector
│       └── MetadataAndStatistics
├── LoadPackageDialog
├── ShareDialog / LoadCodeDialog
├── ReplaceWorkConfirmation
└── AccessibleStatusAndErrors
```

The workspace controller coordinates use cases, not drawing. The API client owns HTTP and response decoding; domain adapters own request/package mapping and validation rules; components render state and dispatch user intent. Piano-roll geometry is calculated from a package snapshot and viewport, independent of HTTP and form state. Catalog data is loaded once per session and may be refreshed explicitly.

## Controls and editing semantics

### First-use controls

| Control | Interaction and initial behavior |
| --- | --- |
| Root / scale | Select supported notes and scales. Use values from the documented API contract/OpenAPI; there is no scales endpoint. |
| Style | Select an API preset; describe its musical character without promising exact note density. |
| Tempo / length | Numeric input with steppers: BPM 30–300; bars 1–512. Explain that built-in arrangement length is **per section**. |
| Progression | Searchable list from the API, with sequence, mood, and description; default **Random for scale**. |
| Mode | Single, built-in arrangement, or custom sections. Custom rows have name, style, and 1–512 bars; total cannot exceed 4096 bars. |
| Generate | Generates a package; the server chooses a seed if omitted. Show the actual returned seed. |

Use an initial example such as A Minor, Acid, 140 BPM, 8 bars, single mode, and random progression. It is an application default, not an API guarantee.

### Advanced controls

- Five behavior sliders from 0–100: movement (root-focused to melodic), groove (straight to syncopated), mutation (stable to evolving), tension (consonant to unstable), and repetition (changing to recurring). Display values and provide numeric entry and keyboard access. Before generation, each can be **Use style default**, represented by an omitted/null override, not zero.
- MIDI settings: channel 0–15 (display both a musician-friendly 1–16 and its API value where useful), velocity 1–127, velocity variation 0–127, timing variation 0–30 ticks, swing 0–0.5 of an eighth note, and accent downbeats.
- Seed: optional signed 32-bit value. **Keep seed** is the default for replay; **New seed** selects another valid seed and updates both seed fields in the replay draft. A new seed changes the variation, but cannot guarantee a different audible result. Explain that the same seed is reproducible only with the same supported engine and compatible runtime.
- Section inspector: show each generated section's effective style, bars, and five resolved behavior values. The user can set individual section behavior values for replay. For clarity, indicate whether the user is changing one section or intentionally applying the same value to all sections.

### Routing edits correctly

Package replay consumes explicit `effectiveConfiguration.sections`: **per-section behavior is authoritative**, and derived notes/statistics are ignored. Do not expose a global slider that only changes a top-level package field while pretending it changes section behavior.

- For replay-compatible edits, update the relevant package configuration in the draft: key, scale, tempo, MIDI settings, `requestedProgression`, seed, and explicitly edited section lengths/behavior. When a user applies a behavior to all sections, update that behavior in every section; preserve all other per-section values. Change `requestedProgression`, not the derived selected progression metadata. On seed changes, update both `seed` and `effectiveConfiguration.seed`.
- For structure/style changes that need **style defaults** (including changing mode, choosing a new style, adding a new style-based section, or resetting a section to defaults), use a fresh `POST /api/generate/package` request. The API does not expose a per-section style-default lookup or accept per-section behavior overrides in `GenerateRequest`. Clearly warn before switching: this rebuilds the section configuration and can discard per-section behavior edits; retain/export the existing result first. Carry over valid global settings and the selected seed where representable. Never silently invent preset behavior values.
- For a replayed arrangement, section lengths in `sections` determine actual length; changing only the top-level `bars` will not rebuild the section sequence. Edit the relevant section lengths, or explicitly rebuild the arrangement with a fresh generation request.
- No note rectangles are draggable/editable. Changes to package `notes` and `statistics` do not affect replay or MIDI rendering. Label regeneration as a whole-result operation, not a single-note or isolated-section edit.
- The returned package replaces the result only after success. Show the returned effective section values and selected progression, rather than assuming the draft exactly predicts the output.

## State management

Use a reducer or small state store with explicit events and a single canonical generated package:

| State | Responsibility |
| --- | --- |
| Catalogs | Presets, progressions, supported request choices, loading/error status. |
| Draft | New-generation request or replay-edit draft; validation, section scope, selected seed, revision, and dirty state. |
| Result | Last successful complete package, source, result revision, and whether it has been exported. Immutable while editing. |
| Operations | Separate status/error for generation/replay, file import, share/decode, and MIDI rendering. |
| Presentation | Active section, viewport/zoom, open panels/dialogs, focus target, and announcements. |

**Transitions:** initial draft → generating → result; result → dirty draft → replaying or fresh generation → new result; result → share/render/save without mutating it; file/code → loading/decoding → new result on success. A failure returns to the previous result and preserves the draft. Canceling a replacement keeps both unchanged.

- Snapshot the draft and revision at submission. Disable conflicting generation/replay actions while a request is pending, or cancel superseded requests. If editing remains enabled, never let a late response overwrite newer edits: retain the new draft, mark it dirty against the returned snapshot, and ignore results from superseded operations.
- Attach share and MIDI requests to a specific immutable result revision. If the result changes while a request is pending, do not mislabel an old response as belonging to the new result; cancel or discard it.
- Only mark a package saved after initiating a successful download; browsers cannot generally confirm that the user retained the file. Track draft dirty state separately from whether the generated result was exported.
- Loading a package validates JSON shape/size locally before replaying via the API. Treat the response as authoritative; imported notes are a snapshot, not evidence that replay succeeded. A `400` can mean malformed or unsupported: show the server's safe message rather than inventing a precise cause.
- Keep state in memory by default. Local saving/loading means explicit JSON file download and import; no browser storage, cloud storage, or automatic recovery in MVP. Warn before navigation/reload when unsaved work would be lost, subject to browser restrictions.

## API integration strategy

All generation and sharing requests are explicit user actions; avoid regenerating on every slider movement. Use a configurable API base URL, JSON content negotiation for package operations, typed request/response models, and one error-normalization layer. API output is authoritative. Configure the actual frontend origin in the API's CORS allowlist for cross-origin deployment.

| User action | Request | Result and UI behavior |
| --- | --- | --- |
| Load choices | `GET /api/presets`, `GET /api/progressions` | Populate style/progression choices; retry individual failures. |
| Generate or rebuild style defaults | `POST /api/generate/package` | Send `GenerateRequest`; store returned package and seed. |
| Apply replay-compatible edits | `POST /api/package/regenerate` | Send edited package; replace result with regenerated notes, statistics, and effective settings. |
| Download MIDI | `POST /api/package/midi` | Send current result package; check success and `audio/midi`, download blob, and release browser resources. |
| Save package | Browser file download | Serialize complete current package to JSON; no server call. |
| Load package | File picker, then `POST /api/package/regenerate` | Parse locally, replay, and activate only on success; preserve original file on failure. |
| Create share | `POST /api/share` | Send current package; copy returned `shareCode` or a fragment link. |
| Load share | `POST /api/share/decode` | Send `{ "shareCode": "..." }`; activate returned package on success. |

The `GET /api/health` endpoint may inform a status indicator, but do not require a separate health check before every action. The alternative `POST /api/generate` can return MIDI or metadata, but the package endpoint is the MVP's primary path because it includes the notes for visualization.

For `400`, show the safe server message and identify the action that failed; where possible, show client-side field errors before submission. For `429`, display “Too many requests; wait and retry” and do not run automatic retry loops. Handle network/CORS failures, non-JSON errors, and unexpected binary types distinctly from validation errors. The service defaults to 100 requests per 60 seconds per client IP; each replay, share, decode, and render counts.

Share codes are URL-safe and encode settings/seed, not the note snapshot. Fragment links avoid sending the code as part of the HTTP request URL; the browser application still has access to it, so do not forward it to telemetry or third parties. The API allows codes up to 262,144 characters, but real browsers and messaging tools may accept less: always support raw-code paste and JSON files, and never promise all generated links will work everywhere.

## Piano roll visualization design

### Data and geometry

The package's `notes` supply `note`, `midiNote`, `startBeat`, `durationBeats`, and `velocity`. Beats are quarter notes and the current engine uses four beats per bar. For each visible note:

- Horizontal position is its absolute `startBeat` relative to the viewport; width is `durationBeats` at the current horizontal zoom. Do not quantize or round positions for drawing: swing and timing variation may place notes between grid lines.
- Vertical position is `midiNote`; higher pitches appear higher. Label pitch lanes using note names with C4 = MIDI 60.
- Color/opacity represents velocity, with a legend and a non-color-dependent numeric value in the note inspector. Empty notes or missing optional visualization data get an informative empty state.
- The bar ruler starts at bar 1, with bar and beat grid lines. Section boundaries come from cumulative effective section bars and align with the absolute song timeline. Show the progression sequence as context; do not imply a note is an exact chord tone.

### Interaction and accessibility

- Initial view fits the generated pitch range with a small margin and a useful time window; **Fit**, horizontal zoom, and scroll expose details without losing context. Overview selection navigates to a section; the viewport position stays aligned with its ruler.
- Selecting/focusing a note reveals pitch name, MIDI number, start beat, duration, and velocity. Do not require hover, and do not suggest that notes can be moved. Provide keyboard navigation and a textual note list/table alternative with the same data.
- Offer section labels, bar numbers, and readable contrast at all zoom levels. Announce loading, errors, and generated-result updates to assistive technology; preserve visible focus. Provide a compact summary if no notes exist.
- For long arrangements, render only notes intersecting the visible time/pitch window, while keeping a simplified section overview. Test up to the API's 4096-bar maximum, including dense outputs; avoid a DOM element for every off-screen note. The rendering technology is an implementation choice, but accessibility must not depend solely on canvas pixels.

## Phased implementation roadmap

Each phase ends with an independently demonstrable outcome. The MVP is complete only after phases 1–4, including all save/load/share and edit/replay workflows.

### Phase 1 — Foundation and generation

- Establish application shell, typed API client, configuration, catalog loading, core controls, and responsive workspace.
- Implement single-mode and built-in-arrangement generation, metadata summary, loading/validation/error states, and a seed display.
- **Exit:** a musician can generate a package and see its key, tempo, seed, progression, sections, and statistics; errors do not erase the previous result.

### Phase 2 — Inspect and export

- Implement read-only piano roll, section overview, zoom/scroll, note inspector, keyboard access, and textual alternative.
- Implement MIDI rendering/download and local JSON package export.
- **Exit:** the displayed package yields a downloadable MIDI file and complete JSON snapshot; the roll represents returned note positions and remains usable for long arrangements.

### Phase 3 — Refine and replay

- Add advanced controls, seed variation, custom sections, per-section behavior editing, dirty-state handling, and replacement confirmation.
- Route replay-compatible edits to package replay and style/default/structure rebuilds to fresh generation, with the warnings described above.
- **Exit:** editing never silently changes the current result; regenerate updates the roll and metadata, and MIDI reflects the newly returned package.

### Phase 4 — Reopen and share

- Add JSON file import with replay validation, share-code creation/copy, code input/decode, and fragment-link loading.
- Preserve the current workspace on malformed, unsupported, oversized, or network-failed input; provide raw-code and file fallbacks.
- **Exit:** a package saved locally can be reopened, and a shared code/link reconstructs a package with the same supported replay inputs.

### Phase 5 — Release hardening

- Exercise end-to-end generate → inspect → edit → regenerate → MIDI → save → reload → share → decode; check deterministic same-seed replay under a fixed supported environment.
- Verify CORS/deployment, request types, privacy/no unintended persistence, 400/429/network handling, stale response races, and supported/unsupported package versions.
- Test accessibility, keyboard flows, responsive layouts, long/dense packages, share-link size limitations, and download/import recovery.
- **Exit:** critical workflows are accessible and resilient on supported desktop browsers and usable on smaller screens.

## Acceptance boundaries

- The active generated package—not a draft—is the source for every displayed note, statistic, save, share, and MIDI download.
- User-facing copy distinguishes generation settings from note editing and distinguishes manual local files from automatic or server-side saving.
- Package and engine versions are retained. Unsupported replay is reported without silently changing versions; snapshots and downloaded MIDI are the recovery path for historical output.
- An API response never overwrites newer user work unnoticed, and failed actions do not discard a valid result or draft.
- No browser playback, direct note editing, account, cloud persistence, or new API feature is required to meet MVP scope.
