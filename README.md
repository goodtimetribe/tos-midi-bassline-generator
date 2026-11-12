# BasslineGenerator

[![CI](https://github.com/goodtimetribe/tos-midi-bassline-generator/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/goodtimetribe/tos-midi-bassline-generator/actions/workflows/ci.yml)

A .NET 10 library, console application, and HTTP API for generating motif-based electronic
basslines as standard MIDI type 1 files. Import the resulting MIDI into a DAW or
a compatible MIDI workflow and assign it to a bass instrument.

`src/BasslineGenerator.Core` contains the reusable generation and MIDI logic;
`src/BasslineGenerator.Cli` handles console interaction, and
`src/BasslineGenerator.Api` serves stateless HTTP requests; both reference Core.
`tests/BasslineGenerator.Tests` tests Core, CLI, and API behavior.

## Architecture

```text
CLI user -> BasslineGenerator.Cli -> BasslineGenerator.Core -> MIDI file on disk
HTTP client -> BasslineGenerator.Api -> BasslineGenerator.Core -> MIDI in memory
                                                            -> HTTP response
```

Core has no dependency on either front end. The CLI writes files only after
confirmation; the API does not write generated files or request data.

## Build and run

Install the .NET 10 SDK, then from the repository root:

```sh
dotnet build
dotnet test
dotnet run --project src/BasslineGenerator.Cli
```

Alternatively, run `dotnet run` from the `src/BasslineGenerator.Cli` directory. Choose
single section or full arrangement, style (for a single section), key, scale,
BPM, bars and progression. Confirm with `Y` to write a uniquely named
`bassline-<id>.mid` file in the current working directory. Existing files are
not overwritten. The program displays the output path and the actual
progression and motif selected.

### Command-line options

Use `--help` (or `-h`) to list all options, accepted values, and progression
names. Any omitted option is requested interactively; style is only used for a
single section. For example, to generate without prompts:

```sh
dotnet run --project src/BasslineGenerator.Cli -- --mode single --style acid --key A --scale minor --bpm 140 --bars 16 --progression "Synthwave" --yes
```

Mode, style, and scale also accept the numbers shown in their prompts.
Progression accepts its displayed number, `random`, or its exact, case-sensitive
display name, ID, or legacy chord sequence. The first six numeric choices retain
their original meaning. Pass `--seed <integer>` to make output reproducible; the
actual seed is printed with the generation result. Use `--confirm yes` or `--confirm no` to answer the generation prompt
explicitly; `--yes` is shorthand for `--confirm yes`. Options may also be
written as `--option=value`. Invalid options or values produce an error.

## HTTP API

### Web application

The React frontend lives in `src/BasslineGenerator.Web`. Start the API as shown
below, then in a second terminal run:

```sh
cd src/BasslineGenerator.Web
npm ci
npm run dev
```

Open the Vite URL printed in the terminal. The development server proxies API
and OpenAPI requests to `http://localhost:5080`; run the API with the development
launch profile. The workspace generates and replays packages, displays a
read-only piano roll, previews the generated notes, downloads MIDI, and supports
local JSON package files and share codes. The preview uses a lightweight Web
Audio synthesized bass by default; it may sound different from a DAW. Play,
pause, stop, and restart control the preview, and synth volume is remembered for
the current browser session. Preview playback is bass-only. Choose **Download
Bass MIDI** for bass alone or **Download Bass + Chords MIDI** for a Type 1 MIDI
file with separate bass and sustained chord tracks.
Draft changes take effect after regeneration, which stops and resets playback.
The bass track uses the generated bass channel; chords use MIDI channel 2.
If both use channel 2, the workspace warns but still exports both unchanged.
They may play on the same instrument when routed to the same MIDI destination.
Change the bass channel and regenerate, or route the tracks separately in your DAW.

To play through a MIDI device or virtual port, connect it, select **Refresh
Devices**, grant browser MIDI permission, and choose it under **Output**.
Web MIDI requires a compatible browser and a secure context (HTTPS or
localhost). Select **MIDI Channel** (1-16) to match the receiving device or
DAW; it does not change the generated bassline or exported MIDI. Routing changes preserve the
playback position. The synth volume control does not affect MIDI velocity.
Disconnecting a selected device pauses playback at its current position; reconnect
and refresh, or select another output, before resuming.

Use **Profile** to recall local playback routing. Select a profile, adjust its
output or channel, and choose **Update Profile** to keep the changes; enter a
new name and choose **Save Profile** to create another. Profiles and the active
selection are stored in browser local storage, not in packages, exports, or share
codes. Circuit Tracks presets automatically match a single connected output
whose name contains "Circuit Tracks"; if no unique match is found, select the
device explicitly and update the profile. External MIDI channel presets likewise
need an output selected. MIDI permission and a connected device are still
required, and unavailable outputs never silently fall back to the browser synth.

The preview transport schedules package notes against the Web Audio clock with a
short look-ahead for both outputs. Browser background-tab timer throttling may
interrupt playback; keep the tab visible for reliable playback.

Run `npm test` and `npm run build` in the frontend directory. The production
build is a static site in `dist/`. Configure `VITE_API_BASE_URL` at build time
to the API origin (leave it unset when the site and API share an origin), and
allow the exact frontend origin in the API CORS configuration for cross-origin
hosting. Root-note and scale choices follow the API's documented OpenAPI
suggestions; keep them aligned if the API contract changes. The existing deployment workflow deploys only the
API; publishing the static frontend requires separate hosting configuration.
Packages are saved only when explicitly downloaded by the user.

Run the API from the repository root:

```sh
dotnet run --project src/BasslineGenerator.Api
```

The development launch profile listens at `http://localhost:5080`; open
`http://localhost:5080/swagger` for interactive OpenAPI documentation.
Swagger and its OpenAPI document are also publicly available in Production
while the API is being developed, at
`https://bassapi.beyond-earth.net/swagger` and `/swagger/v1/swagger.json`.
They expose the API contract and allow visitors to submit requests; revisit
this exposure before deploying sensitive endpoints. Set `ASPNETCORE_ENVIRONMENT` and
`ASPNETCORE_URLS` (or use standard ASP.NET Core configuration) for other
environments and listen addresses.

In Swagger, expand **Generation → POST /api/generate** and select **Try it out**
to choose notes, scales, styles, modes, and progressions from dropdowns and enter the tempo and
length, seed, MIDI and humanization settings. Enter custom sections as a JSON
array in the sections field and leave the field to apply it. The form updates the JSON request editor, which remains available for
manual editing. The examples selector offers Dreamy ambient, Pop, Acid, Rave,
and custom-section starting points; selecting an example also updates the form.
The progression dropdown shows friendly names and displays each selection's
chord sequence, description, mood, and suggested use.
With the default JSON response, select **Download MIDI** beside the response body
to download the same bassline. This sends a second request using the returned
seed and counts toward the API rate limit. Alternatively, select `audio/midi`
as the response media type before executing to download MIDI directly.

| Method and path | Description |
| --- | --- |
| `GET /api/health` | Returns `{"status":"healthy","version":"1.0.0"}`. |
| `GET /api/presets` | Returns `["Dreamy","Pop","Acid","Rave"]`. |
| `GET /api/progressions` | Returns progression IDs, names, descriptions, chord sequences, moods and suggested uses. |
| `POST /api/generate` | Returns a downloadable `bassline.mid` (`audio/midi`) by default, or effective generation metadata (`application/json`) when requested. |
| `POST /api/generate/package` | Same request as `/api/generate`; returns a JSON generation package (no MIDI). |
| `POST /api/package/regenerate` | Replays a saved or edited package and returns a fresh one. |
| `POST /api/package/midi` | Replays a saved or edited package and returns a downloadable MIDI file. |
| `POST /api/package/combined-midi` | Replays a package and returns a Type 1 MIDI file with conductor, bass and chord tracks. |
| `POST /api/share` | Package in, `{"shareCode":"..."}` out. |
| `POST /api/share/decode` | `{"shareCode":"..."}` in, package out. |

Generation remains a MIDI download by default for existing clients. To receive a
JSON configuration instead, send `Accept: application/json` to the same endpoint.
MIDI responses also include the generated seed in the `X-Generation-Seed` header.
The response includes the generated seed, resolved progression, section styles
and their effective behavior dimensions, MIDI settings, and note statistics.
Its `request` object is ready to submit again with the returned seed.
`configuration.requestedProgression` records the original optional value
(including `null`), while `progressionId`, name, and chord sequence report the
selected progression. `midiSha256` lets clients verify a replay. For
arrangements, effective dimensions are included for every section because style
defaults vary by section.

### Generation packages and share codes

A generation package is a portable, versioned JSON asset:
`{"version":1,"engineVersion":"1.0.0","seed":12345,"effectiveConfiguration":{...},"statistics":{...},"notes":[...]}`.
`version` identifies the package format; `engineVersion` identifies the generation
algorithm and its compatibility-sensitive defaults. The effective configuration
records the seed, key, scale, style, selected progression metadata, MIDI settings
and each section's behavior dimensions. Replay also depends on the matching
engine, including its preset density and gate and progression library. A random
seed is chosen when none is supplied and reported in the package.
Each note is `{"note":"A2","midiNote":45,"startBeat":4.0,"durationBeats":0.5,"velocity":96}`
(C4 = 60; beats are quarter notes), enough for piano-roll, timeline and density
views without parsing MIDI. Statistics include note count, unique notes,
lowest/highest note, average length (beats) and velocity, octave jumps, section
count and arrangement length.

To save, load, edit or clone, keep the package JSON. Edit
`effectiveConfiguration` (per-section `behavior` values are authoritative on
replay) and submit it to `/api/package/regenerate`; `notes` and `statistics`
are ignored on input and recomputed. Submit the same package to
`/api/package/midi` to download the MIDI produced by that replay. `seed` must
match the configuration's seed. When editing progression, change
`requestedProgression` (null selects randomly using the seed); the selected
progression metadata is derived on replay.

The current engine supports package format 1 and engine `1.0.0`. Older format 1
packages and share codes without `engineVersion` are treated as legacy engine
`1.0.0`; their original engine cannot be independently verified. Successful
replay returns the explicit version. Unsupported format or
engine versions are rejected, not silently replayed with a different engine.
Future changes affecting generated output must increment the engine version and
either retain a compatible replay implementation or reject old assets explicitly.
There is no automatic migration or historical-engine hosting today. Identical
notes and MIDI are expected for the same package and engine implementation in
the same supported runtime/environment; engine identity alone does not guarantee
byte-for-byte output across runtime or dependency changes. Keep the saved JSON
note snapshot or MIDI if the historical output must remain available.

Share codes are deterministic URL-safe encodings (deflate + base64url) of the
format version, engine version, seed and effective configuration; decoding
regenerates notes and statistics. Encoded JSON is limited to 1,048,576 bytes and
the resulting code to 262,144 characters; sharing returns 400 if either limit is
exceeded. Codes do not contain the note snapshot. Nothing is stored server-side. Existing
`POST /api/generate` behavior is unchanged; statistics only gained fields.

For example, one entry in the progression response is:

```json
{
  "id": "synthwave",
  "name": "Synthwave",
  "description": "Retro and nostalgic.",
  "chordSequence": ["i", "VI", "III", "VII"],
  "mood": "Nostalgic",
  "suggestedUse": "Synthwave and retro electronic music."
}
```

### Generate Sample Bassline

Select a Swagger example, or use this Acid example
directly. The `-o` option saves the MIDI response **on the client**:

```sh
curl -X POST http://localhost:5080/api/generate \
  -H 'Content-Type: application/json' \
  -d '{"rootNote":"A","scaleType":"Minor","style":"Acid","bpm":140,"bars":16,"seed":12345,"movement":70,"groove":80,"mutation":35,"tension":65,"repetition":85}' \
  -o bassline.mid
```

The root, scale, style, BPM and bars fields are required. `rootNote` supports
natural notes and the same sharps and flats as the CLI (for example `C#` and
`Bb`); `scaleType` is one of the scales listed below, with `Natural Minor` an
alias for `Minor`; and `style` is one of the returned presets. These names are
case-insensitive. `bpm` is an integer from 30 to 300, and `bars` is an integer
from 1 to 512, inclusive.
Optional `mode` is case-sensitive: `single` (the default) generates one section,
and `arrangement` generates the six sections described below. In arrangement
mode, `bars` is per section; `style` is still required
but the arrangement uses its own section styles. Optional `progression` selects
one of the case-sensitive display names or IDs listed in Swagger or the
progressions endpoint. Existing Roman-numeral sequence values remain supported;
omit the field or use `null` for a
random progression chosen for the scale. Omit `mode` or use `null` for single
mode.

To specify a custom arrangement instead, provide `sections`, for example
`[{"name":"Intro","style":"Dreamy","bars":4},{"name":"Drop","style":"Acid","bars":8}]`.
The list must be nonempty, each section must be 1–512 bars, and total at most
4096 bars; it overrides single mode and cannot be combined with
`mode: "arrangement"`. The top-level `style` and `bars` are still required but
are not used for custom sections.

Specify `seed` as a signed 32-bit integer to reproduce the same MIDI from the
same settings and engine version, including when progression is random. Save
both the seed and settings to recall a pattern; when omitted, the generated seed
is returned in the `X-Generation-Seed` header and in JSON responses. Optional
MIDI and humanization controls are `channel` (0–15, default
0), `velocity` (1–127, default 100), `velocityVariation` (0–127, default 0),
`timingVariation` (0–30 ticks, default 0), `swing` (0–0.5 of an eighth note,
default 0), and `accentDownbeats` (boolean, default false). Omit them or use
`null` for defaults.

The five optional integer dimensions range from 0 to 100. Omit a dimension or
set it to `null` to use the selected style's default; individual values override
only that dimension. In arrangements, defaults follow each section's style,
while explicit overrides apply to every section.

| Scale | Semitone intervals | Character |
| --- | --- | --- |
| Major | 0, 2, 4, 5, 7, 9, 11 | Bright and uplifting |
| Minor | 0, 2, 3, 5, 7, 8, 10 | Dark and reflective (natural minor) |
| Dorian | 0, 2, 3, 5, 7, 9, 10 | Minor with a bright, soulful color |
| Mixolydian | 0, 2, 4, 5, 7, 9, 10 | Warm, bluesy and relaxed |
| Lydian | 0, 2, 4, 6, 7, 9, 11 | Bright, floating, cinematic |
| Phrygian | 0, 1, 3, 5, 7, 8, 10 | Dark, tense, exotic |
| Harmonic Minor | 0, 2, 3, 5, 7, 8, 11 | Dramatic, epic, fantasy-inspired |
| Melodic Minor | 0, 2, 3, 5, 7, 9, 11 | Sophisticated, jazzy, modern |
| Major Pentatonic | 0, 2, 4, 7, 9 | Simple, open, uplifting |
| Minor Pentatonic | 0, 3, 5, 7, 10 | Rock, blues, synthwave-friendly |
| Blues | 0, 3, 5, 6, 7, 10 | Expressive, gritty, traditional blues vocabulary |

Melodic Minor uses the ascending form in both directions. All generated notes,
including chord and passing tones, are constrained to the chosen scale; chord
tones outside it are mapped to the nearest scale tone. Try Lydian or Dorian
with Dreamy, Minor or Phrygian with Acid, Minor or Harmonic Minor with Rave,
and Major or Major Pentatonic with Pop. Swagger examples use these pairings;
styles never override the selected scale. The API still requires a scale, and
the Core default remains Minor.

| Style | Movement | Groove | Mutation | Tension | Repetition |
| --- | ---: | ---: | ---: | ---: | ---: |
| Dreamy | 60 | 20 | 75 | 15 | 45 |
| Pop | 40 | 50 | 25 | 20 | 80 |
| Acid | 75 | 70 | 50 | 80 | 85 |
| Rave | 65 | 55 | 35 | 60 | 70 |

Movement increases scale traversal and intervals; groove adds syncopation,
offbeat accents and rests; mutation evolves phrases over time; tension adds
less stable scale tones and approaches; repetition favors recurring motifs
and rhythms. Mutation and repetition are independent: high values for both
produce a recurring theme that gradually evolves. Style also retains its
characteristic density and note gate.

Invalid input returns HTTP 400, for example
`{"error":"BPM must be an integer between 30 and 300."}`. Unexpected failures
return HTTP 500 and `{"error":"An unexpected error occurred."}`. A rate-limited
request returns HTTP 429 and `{"error":"Too many requests."}`. The future
analysis, variation, and arrangement controllers are extension points, not
implemented routes.

### Configuration and deployment

`src/BasslineGenerator.Api/appsettings.json` configures allowed CORS origins and
the default limit of 100 requests per 60 seconds per client IP. No origins are
allowed by default outside Development. `appsettings.Development.json` permits
`http://localhost:3000` and `http://localhost:5173` for local development.
Replace these with explicit website origins for deployment, for example:

```json
{
  "Cors": { "AllowedOrigins": ["https://music.example.com"] },
  "RateLimit": { "PermitLimit": 100, "WindowSeconds": 60 }
}
```

Configuration can also be supplied through environment variables such as
`Cors__AllowedOrigins__0=https://music.example.com`,
`RateLimit__PermitLimit=100`, and `RateLimit__WindowSeconds=60`. ASP.NET Core
merges configuration sources; when overriding an array, explicitly replace or
remove any inherited origins you do not want to allow.

The API is stateless and can run on a VPS or in a container behind Nginx.
Rate-limit counters and IP keys exist only temporarily in process memory;
limits are per instance and reset on restart. By default the API uses the
socket peer's IP and does **not** trust forwarded headers. Behind a reverse
proxy, all clients will otherwise share the proxy's limit: configure ASP.NET
Core forwarded headers for only the known proxy or network before relying on
per-client limits. Never trust arbitrary client-supplied forwarding headers.
Configure Nginx, hosting providers, and any telemetry separately to avoid
access logs, request-body capture, IP retention, and response caching if the
same privacy guarantee is required end to end.

The API keeps generated MIDI and settings in memory for the request only,
returns `Cache-Control: no-store`, and does not store generated content,
settings, request bodies, or IP addresses on disk. It has no database, accounts,
authentication, file storage, or session state. Its application logs contain
fixed operational messages, not musical data or exception details. Clients and
intermediaries remain responsible for their own handling of the response.

## CI/CD architecture

GitHub Actions runs the CI workflow on pull requests and pushes to `main`,
or manually from **Actions → CI**. It restores, builds, and tests the solution
with the .NET 10 SDK, then publishes the API. The `basslinegenerator-api`
workflow artifact is retained for seven days.

After validation succeeds, pushes to `main` deploy automatically. To deploy
manually, select the `main` branch and enable **Deploy the validated main
branch to the VPS** when running **Actions → CI**. Leave it disabled for a
CI-only run. Pull requests and reusable workflow calls do not deploy.
Deployment connects to the VPS over SSH, pulls `main`, publishes to
`/opt/basslinegenerator`, restarts the `basslinegenerator` systemd service,
and checks both service status and the public health endpoint. The artifact
is for inspection or download; deployment publishes from the VPS checkout.

### Developer flow

```text
Create branch → Pull request → GitHub Actions build/test → Merge to main
  → Validate deployment commit → Automatic deploy → Health check
```

Configure branch protection or a ruleset to require the CI check before
merging. A manual deployment also validates `main` before deploying it.

### Production topology

```text
GitHub main -> CI validation -> Deploy to VPS -> SSH -> VPS checkout
                                                  -> dotnet publish -> /opt/basslinegenerator
                                                  -> systemd restart -> public health check
HTTP client -> Cloudflare / Apache -> basslinegenerator systemd service
                                  -> API -> Core -> in-memory MIDI response
```

The VPS hosts the API behind Apache, with Cloudflare involved in public
routing. The systemd service runs the published application; production
configuration belongs outside the publish directory. There is no database or
server-side MIDI storage. Deployment publishes in place, rather than swapping
versioned releases, and may cause a brief interruption during restart.

### Deployment setup

The existing Ubuntu VPS must have the .NET 10 SDK, `git`, `curl`, Apache,
and the configured `basslinegenerator` systemd service. The SSH deployment
user needs:

- A clean checkout of this repository on the `main` branch at
  `/usr/local/src/tos-midi-bassline-generator`, with an `origin` remote and
  non-interactive GitHub read access for `git pull --ff-only origin main`.
- Write access to the checkout and `/opt/basslinegenerator`, and permission
  to run `sudo -n systemctl restart basslinegenerator` without a password.
  Limit the sudo permission to that command. The service must run the API
  from `/opt/basslinegenerator`.
- A configured SSH authorized key for the deployment key. Keep production
  settings in the service environment or another location outside the
  publish directory: publishing may overwrite files in that directory.

In the repository's **Settings → Secrets and variables → Actions**, add
these repository secrets:

| Secret | Value |
| --- | --- |
| `VPS_HOST` | VPS hostname or IP address, reachable over SSH on port 22. |
| `VPS_USERNAME` | SSH deployment user. |
| `VPS_SSH_KEY` | Private SSH key authorized for that user on the VPS. |
| `VPS_KNOWN_HOSTS` | SSH `known_hosts` entry for the VPS, with its host key fingerprint verified through a trusted channel. |

Use the VPS's actual SSH host key, not the deployment user's key. Verify its
fingerprint independently before adding the `known_hosts` entry; update the
secret if the server host key is intentionally rotated.

Deployments are serialized and refuse a dirty checkout or a checkout that
does not match the commit that passed validation. If `main` advances while
an older deployment is waiting, that deployment can fail instead of deploying
untested code; the newer push starts its own deployment. Publishing is
in-place and there is no automatic rollback.

### Rollback

Prefer reverting the faulty change on `main` and letting the normal deployment
workflow validate and deploy the revert. Identify the last known-good commit
and the changes to undo, then create a revert pull request (for a merge commit,
select the appropriate mainline parent). After it merges, monitor **Actions →
CI** and verify `https://bassapi.beyond-earth.net/api/health` and the
affected behavior. If deployment fails, use the troubleshooting steps below.

Do not reset or detach the VPS checkout to an older commit: subsequent
deployments require a clean `main` checkout and the validated commit. A failed
health check does not restore the previous files; the service may remain
unhealthy until a corrective deployment succeeds. Recovery also depends on the
VPS, SSH access, and GitHub Actions being available.

### Deployment troubleshooting

- Check the failed job and step under **Actions**. Build or test failures
  prevent deployment; fix them before retrying.
- For SSH failures, check the secrets, authorized key, network access, and
  independently verified host key. For Git failures, check the server's
  `origin` credentials, branch, clean working tree, and fast-forward status.
- For publish or restart failures, check the VPS .NET SDK, directory
  ownership, and passwordless sudo permission. Inspect
  `sudo systemctl status basslinegenerator` and
  `sudo journalctl -u basslinegenerator -n 100 --no-pager` on the VPS.
- If the health check fails, inspect the service and Apache logs, then check
  `curl --fail --show-error https://bassapi.beyond-earth.net/api/health` from
  the VPS. Check Apache and Cloudflare routing and ensure the endpoint is not
  served from cache. A failed health check does not undo the deployment;
  resolve the issue or manually restore a known-good version.

## Musical model

Notes use 4/4 time and a 480 PPQ grid. Rhythms are weighted selections of
repeated bar patterns; phrase endings leave space for longer notes. A single
chord-relative motif is reused throughout the song, with preset-specific
variations. The progression advances one chord per bar, including across section
boundaries. Dreamy is sparse, Pop is moderate, Acid adds scale approaches and
octave movement, and Rave uses dense, repetitive patterns. The full arrangement
uses Intro (Dreamy), Build (Pop), Breakdown (Dreamy), Drop (Acid), Peak (Rave),
and Outro (Dreamy); the CLI and API arrangement bar counts are **per section**.

Scale degrees and pitch classes support all the scales listed above; Core also
accepts `Natural Minor` as an alias for `Minor` and roots with sharps or flats
such as `C#` and `Bb`. Progression chord symbols preserve Roman numeral case and
describe their written major/minor chord qualities and roots relative to the
tonic. Chord tones that do not belong to the selected scale are mapped to the
nearest scale tone, so their sounding quality can differ from the written
progression. Random selection chooses from the major-family or minor-family
progressions according to the scale. Presets with identical chord sequences,
such as Synthwave and Trance Lift, differ in their intended use rather than
their harmony. The progression cycles continuously throughout the song.

## Programmatic use

Reference `BasslineGenerator.Core` and construct the generator directly:

```csharp
using BasslineGenerator.Export;
using BasslineGenerator.Generation;
using BasslineGenerator.Models;

var exporter = new MidiExporter();
var generator = new BasslineGenerator.Generation.BasslineGenerator();
var settings = new GeneratorSettings
{
    RootNote = "A", ScaleType = "Minor", BPM = 140, Style = SongWorld.Acid,
    Progression = "Synthwave",         // null selects randomly
    Seed = 42,                         // optional reproducible output
    Velocity = 100, Channel = 0,       // MIDI channel is zero-based (0–15)
    VelocityVariation = 8, TimingVariation = 10, Swing = 0.15,
    AccentDownbeats = true,
    Sections = [
        new("Intro", SongWorld.Dreamy, 16),
        new("Build", SongWorld.Pop, 16),
        new("Acid", SongWorld.Acid, 32),
        new("Peak", SongWorld.Rave, 32),
        new("Outro", SongWorld.Dreamy, 16)
    ]
};
var result = generator.Generate(settings);
exporter.Save(result, "bassline.mid");
```

`Save` overwrites an existing file at the specified path. Use `SaveNew` to
create a file only if the path does not already exist. Both accept either the
generation result or its in-memory MIDI file. `IStreamMidiExporter.Write` writes
the result to a caller-owned stream without closing it.

`Generate` does not write to disk. Its result exposes the notes, in-memory MIDI
file, selected motif, progression, sections, key, scale, requested style, BPM,
total bar count and note statistics. For arrangements, `Style` is the requested
settings value; the actual style of each part is in `Sections`. Use
`IMidiExporter` to abstract saving, or save the returned MIDI file separately.
For custom presets or rhythm generation, use the generator constructor accepting
the section generator, motif generator, MIDI file factory, and presets.

Omit `Sections` to use `Style` and `Bars` for a single section, or use
`BasslineGenerator.Generation.BasslineGenerator.FullArrangement(barsPerSection)`.
Sections are consecutive, each section is at most 512 bars, and the total is at
most 4096 bars. BPM is 30–300; velocity is 1–127; velocity variation is 0–127;
timing variation is 0–30 ticks; swing is
0–0.5 of an eighth note. The five optional musical dimensions are 0–100;
when omitted, each section uses its style's defaults. Groove can accent offbeats
above the base velocity, independently of the optional downbeat accent.
Humanization defaults to off. Notes are quantized when
timing variation and swing are zero. The exporter writes a conductor track
containing tempo and time signature and a separate note track.