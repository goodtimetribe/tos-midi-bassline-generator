# Frontend Readiness Review

## Executive assessment

**The API is ready to support a focused, stateless web application MVP.** It already exposes reproducible generation packages, beat-positioned notes, effective settings, statistics, replay, share-code encode/decode, and MIDI rendering. The frontend can generate and visualize a result without parsing MIDI, then use the same package as the source for replay, sharing, and MIDI downloads.

The main product boundary is important: **editing is configuration editing, not piano-roll note editing.** Replay ignores submitted notes and statistics and regenerates them from the package's effective configuration, seed, and supported engine version. The current API also does not provide audio preview, accounts, remote saves, or asynchronous job/progress endpoints.

Before deploying a separate web origin, configure API CORS for that exact origin. Production allows no origins by default. The API is rate-limited (100 requests per 60 seconds by default), returns `429` when exceeded, sets `Cache-Control: no-store`, and does not persist requests or generated content. The UI should preserve those privacy expectations and communicate that saved work lives in a downloaded file, a share code, or (only if deliberately implemented) the user's browser.

## User journeys

### Generate and visualize

1. Open the generator with sensible defaults and load presets and progressions.
2. Set key, scale, tempo, length, style, progression, and optional behavioral/MIDI controls.
3. Choose single-section, built-in arrangement, or custom sections.
4. Generate a package and retain its returned seed, effective configuration, notes, and statistics together.
5. Show the generated arrangement and piano roll immediately; offer MIDI download from the same package.

### Edit and regenerate

1. Adjust top-level settings or section-specific style, length, and behavior in an editable draft.
2. Mark the draft as changed while keeping the last generated result visible but clearly labeled as stale.
3. On regenerate, submit the edited package to replay; replace the current result only on success.
4. Explain that notes/statistics are recomputed and direct note edits are not applied.

### Share and reload

1. Request a share code for the current package and offer copy-link and copy-code actions.
2. Put the code in a URL fragment (not a query parameter) so it is not sent in ordinary HTTP requests or referrer headers; decode it in the browser on load.
3. On reload, decode the share code into a package, then show its generated configuration, notes, and statistics.
4. Also support importing/exporting package JSON for durable local files and recovery when the link is lost.

### Re-render MIDI

1. From any successfully loaded or regenerated package, request MIDI rendering.
2. Download the returned `audio/midi` blob with a stable filename and report failures, including rate limiting.
3. Keep the package and visualization intact if rendering fails; retry rendering the same package rather than generating a new one.

## Screen concepts

### Generator workspace (MVP)

A single responsive workspace with a settings panel and a result canvas. The settings panel groups fundamentals (key, scale, tempo, bars), style/progression, arrangement, and optional advanced controls. The result canvas has an arrangement timeline, piano roll, compact statistics, and actions for regenerate, share, JSON export, and MIDI download. On narrow screens, use stacked panels and a compact sticky action row.

### Shared or imported generation (MVP)

Reuse the workspace in a loaded state. Show source/status (shared link, imported package, or current session), engine/package version, resolved progression, and any replay errors. Do not silently substitute a new engine when a package version is unsupported.

### Optional local library (post-MVP)

A browser-only list of explicitly saved packages, with rename, reopen, duplicate, export, and delete. There is no server-side save endpoint. Local persistence is a product/privacy choice and should be opt-in, clearly described, and easy to clear.

## API endpoint mapping

| UI need | Endpoint | UI handling |
| --- | --- | --- |
| Service check | `GET /api/health` | Optional startup/degraded-service indicator; do not make generation depend on a separate check. |
| Style choices | `GET /api/presets` | Populate style selectors; values are display names. |
| Progression choices/details | `GET /api/progressions` | Populate progression selector and show chord sequence, mood, description, and suggested use. Empty/null selection means seeded random choice. |
| New package | `POST /api/generate/package` | Send a `GenerateRequest`; receive the full versioned package, note visualization data, statistics, seed, and effective configuration. |
| Direct MIDI generation (alternative) | `POST /api/generate` | Default response is MIDI; `Accept: application/json` gives metadata, not note positions. Prefer package generation in the UI so the visualization and export share the same source. |
| Replay edited package | `POST /api/package/regenerate` | Send the edited package. Treat returned notes, statistics, and configuration as authoritative; input notes/statistics are ignored. |
| Render/re-render MIDI | `POST /api/package/midi` | Send current package; download `audio/midi`. |
| Create share code | `POST /api/share` | Send package; receive `{ "shareCode": "..." }`. Handle invalid/oversized packages. |
| Load share code | `POST /api/share/decode` | Send `{ "shareCode": "..." }`; receive regenerated package. |

Validation errors and unsupported package/engine versions use `400` with a safe `{ "error": "..." }` message; throttling uses `429`. Surface actionable messages without echoing request content. The server exposes `X-Generation-Seed` to browser clients via CORS, but the package response already includes the seed and should be the primary source of truth. The OpenAPI document can supply request schema examples/suggestions; there is no dedicated scales endpoint.

### Request semantics the UI must preserve

- `style` is required even for arrangements/custom sections, although per-section styles drive their behavior.
- `mode` is case-sensitive (`single` or `arrangement`). Custom `sections` override single mode and cannot be combined with arrangement mode.
- In arrangement mode, `bars` is per section. For custom sections, `bars` is ignored; the sequence has a total maximum of 4096 bars.
- `progression` accepts a library name/ID or supported legacy sequence. For package edits, update `requestedProgression`; resolved progression fields are derived.
- Seed is a signed 32-bit integer and must match `effectiveConfiguration.seed` in a package.
- Behavior controls are nullable overrides from 0 to 100. In arrangements, each section has authoritative effective behavior.
- Swing is 0–0.5; timing variation 0–30 ticks; velocity variation 0–127; channel 0–15; velocity 1–127.
- Share codes contain version, engine version, seed, and effective configuration, not the note snapshot. Replay determinism is bounded by the supported engine/runtime implementation.

## State model recommendations

Keep the current editable draft separate from the last successful generated package:

```text
catalogs: { presets, progressions, loadStatus }
draft: GenerateRequest | edited package configuration
result: GenerationPackage | null
dirty: boolean
operation: idle | generating | replaying | sharing | decoding | renderingMidi
error: { operation, status, message } | null
source: new | share-link | imported-file | local-save
```

- Treat a successful `GenerationPackage` as the canonical result snapshot. Its `notes` and `statistics` are display/output data; its `effectiveConfiguration` and `seed` are replay inputs.
- Do not mutate a result package in place while the user is editing. Create a draft, then construct the replay package from that draft while preserving package version and seed consistency.
- Derive visual geometry and summary labels from the package; avoid storing duplicate note/stat values in UI state.
- Keep independent pending/error state per operation or serialize conflicting mutations. Ignore stale responses using an operation/request ID; a late response must not overwrite a newer result.
- Mark the result stale when the draft diverges. Do not imply that changing a control has changed the notes until replay succeeds.
- Abort superseded network requests where practical. Show progress/spinner states, but do not imply server-side job tracking or cancellation.
- Keep secrets and personal data out of share payloads. Treat imported JSON/share codes as untrusted input and validate shape/size client-side as a usability guard, while relying on server validation for authority.
- Do not cache API responses or generated package content in a service worker. If local saves are added, make persistence opt-in and provide clear/delete controls.

## Suggested UI architecture

Use a small client-rendered application with four boundaries:

1. **API client:** typed endpoint functions, base URL configuration, JSON/binary response handling, normalized API errors, and explicit handling for `429`, `400`, and network/CORS failures.
2. **Domain/state layer:** package/request types, draft-to-package conversion, dirty comparison, version checks, and generation/replay orchestration. Keep all generator semantics here rather than scattering them across components.
3. **Workspace components:** settings form, arrangement editor, action bar, statistics, timeline, piano roll, and import/share dialogs.
4. **Visualization layer:** pure rendering from package notes and effective section boundaries. Keep rendering independent from API transport so it remains testable and responsive.

TypeScript is recommended for the API shapes and discriminated async states. Use a lightweight state container or reducer; a server-state library is optional because the catalogs are small and generated packages are session assets rather than cacheable queries. Keep API base URL externalized for deployment. No backend-for-frontend or database is required for the MVP.

## Interactive controls

### Core generation controls

- Root note and scale selectors.
- Tempo (30–300 BPM) and bars/length (1–512 within applicable mode semantics).
- Style selector with short descriptions; progression selector with chord sequence, mood, and suggested use.
- Mode switch for single versus built-in arrangement.
- Optional custom section list editor: name, style, bars; add/remove/reorder sections and show total bars.

### Advanced controls

- Movement, groove, mutation, tension, and repetition sliders (0–100), with “Use style default” represented by an unset value rather than zero.
- MIDI channel, base velocity, velocity variation, timing variation, swing, and accent-downbeats toggle.
- Seed field plus “Randomize seed” action. Show the actual generated seed after generation and make “regenerate same seed” distinct from “new seed.”

### Result actions

- Regenerate from the changed configuration.
- Share link/code, copy package JSON, import package JSON.
- Render/download MIDI from the current package.
- Optional “duplicate” to branch settings without losing the previous result.

## Visualization opportunities

- **Piano roll:** plot `startBeat`, `durationBeats`, and `midiNote`; map pitch names from `note`; encode velocity by color/opacity. Add octave labels, beat/bar grid, zoom, and horizontal scroll.
- **Arrangement timeline:** show section boundaries and names/styles, aligned to the absolute song beat; use each section's bar length and effective behavior as metadata or tooltip.
- **Progression strip:** display selected chord sequence across bars as a harmonic context, but label it as progression metadata rather than claiming exact per-note chord assignments.
- **Density/rhythm view:** note onset markers or a per-bar density histogram; `startBeat` supports rhythmic inspection without MIDI parsing.
- **Statistics panel:** note count, unique notes, pitch range, average note length/velocity, octave jumps, section count, total bars, and total beats.
- **Behavior inspector:** compare section style/default-derived effective dimensions and user overrides to help explain why sections differ.
- Keep all views derived from `notes`, `statistics`, and effective configuration. For long arrangements (up to 4096 bars), virtualize or downsample overview views and provide a detailed zoom window.
- Audio playback is outside the current API. A browser-side synthesizer or MIDI playback feature would be a separate dependency and product decision; do not imply the API returns audio.

## Minimum viable web application

Deliver one workspace that can:

1. Load preset/progression catalogs and present core generation settings.
2. Generate a package and display its piano roll, section timeline, and compact statistics.
3. Edit configuration, replay it, and make the stale-versus-current state explicit.
4. Create/copy a share link using a URL fragment, decode it on reload, and import/export package JSON.
5. Render/download MIDI from the current package.
6. Handle validation, unsupported versions, network errors, and rate limits accessibly.

Defer accounts, server-side projects, collaboration, direct note editing, audio playback, undoable clip editing, audio rendering, cloud autosave, and background generation jobs.

## Roadmap

### Phase 0 — Contract and deployment readiness

- Confirm the production API URL, TLS, and exact frontend origin; configure `Cors:AllowedOrigins` for that origin.
- Treat the current OpenAPI document as the contract source and verify the frontend's request/response types against it.
- Test browser CORS behavior for JSON package endpoints, MIDI downloads, and the exposed seed header; verify `Cache-Control: no-store`.
- Agree on unsupported-version and local-persistence UX. No backend changes are required for the MVP if configuration meets deployment needs.

### Phase 1 — Generate, visualize, export

- Build responsive generator controls for single mode and built-in arrangements.
- Load presets and progression data; submit `POST /api/generate/package`.
- Render timeline, piano roll, and statistics from package data.
- Add package JSON export and `POST /api/package/midi` download.
- Add loading, validation, empty, network, and `429` states.

### Phase 2 — Edit and replay

- Add custom sections and advanced behavioral/MIDI controls.
- Support package configuration edits with strict seed consistency and dirty-result UI.
- Replay through `POST /api/package/regenerate`; render/download MIDI from the updated package.
- Test replays for single, arrangement, and custom-section packages, including unsupported versions.

### Phase 3 — Share and reload

- Encode current packages through `POST /api/share`; generate fragment-based share URLs.
- Decode on load with `POST /api/share/decode`; provide copy-code and package JSON fallback.
- Test malformed, unsupported, oversized, and practically long share codes across target browsers/platforms.
- Consider explicit browser-only local saves only after privacy and retention UX is agreed.

### Phase 4 — Quality and polish

- Add accessibility review for keyboard operation, screen-reader announcements, contrast, and chart alternatives.
- Test small screens, long arrangements, slow/erroring networks, rate limits, and stale-response races.
- Add browser-level end-to-end coverage for generate → edit → replay → share → reload → MIDI.
- Establish API/engine compatibility messaging and a regression fixture for representative package versions.

## Readiness gaps and decisions

- **CORS is deployment configuration:** production's default allowlist is empty. The UI will fail cross-origin until the deployment origin is explicitly allowed.
- **No direct note-edit API:** note changes in a submitted package are discarded. If note-level editing becomes a requirement, it needs a separate contract and product semantics for how edits interact with seeded replay and MIDI.
- **No remote persistence:** reload recovery requires a share link, imported package, or optional browser-local save. There is no account/project API.
- **Share-code practical size:** the API permits codes up to 262,144 characters; actual URL/channel limits can be substantially lower. Always offer copyable raw code and JSON export, and test link length in target clients.
- **Engine compatibility:** replay rejects unsupported format/engine versions; a share code does not include historical note snapshots. Preserve a downloaded package JSON when long-term recovery matters.
- **No audio or job APIs:** visualization is supported, but audio preview, asynchronous progress, and server-side cancellation are not.
- **Rate limiting:** the default is 100 requests per 60 seconds per client IP and is per process instance. The UI should avoid redundant calls, handle `429`, and avoid automatic retry storms.
