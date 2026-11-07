# BasslineGenerator

[![CI](https://github.com/goodtimetribe/tos-midi-bassline-generator/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/goodtimetribe/tos-midi-bassline-generator/actions/workflows/ci.yml)
[![Deploy API](https://github.com/goodtimetribe/tos-midi-bassline-generator/actions/workflows/deploy.yml/badge.svg?branch=main)](https://github.com/goodtimetribe/tos-midi-bassline-generator/actions/workflows/deploy.yml)

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
dotnet run --project src/BasslineGenerator.Cli -- --mode single --style acid --key A --scale minor --bpm 140 --bars 16 --progression "i - VI - III - VII" --yes
```

Mode, style, and scale also accept the numbers shown in their prompts.
Progression accepts its displayed number, `random`, or its exact, case-sensitive
name. Use `--confirm yes` or `--confirm no` to answer the generation prompt
explicitly; `--yes` is shorthand for `--confirm yes`. Options may also be
written as `--option=value`. Invalid options or values produce an error.

## HTTP API

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
to choose notes, scales, and styles from dropdowns and enter the tempo and
length. The form updates the JSON request editor, which remains available for
manual editing. The examples selector offers Dreamy ambient, Pop, Acid, and
Rave starting points; selecting an example also updates the form.

| Method and path | Description |
| --- | --- |
| `GET /api/health` | Returns `{"status":"healthy","version":"1.0.0"}`. |
| `GET /api/presets` | Returns `["Dreamy","Pop","Acid","Rave"]`. |
| `POST /api/generate` | Returns a downloadable `bassline.mid` with content type `audio/midi`. |

### Generate Sample Bassline

Select one of the four style examples in Swagger, or use this Acid example
directly. The `-o` option saves the MIDI response **on the client**:

```sh
curl -X POST http://localhost:5080/api/generate \
  -H 'Content-Type: application/json' \
  -d '{"rootNote":"A","scaleType":"Minor","style":"Acid","bpm":140,"bars":16}' \
  -o bassline.mid
```

All fields are required. Values are case-sensitive: `rootNote` is one of
`A`, `B`, `C`, `D`, `E`, `F`, `G`; `scaleType` is `Major`, `Minor`, `Dorian`,
or `Mixolydian`; and `style` is one of the returned presets. `bpm` is an integer
from 40 to 250, and `bars` an integer from 1 to 256, inclusive. API input is
intentionally narrower than the CLI and Core (which also support accidentals).
Invalid input returns HTTP 400, for example
`{"error":"BPM must be an integer between 40 and 250."}`. Unexpected failures
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

GitHub Actions runs CI on pull requests and pushes to `main`, or manually from
**Actions → CI**: it restores, builds, and tests the solution with the .NET 10
SDK, then publishes the API. The `basslinegenerator-api` workflow artifact is
retained for seven days.

The deployment workflow runs on pushes to `main` and can also be started
manually from **Actions → Deploy API** using the `main` branch. It runs the
same CI checks before connecting to the VPS over SSH. On the VPS it pulls
`main`, publishes to `/opt/basslinegenerator`, restarts the
`basslinegenerator` systemd service, and checks both service status and the
public health endpoint. A push to `main` therefore runs CI twice: once as
the normal CI workflow and once as a deployment prerequisite. The artifact
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
GitHub main -> Deploy API -> CI validation -> SSH -> VPS checkout
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
Deploy API** and verify `https://bassapi.beyond-earth.net/api/health` and the
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
boundaries. Dreamy is sparse, Pop is moderate, Acid adds chromatic approaches and
octave movement, and Rave uses dense, repetitive patterns. The full arrangement
uses Intro (Dreamy), Build (Pop), Breakdown (Dreamy), Drop (Acid), Peak (Rave),
and Outro (Dreamy); the CLI bar count is **per section**.

Scale degrees and pitch classes support Major, Natural Minor (`Minor`), Dorian,
and Mixolydian; sharps and flats such as `C#` and `Bb` are accepted. Progression
names preserve Roman numeral case and specify actual major/minor chord qualities
and semitone roots relative to the selected tonic. Consequently, a chosen
progression can deliberately borrow notes outside the selected scale (including
the major V in `i - V - VI - IV`); scale tones are used for non-chromatic
passing notes. Random selection chooses from the major-family or minor-family
progressions according to the scale.

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
    Progression = "i - VI - III - VII", // null selects randomly
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
Sections are consecutive and total at most 4096 bars. BPM is 4–1000; velocity is
1–127; velocity variation is 0–127; timing variation is 0–30 ticks; swing is
0–0.5 of an eighth note. Humanization defaults to off. Notes are quantized when
timing variation and swing are zero. The exporter writes a conductor track
containing tempo and time signature and a separate note track.