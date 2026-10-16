# Contributing

Report reproducible bugs or propose a focused change using GitHub Issues. Include the game build, mod version, dependency versions, expected behavior, actual behavior, and minimal reproduction steps. A relevant log excerpt is more useful than an unfiltered full mod-list dump; remove tokens and personal paths.

Before submitting a pull request:

1. Read [BUILDING.md](BUILDING.md) and [COMPATIBILITY.md](COMPATIBILITY.md).
2. Keep public package IDs, definition names, and saved fields stable unless a migration is provided.
3. Preserve eligibility and lifecycle rules and avoid unrelated behavior changes.
4. Build against the target runtime using your own game/mod references. Include verification results and remaining live-game limitations in the PR.
5. Do not commit third-party DLLs, saves, local configuration, logs, or `bin`/`obj` output. Keep the bundled mod DLL in sync with the source for a release.

Original contributions are intended to use this repository's MIT terms. Do not relicense adapted third-party material; retain its credits and applicable terms. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
