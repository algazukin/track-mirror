# Privacy and data handling

This describes the current application implementation, not a guarantee about Hubstaff, Windows,
your employer's software, third-party runtime internals or changes in a modified build.

## Information read and retained

| Data | Purpose and retention |
| --- | --- |
| Local Hubstaff JPGs | Separate review copies; retained until local cleanup/manual removal. |
| Screenshot metadata | SQLite IDs, account/file names, UTC times, flags and reviewed state. |
| Activity intervals | Account IDs, UTC interval/receive times and tracked/mouse/keyboard/overall seconds. |
| AI request diagnostics | Opaque request IDs, numeric request bytes, OCR counts, token counts when supplied, duration and safe failure codes; stored per category in SQLite. No request text, image bytes or raw model response. |
| AI state | Image hashes, queue/request IDs, per-category versions/checkpoints, structured findings/regions and decisions. |
| Preferences | Language, notifications, startup, timer mode/intervals/CLI path, model/categories and retention in `settings.json`. |
| Input idle state | Last-input timestamp and elapsed idle time for the current Windows session, in memory for optional timer control. No key contents are read or stored. |
| Folder records | LocalAppData preferences and transfer markers/redirects store paths and IDs. |
| Legacy metadata | Imported JSON remains in the data folder and can contain earlier metadata. |
| Backups | User-created ZIPs contain database, settings, legacy metadata and image copies. |
| Diagnostics | Startup errors can contain exception details and local paths. |

Activity reads Hubstaff's local `TrackedActivity.xml`. The viewer does not record keystrokes or
install keyboard/mouse hooks. Opt-in timer control reads Windows last-input timing and uses CLI status
project/task identifiers only in memory to avoid resuming a different selection. CLI output is not logged.
App names, URLs, project/task IDs and raw XML are not persisted by
the activity store. Screenshots can contain any visible desktop text/windows.

The AI database retains category/severity/reason codes and bounding regions, not recognised secret
values, raw OCR transcripts or raw model responses. Inference processes images and recognised text
in memory. Images remain sensitive regardless of findings or false-positive decisions.

The optional floating screenshot timer reads scheduling events from Hubstaff's local log and its most
recent rotated copy, and task/project identifiers from its XML files. These records are processed in memory; TrackMirror does not
copy raw Hubstaff logs into its database or upload them. The timer only checks whether the Hubstaff
client process is running; it does not inspect process memory or control tracking. Its enabled state,
highlight threshold, appearance and screen position are stored in the existing settings file.

## Network behaviour

The viewer has no application analytics, advertising, registration, automatic crash upload, Hubstaff
API client or cloud AI/API-key provider. It does not upload images/activity to a viewer service.
Optional timer control sends local `status`, `stop` and `resume` commands through the installed
Hubstaff CLI after the user enables it and grants Scripted control permission. This can change tracked
time and future capture activity. Hubstaff manages its own uploads and server records; the viewer
does not replace or delete them. Hubstaff may contact its service as part of normal timer operation.

Optional installation performs HTTPS downloads of the pinned Ollama Windows runtime from GitHub and
models from Ollama registry/blob infrastructure. Those services may see ordinary download metadata,
such as IP address and requested model; these downloads contain no screenshots. Source builds contact
NuGet for dependencies.

Inference sends image/OCR data by HTTP to a private loopback Ollama process at `127.0.0.1:11437`.
Installation uses `127.0.0.1:11438`. The viewer launches its own process with loopback binding, private
models and `OLLAMA_NO_CLOUD=1`; it refuses to attach to an unknown service occupying its endpoint.
Local processes with sufficient access may observe loopback traffic or read files. Third-party
runtime/model behaviour has separate upstream terms.

There is no automatic application updater. Normal use does not contact the GitHub Releases workflow.
CI handles repository code and synthetic fixtures, not a user's workday.

## Storage and deletion

The viewer does not encrypt data, legacy metadata or backups. Windows permissions apply; anyone
with access to your account/data/ZIPs may read them. A sync service can upload a folder independently
of the viewer. Choose private data/backup locations.

Fresh installations use per-installation LocalAppData; existing portable installs keep an adjacent
data folder. Folder transfer deliberately retains the source for recovery. Models remain beside the
executable. The current viewer requires/stores no API keys or passwords.

Retention is optional, disabled by default and protects flagged/unread captures by default. It removes
eligible JPGs and related AI metadata via durable deletion intents. Locked JPGs can remain until retry.
Activity, backups, old migration JSON and previous transfer folders are retained. Filesystem deletion
is not secure erasure. Deleting a viewer copy does not remove an original/server screenshot in Hubstaff.
To remove all viewer copies, also consider backups, prior folders and external sync/backup services.

Uninstalling the program retains data profiles, custom/portable data, backups and downloaded models.
Use the application's storage controls or remove your own data separately when desired.

Release installers, ZIPs and source exports are built from validated staging/file lists. User datasets, diagnostics,
backups, downloaded models and internal planning documents are excluded. The icon, OCR assets,
translations and synthetic fixtures are public project assets.

## Issue reports

Do not post databases, workday images, OCR text, raw model requests, tokens or personal paths publicly.
Reproduce issues with synthetic/redacted examples. Follow [SECURITY.md](SECURITY.md) for vulnerabilities.
Collection/network/storage changes must update this document and receive privacy-focused review.
