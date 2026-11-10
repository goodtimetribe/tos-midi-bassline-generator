# Frontend technology decision

## Recommendation

**Primary: React + TypeScript, built as a client-rendered Vite application.** It fits the MVP's single interactive workspace, static hosting, browser-based package files and fragment links, and gives the team broad choices for visualization and accessible components. The architecture should remain framework-light: a typed API boundary, a reducer for the editing workflow, and a separately testable piano-roll renderer.

**Runner-up: Vue + TypeScript, also as a client-rendered Vite application.** Vue provides similarly straightforward static deployment and browser integration, with an approachable component and reactive-state model. Choose Vue instead if the implementers have stronger Vue experience; there is no inherent piano-roll performance advantage for React.

This decision evaluates the existing [MVP frontend design](frontend-design.md); there is no `frontend-mvp-design.md` in the repository. The API remains the backend and is considered feature complete. The current repository is a .NET 10 solution with CI that builds, tests, and deploys the API; adding any JavaScript frontend would introduce a second toolchain and separate build/deployment responsibilities.

## Decision matrix

Scores are **1–5, higher is a better fit for this particular MVP**, not a benchmark or universal framework ranking. They assume an available team with basic web/TypeScript knowledge and no established expertise in any one frontend framework. Learning curve scores reflect the extra concepts needed for this app. Scores are subjective and close differences should not outweigh the team's actual skills.

| Criterion | Weight | React | Next.js | Blazor WASM | Blazor Server | Vue | Svelte |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Piano roll rendering | 20% | 5 | 5 | 3 | 2 | 5 | 5 |
| Stateful editing workflow | 20% | 5 | 5 | 4 | 3 | 5 | 5 |
| Share-code links | 10% | 5 | 5 | 4 | 3 | 5 | 5 |
| Hosting complexity | 15% | 5 | 3 | 4 | 2 | 5 | 5 |
| API integration | 10% | 5 | 5 | 5 | 4 | 5 | 5 |
| Long-term maintainability | 15% | 5 | 4 | 4 | 4 | 4 | 3 |
| Learning curve | 10% | 3 | 3 | 4 | 4 | 4 | 4 |
| **Weighted total / 5** | **100%** | **4.80** | **4.35** | **3.90** | **3.00** | **4.75** | **4.60** |

Weighted total = sum of each score × its percentage weight. The matrix favors client-side interaction and simple static hosting; it does not measure bundle size, runtime speed, or team productivity. Validate rendering and accessibility with representative large packages before treating any option as proven.

### Interpretation by option

| Option | Fit and tradeoffs |
| --- | --- |
| **React** | Browser-native canvas/SVG, file, clipboard, and URL APIs; reducer-based state and a large component/testing ecosystem. Requires choosing conventions and learning hooks/async state. Vite is a build tool, not an application framework with batteries included. |
| **Next.js** | React's interactive capabilities plus routing, server rendering, and server features. Can be statically exported for this MVP, but its server/client boundary, routing conventions, and static-export limitations add concepts without a current need for server rendering or SEO. A server deployment would add another runtime; it is not required if static export meets the app's needs. |
| **Blazor WebAssembly** | C# and .NET familiarity align with the repository; client-side state and static hosting are possible. Browser APIs, canvas libraries, downloads, and clipboard commonly require JavaScript interop, and a .NET runtime is delivered to the browser. Startup payload and interop costs need measurement. Sharing C# DTOs is possible but couples client and server builds; the HTTP contract should remain the boundary. |
| **Blazor Server** | C# throughout and server-side access to .NET libraries, but interactive state lives in server circuits and requires a live connection. Hosting must handle connections, reconnection, memory per user, scaling, and privacy of transient server state. Latency-sensitive piano-roll interaction needs careful browser-side JavaScript isolation to avoid round trips. It is the least aligned with this stateless, client-oriented MVP. |
| **Vue** | Good browser-native rendering and a cohesive component/reactivity model, with static deployment and TypeScript support. The same reducer/store boundaries and rendering virtualization are available; team familiarity can easily make it the better choice. |
| **Svelte** | Concise reactive components and static client deployment; straightforward browser API access. Also capable of the visualization and state model. A smaller ecosystem/hiring assumption and framework-specific conventions make it a slightly less conservative default here; a Svelte-experienced team could reasonably prefer it. |

For all six options, the piano roll's performance depends primarily on viewport culling, drawing strategy, data volume, and accessibility design—not the framework name. All can pass a fragment share code to the same API. A URL fragment is not sent in the normal HTTP request, but application code can read it; clipboard/file access, especially in Blazor, may require browser interop.

## Architecture recommendation

```text
Static frontend (browser)
  React workspace + accessible controls
    → reducer / use-case orchestration
    → domain adapters and piano-roll geometry
    → typed HTTP client → existing ASP.NET Core API
    → browser file, clipboard and fragment-link APIs
```

- **Delivery:** Serve the built assets over HTTPS from a static host or the existing web server. Configure the API's exact frontend origin in its CORS allowlist if cross-origin; same-origin hosting is also possible. CI currently publishes only the API: add a separate frontend build/test artifact and deployment step when implementing, without assuming the existing workflow already deploys it.
- **State:** Keep an immutable last successful package separate from the editable draft, with revision IDs and per-operation pending/error states. A dirty draft must not change the displayed result or the package used for save/share/MIDI. Snapshot requests, cancel or disregard superseded responses, and retain drafts and results on failure.
- **Domain adapters:** New generation uses `POST /api/generate/package`; replay-compatible edits use `POST /api/package/regenerate`. Per-section behavior is authoritative during replay; update section values explicitly, keep both seed fields equal, and update `requestedProgression` rather than derived fields. For style/default or structural rebuilds, use a fresh generation request and warn if per-section edits may be lost.
- **Visualization:** Start with a read-only canvas-based viewport and separately rendered accessible controls, labels, and note-list alternative. Calculate note geometry in pure functions; preserve fractional beat timing, draw only visible notes, and use absolute section boundaries. Canvas is a recommendation, not a guarantee of speed: profile representative and near-maximum arrangements before committing to detailed optimizations. Do not make notes editable or imply audio playback.
- **API client:** Centralize JSON/binary handling, typed DTOs, safe error parsing, cancellation, and explicit handling for `400`, `429`, network/CORS failures, and MIDI content type. Obtain root/scale choices from the documented OpenAPI contract; load presets and progressions from the API.
- **Save and share:** Export/import complete package JSON through explicit local files. Generate/decode share codes through the API; use URL fragments and support raw-code paste and JSON fallback. No automatic storage, analytics containing codes, or server-side project persistence. Preserve incompatible imported files and show errors without discarding the active result.
- **Contract ownership:** Generate TypeScript types from the API OpenAPI document during development/CI or maintain a small checked-in typed contract with drift checks. Validate untrusted imported data at runtime; TypeScript types alone are not validation. Neither option changes API behavior.
- **Accessibility and responsiveness:** Keyboard-operable controls and note navigation, visible focus, screen-reader status, a textual note alternative, and small-screen layouts are part of the MVP, not post-release decoration.

### When to revisit the decision

Choose **Blazor WASM** if the implementation team is substantially stronger in C# and accepts browser-runtime payload and JS interop after a piano-roll proof of concept. Choose **Next.js** if server-rendered public pages, server-side authentication, or a genuine backend-for-frontend become product requirements; static export alone does not require a server runtime. Choose **Blazor Server** only if a persistent server-side session and operational capacity are deliberate requirements. Vue or Svelte are equally credible if the team's expertise outweighs the assumed ecosystem advantage of React.

## Initial project structure

Proposed paths are conceptual; none of these application files exist yet.

```text
src/BasslineGenerator.Web/
├── public/                     # static assets
├── src/
│   ├── app/                    # application shell, configuration, composition
│   ├── api/                    # DTOs, HTTP client, endpoint operations
│   ├── domain/                 # request/package mapping, validation, replay rules
│   ├── state/                  # workspace reducer, events, async orchestration
│   ├── features/
│   │   ├── generation/         # controls, arrangement and seed editors
│   │   ├── result/             # metadata, statistics, result actions
│   │   ├── piano-roll/         # viewport, geometry, renderer, note alternative
│   │   ├── packages/           # file import/export
│   │   └── sharing/            # code/link dialogs and fragment handling
│   ├── components/             # shared accessible UI primitives
│   └── styles/                 # tokens, layout, responsive rules
└── tests/                      # unit, component, and browser-level coverage
```

Keep Core independent of the UI. Do not import server implementation code into frontend rendering or duplicate generation logic. Keep API base URL configuration deployment-specific; never put secrets in a static frontend bundle.

## Proposed implementation order

1. **Foundation and contract.** Create the React/TypeScript/Vite shell, establish API configuration and typed contracts, load presets/progressions, and add responsive layout plus error/status primitives. Add a frontend build and tests to CI while preserving existing .NET build, test, and API deployment.
2. **Generate and inspect metadata.** Implement core controls, request validation, package generation, result/seed/section/statistics display, and preservation of the last result on errors. Verify the draft/result distinction.
3. **Visualize and export.** Build a viewport-culling piano roll, section overview, note inspector, keyboard/text alternative, MIDI download, and complete package JSON export. Exercise timing variation and large arrangements early.
4. **Edit and regenerate.** Add advanced controls, seed choice, custom sections, per-section behavior, dirty-state warnings, replay versus fresh-generation routing, and race handling. Verify that downloads and shares always use the visible generated result.
5. **Load and share.** Add package import/replay, share-code encode/decode, fragment links, clipboard actions, and recovery for malformed, oversized, or unsupported inputs. Preserve unsaved work on replacement.
6. **Release hardening.** Test the end-to-end musician workflow, CORS and deployment, 400/429/offline conditions, stale async responses, accessibility, responsive layouts, and supported/unsupported versions. Validate real share-link length and long/dense visualizations on supported browsers.

MVP completion requires all six steps, including manual local package save/load and share-code workflows. Audio playback, server accounts, cloud storage, and direct note editing remain outside scope.

## Reference material

- [React: starting a project](https://react.dev/learn/start-a-new-react-project)
- [Next.js: static exports](https://nextjs.org/docs/app/guides/static-exports)
- [ASP.NET Core Blazor hosting models](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models?view=aspnetcore-10.0)
- [Blazor JavaScript interoperability](https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/?view=aspnetcore-10.0)
- [Vue: quick start](https://vuejs.org/guide/quick-start.html)
- [Svelte: introduction](https://svelte.dev/docs/svelte/overview)
