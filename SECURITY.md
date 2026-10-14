# Security policy

The latest released version is the maintained baseline; there is no promised support period for old
builds. Back up data before updating and review release notes.

## Private reports

Use this GitHub repository's Security -> Report a vulnerability feature when enabled. Supply version,
affected component, reproduction and a synthetic proof of concept. If private reporting is unavailable,
open an issue requesting a private channel without disclosure details. Do not post real screenshots,
databases, credentials or tokens. A maintainer should discuss a fix/release before public disclosure;
there is no automated response service or promised acknowledgement deadline.

## Review boundaries

Treat images/OCR, imported archives and model responses as untrusted. Review path ownership, linked
folders, traversal/size limits, schema migration, handles, cancellation, inference endpoints, model
integrity and release dependencies. Data/backups are unencrypted; AI is not a security guarantee.
See [PRIVACY.md](PRIVACY.md).

## Downloads

Release workflows build version tags, include source/notices and publish SHA-256 checksums. Windows
builds are currently unsigned. Checksums detect corruption/mismatch but do not replace checking origin.
Downloaded model weights retain their own licenses, not the project's license.
