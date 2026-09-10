# Finalpass Desktop Rebuild Plan

Status: approved on 2026-09-09; feature implementation is complete and release
validation is in progress.

## 1. Product scope

Rebuild Finalpass as a local-first, single-user password manager for Windows. The
old Go/Qt desktop application is reference material for useful behavior only.
The `api` application, accounts, remote save/sync, and 2FA for the old online
account are outside the new product.

The first release will provide:

- Create, open, close, lock, save, back up, and restore a vault.
- Folders, tags, favorites, and credential entries.
- Entry fields for title, username, password, URL, notes, timestamps, and custom
  fields.
- Fast search across non-secret fields, folder/tag filtering, and sorting by
  title, username, URL, folder, created time, or updated time.
- An explicit, session-only option to include password values in a search. They
  will not be searched by default.
- One-click and keyboard-driven username/password copying.
- A cryptographically secure password/passphrase generator.
- A self-contained Windows x64 distribution that installs and runs without local
  administrator rights or separately installed runtimes.

Not in the first release: a server, cloud synchronization, browser integration,
mobile apps, shared vaults, attachments, or account-based password recovery.

## 2. Recommended technology

- **Language/runtime:** C# 14 on .NET 10 LTS.
- **Desktop UI:** WinUI 3 with the Windows App SDK, using Fluent controls and an
  MVVM separation between UI and application logic.
- **Cryptography:** pinned official libsodium Windows binaries, called through a
  small, reviewed interop layer. No home-grown cipher or KDF implementation.
- **Persistence:** a documented, versioned `.fpass` encrypted document. The
  decrypted model exists only in process memory while the vault is unlocked.
- **Testing:** unit and property tests for the cross-platform core, plus Windows
  integration/UI tests and release builds in Windows CI.

WinUI 3 is Microsoft's recommended UI framework for a new native Windows app.
A self-contained build includes the .NET and Windows App SDK runtimes. The
primary artifact will be an offline, per-user installer targeting
`%LocalAppData%\Programs\Finalpass`; it must not request elevation or download
dependencies. A portable zip will also be produced when practical, but it is
not required for the no-admin guarantee.

Planned support baseline: Windows 11 x64. WinUI can run on older Windows 10
versions, but current .NET support for Windows 10 is limited to supported
LTSC/Enterprise editions. ARM64 can be added after the x64 release is stable.

## 3. Why not use SQLite for the new vault

The data set for a personal vault is small enough to decrypt once and search in
memory. A single encrypted document has fewer failure modes than SQLite plus an
encryption extension, journaling/WAL files, native database bindings, backup
coordination, and schema migrations.

The `.fpass` file is still the vault database from the user's perspective: it is
one portable file that can be copied, backed up, and opened on another machine.
It contains no readable titles, usernames, URLs, notes, or other metadata.

## 4. Vault cryptographic design

### 4.1 Primitives

- Generate all salts, nonces, data keys, and passwords with libsodium's system
  CSPRNG.
- Derive a 256-bit key-encryption key from the master password with Argon2id
  v1.3 and a fresh 128-bit salt.
- Start with numeric KDF parameters equivalent to a moderate desktop setting
  (target roughly 0.5-1 second on supported hardware, with a 256 MiB memory goal).
  Store the exact algorithm and numeric parameters in every file so future
  versions can upgrade them.
- Encrypt a random 256-bit vault data key with XChaCha20-Poly1305.
- Encrypt the serialized vault payload with that data key and a new random nonce
  on every save.
- Authenticate the format version and all public header fields as associated
  data, so parameters cannot be silently changed or downgraded.

ChaCha20-Poly1305 was not inherently the problem in the old application.
XChaCha20-Poly1305 retains it while providing a much larger nonce, making random
nonce generation safer. Argon2id supplies the expensive, memory-hard password
derivation that the old SHA-256 step lacked.

### 4.2 File envelope

The binary envelope will contain:

1. Fixed magic bytes and a file-format version.
2. Bounded KDF algorithm, salt, memory, and operation fields.
3. A nonce and authenticated wrapped vault data key.
4. A payload nonce, ciphertext length, and authenticated payload ciphertext.

The parser will reject unknown mandatory features, unreasonable allocation/KDF
values, integer overflows, truncation, trailing data, and over-sized payloads
before performing expensive work. Exact bytes and byte order will be frozen in
a separate format specification before persistence code is merged.

Envelope encryption lets a master-password or KDF-parameter change rewrap the
random data key without reusing the password directly as a content key. There
is deliberately no password hint, recovery secret, or back door. A forgotten
master password means the vault cannot be recovered.

### 4.3 Secret handling

- Never write a plaintext vault, SQLite temp file, password, or generated secret
  to disk, logs, settings, crash reports, or telemetry.
- Keep secrets in mutable buffers where practical and wipe temporary key/password
  buffers immediately after use. Managed UI strings cannot provide a perfect
  memory-erasure guarantee, so their lifetime will be minimized.
- Lock the vault after a configurable idle period, workstation lock, suspend,
  or an explicit lock command; discard the decrypted model and wipe key buffers.
- Do not send network requests. The first release has no networking capability.
- Treat malformed file headers as hostile and cap their KDF resource requests.

The threat model protects a closed vault against offline file theft and detects
file tampering. It cannot protect an unlocked vault from malware, keyloggers,
screen capture, an administrator inspecting the process, or a fully compromised
Windows installation.

## 5. Reliable save, backup, and recovery

- Serialize a complete immutable snapshot in memory.
- Encrypt it to a uniquely named temporary ciphertext file in the same directory.
- Flush the file, validate that it can be opened, then atomically replace the
  destination.
- Preserve one last-known-good encrypted `.bak` during replacement. Never replace
  a valid vault if serialization, encryption, validation, or flushing fails.
- Maintain a revision and source-file fingerprint. Refuse a blind overwrite if
  another process or sync tool changed the file after it was opened.
- Hold an advisory lock while editing and offer read-only open if the lock is
  already held.
- Provide **Back Up As** as a byte-for-byte encrypted copy and **Restore/Open** by
  selecting any valid `.fpass` file. Reopen and authenticate a backup before
  reporting success.
- Autosave successful mutations after a short debounce; retain Ctrl+S and an
  unsaved-state indicator. Lock/exit must wait for or report a failed save.

The temporary save and `.bak` are ciphertext, not plaintext. Vaults on OneDrive,
Dropbox, or removable media can be copied, but concurrent multi-device merging
is not part of the first release.

## 6. Desktop experience

Use a three-area layout:

```text
Folders / tags     Search + sortable entry list       Selected entry
All items          [ Search credentials... ]           Title
Favorites          Title  Username  URL  Updated        Username  [Copy]
Folders...                                                Password  [Reveal] [Copy]
Tags...                                                   URL       [Open]
                                                          Notes / custom fields
```

Important interaction details:

- Passwords are masked in lists and detail views. Reveal is explicit and
  temporary.
- Copy buttons are visible next to username and password; shortcuts are exposed
  in menus/tooltips and work from the entry list.
- Password clipboard data opts out of Windows clipboard history and cloud
  clipboard, then clears after a configurable default of 30 seconds only if the
  clipboard still contains the value Finalpass placed there.
- Search is case-insensitive and updates as the user types. It covers title,
  username, URL, notes, tags, folder, and custom-field names/values. Password
  values require the explicit session-only toggle.
- Column-header sorting supports ascending/descending order and is stable.
- Filters compose (for example: folder + tag + favorite), show active chips, and
  are easy to clear.
- Editing uses validation without arbitrary master-password composition rules.
  The UI encourages a long master passphrase and clearly explains that it cannot
  be recovered.
- The generator supports length, character classes, ambiguous-character
  exclusion, and multi-word passphrases. Selection and shuffling are unbiased and
  use a CSPRNG.
- Keyboard navigation, focus visibility, screen-reader names, scaling, dark/light
  themes, and high-contrast behavior are release requirements.

## 7. Proposed repository structure

```text
Finalpass.sln
src/
  Finalpass.App/             WinUI views, view models, Windows lifecycle
  Finalpass.Core/            vault model, commands, search/sort/filter
  Finalpass.Cryptography/    libsodium interop and .fpass format
  Finalpass.Infrastructure/  atomic files, locking, clipboard, settings
tests/
  Finalpass.Core.Tests/
  Finalpass.Cryptography.Tests/
  Finalpass.Infrastructure.Tests/
packaging/
  Finalpass.iss              offline, current-user installer definition
docs/
  DESKTOP_REBUILD_PLAN.md
  VAULT_FORMAT.md
  THREAT_MODEL.md
```

The old Go desktop and API code will remain untouched for repository history and
will be excluded from the new solution and release artifacts. No old `.db`
migration code or legacy cryptography will be carried into the new application.

## 8. Implementation phases and gates

### Phase 1: foundation and specifications

- Create the solution/projects, architecture boundaries, formatting/analyzers,
  dependency pinning, and Windows CI.
- Write `THREAT_MODEL.md` and freeze vault-format v1 in `VAULT_FORMAT.md`.
- Add a minimal libsodium interop spike and verify official Argon2id and
  XChaCha20-Poly1305 vectors.

Gate: format and threat-model review completed before UI or real vault data.

### Phase 2: encrypted vault core

- Implement create/open/save/lock/change-master-password.
- Implement model validation, atomic replacement, encrypted backup, file locking,
  external-change detection, and settings containing paths/preferences only.
- Add corruption, interruption, concurrency, and resource-limit tests.

Gate: no plaintext disk artifacts; every header/payload bit flip fails safely;
failed saves leave the prior vault openable.

### Phase 3: modern desktop UI

- Build onboarding, create/open/unlock, three-area shell, CRUD, generator,
  search/sort/filter, reveal/copy, backup/restore, and lock flows.
- Add keyboard, accessibility, theme, empty/error/loading states, and safe prompts.

Gate: all primary workflows are usable without a mouse and no secret appears in
logs, window titles, notifications, or default search results.

### Phase 4: hardening and release

- Run parser fuzzing/property tests, save-failure injection, dependency/license
  review, static analysis, and a focused cryptographic design review.
- Test install, execution, update, uninstall, and file association behavior on
  clean Windows 11 VMs without administrator rights.
- Produce an offline x64 self-contained current-user installer and, when
  practical, a portable zip, plus checksums, an SBOM, an explicit unsigned-
  distribution notice, and release notes.

Gate: a clean non-admin Windows user can install and run the app, create and
reopen a vault, back it up, restore it, and remove the app without losing vault
files. Installation and first launch must work without an internet connection.

## 9. Required automated tests

- Known-answer vectors and independent round trips for Argon2id and XChaCha20-
  Poly1305.
- Wrong password, wrong salt/nonce, modified header, modified ciphertext/tag,
  truncation, trailing bytes, unknown version, enormous lengths, and excessive KDF
  parameters.
- Unique salt/data key/nonces across new vaults and saves.
- Atomic-save failures injected before write, during write, before flush, before
  replace, and after backup creation.
- Search/filter/sort correctness, including Unicode and thousands of entries.
- Clipboard timeout and "clear only if unchanged" behavior.
- Idle/workstation-lock/suspend locking behavior.
- Offline per-user install, launch, update, and uninstall on a clean standard-user
  Windows 11 VM, plus portable launch if a zip artifact is shipped.

## 10. Acceptance criteria for version 1.0

- One self-contained encrypted file is sufficient to move or restore a vault.
- No master password, entry secret, plaintext payload, or plaintext database is
  written to persistent storage or emitted through logs/telemetry.
- Opening with the wrong password or any authenticated-file modification fails
  without changing the source file.
- Power loss or a failed save cannot destroy the last valid vault.
- Username and password copy take one action; password copies avoid clipboard
  history/cloud sync and expire by default.
- Search, filter, and sort remain responsive for at least 10,000 entries.
- The x64 installer installs and runs for a standard Windows user with no
  separately installed runtime, elevation prompt, or installation-time download.
- After installation, the application performs no network requests and contains
  no remote account, synchronization, telemetry, or update-checking feature.

## 11. Confirmed product decisions

Confirmed on 2026-09-09:

1. Windows 11 x64 is the main supported target.
2. Existing Finalpass `.db` migration is not required.
3. "Search passwords" means search credential records; actual password values
   are excluded unless the user enables the session-only option.
4. The installed application is fully offline and has no remote sync or account
   system.
5. The deployment form may be an installer or portable build; the requirement is
   that installation and execution need no administrator or special rights.

These decisions approve the plan for implementation.

## 12. Implementation checkpoint

Completed in the current rebuild branch:

- Solution boundaries, pinned dependencies, analyzers, Windows CI, verified
  libsodium restoration, portable packaging, and a no-elevation Inno Setup
  definition.
- Frozen vault format and threat model.
- Argon2id/XChaCha20-Poly1305 envelope encryption, data-key wrapping,
  master-password changes, strict hostile-file parsing, and key-buffer wiping.
- Atomic ciphertext saves with `.bak` recovery, external-change detection,
  per-vault writer locks, and verified encrypted `Back Up As` copies.
- A functional WinUI shell for vault creation/opening/locking, login CRUD,
  debounced saves, idle locking, search, favorite filtering, sorting, password
  generation, safe clipboard copies, master-password changes, nested folder
  management, entry folder assignment, and masked/plain custom-field editing.
- Configurable password and offline multi-word passphrase generation, tag
  filtering with removable active chips, all planned sort choices, keyboard
  commands, safe URL launch, and per-user idle/clipboard settings.
- Read-only vault snapshots when another writer owns the lock, plus native
  workstation-lock and power-suspend notifications that discard decrypted state.
- A 10,000-entry Unicode query regression test and list replacement that avoids
  issuing 10,000 individual UI collection notifications.
- Atomic-save failure injection before/during write, before flush/replace, and
  after atomic replace/backup, including post-commit recovery; randomized hostile
  parser cases; independent salt/key-wrap/nonce assertions; and settings tests.
- CI gates for formatting, vulnerable dependencies, offline-only application
  source, SPDX 2.2 SBOM generation, and silent current-user install/update/file-
  association/uninstall smoke testing.
- Branded publish assets plus a successful Windows 11 standard-user VM pass of
  the full WinUI build, encrypted create/save/lock/reopen flow, folder
  create/rename/delete behavior, custom fields, private-value search opt-in,
  portable launch, and current-user installer launch.
- Cross-platform tests for the core, cryptographic vectors/file mutation, and
  persistence behavior.

Still required before calling the application a production 1.0: execute the
updated Windows CI and repeat the clean Windows 11 standard-user UI smoke pass;
add deeper Windows UI automation for clipboard timing and simulated session/
power events; complete hands-on Narrator, keyboard-only, scaling, high-contrast,
and 10,000-row UI profiling; run sustained coverage-guided fuzzing; and obtain
an independent review of the cryptographic design and implementation. The
project intentionally distributes unsigned artifacts, so Windows SmartScreen's
unknown-publisher warning is expected and SHA-256 verification remains part of
the release process. Independent review requires external people and cannot be
completed by source changes alone.

## References

- Microsoft recommends WinUI 3 for new native Windows apps:
  https://learn.microsoft.com/windows/apps/
- Microsoft Windows app packaging and self-contained deployment options:
  https://learn.microsoft.com/windows/apps/package-and-deploy/
- .NET release and support policy:
  https://learn.microsoft.com/dotnet/core/releases-and-support
- Argon2 recommendations (RFC 9106):
  https://www.rfc-editor.org/info/rfc9106/
- Libsodium password hashing / Argon2id:
  https://doc.libsodium.org/password_hashing/default_phf
- Libsodium XChaCha20-Poly1305:
  https://doc.libsodium.org/secret-key_cryptography/aead/chacha20-poly1305/xchacha20-poly1305_construction
- Windows clipboard history/cloud opt-out formats:
  https://learn.microsoft.com/windows/win32/dataxchg/clipboard-formats
