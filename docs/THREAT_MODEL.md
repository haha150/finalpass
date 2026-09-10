# Finalpass Threat Model

Status: version 1 design baseline, 2026-09-09.

## Security goals

Finalpass protects the confidentiality and integrity of a closed `.fpass` vault
when an attacker obtains the vault file, its encrypted backups, or ordinary
application settings. It also aims to avoid accidental disclosure through temp
files, logs, clipboard history, cloud clipboard, crash messages, and normal UI
behavior.

The application must:

- Require the master password to decrypt a vault on a new machine.
- make offline password guessing expensive with a memory-hard KDF;
- authenticate the complete file so modification, truncation, extension, and
  parameter tampering fail closed;
- keep plaintext vault contents off persistent storage;
- leave at least one valid encrypted generation after an interrupted save on a
  supported local Windows file system;
- perform no application-initiated network communication;
- install and run as a standard Windows 11 user; and
- avoid exposing copied passwords to Windows clipboard history or cloud sync.

## Protected assets

- Master passwords and derived key-encryption keys.
- Vault data keys.
- Passwords, usernames, URLs, notes, custom fields, tags, folder names, and
  timestamps.
- The relationship between entries and all vault metadata.

File paths, application preferences, binary sizes, modification times, and the
fact that Finalpass is installed are not secret.

## Attacker capabilities considered

- Copies a closed vault or backup and performs unlimited offline analysis.
- Modifies, truncates, appends to, replaces, or rolls back a vault file.
- Supplies a malicious `.fpass` file containing hostile lengths or KDF settings.
- Interrupts or terminates the application during a save.
- Starts a second Finalpass process against the same vault.
- Reads Windows clipboard history or cloud clipboard data after a copy action.
- Reads ordinary application logs and settings files.

## Explicit limitations

Finalpass cannot protect an unlocked vault from malware executing as the same
user, a keylogger, malicious accessibility software, screen capture, process
inspection, an administrator, kernel compromise, or physical attacks against a
running machine. It cannot guarantee that managed strings have been erased from
all runtime/OS memory, page files, hibernation files, or crash dumps.

Clipboard clearing is best effort because another process can read clipboard
contents immediately after the user copies them. Excluding a password from
history and cloud sync reduces persistence but does not make the clipboard a
private channel.

Finalpass v1 detects conflicting writers but does not merge concurrent edits.
It detects corruption and tampering but does not distinguish those cases from a
wrong master password. It does not prevent rollback to an older, otherwise valid
vault copy when an attacker can replace both the vault and local settings.

## Trust boundaries

```text
Master password -> Argon2id -> key-encryption key
                                   |
Untrusted .fpass header ---------- AEAD unwrap -> vault data key
Untrusted .fpass payload ----------------------- AEAD decrypt -> in-memory vault
                                                              |
                                                     WinUI / clipboard boundary
```

The file parser and native libsodium interop are high-risk boundaries. The WinUI
layer must not parse vault files or invoke cryptographic primitives directly.
Only the persistence layer can write vault ciphertext. Settings may contain a
recent vault path and non-secret preferences, never secrets or key material.

## Required mitigations

### Offline file theft

- Argon2id v1.3 with a unique 128-bit salt and stored numeric cost parameters.
- A random 256-bit data key, wrapped by the derived key-encryption key.
- XChaCha20-Poly1305 for both key wrapping and payload encryption.
- Strong master-passphrase guidance and no recovery bypass.

### Malicious or damaged files

- Validate magic, versions, flags, lengths, integer arithmetic, maximum payload,
  and KDF limits before allocating or deriving.
- Authenticate all header bytes as associated data.
- Return a generic open failure without partially loading entries.
- Fuzz the parser and preserve the source file on every failure.

### Interrupted or conflicting writes

- Write and flush ciphertext in the destination directory.
- Validate the candidate before replacement.
- Use Windows atomic replacement with a last-known-good encrypted backup.
- Track the opened-file fingerprint and revision; never blindly overwrite a
  changed source.
- Use a per-vault advisory lock and support read-only open when appropriate.

### Runtime disclosure

- Keep the master password and derived wrapping key only for unlock or password
  change, then wipe their mutable buffers.
- Retain only the data key and decrypted model while unlocked; wipe mutable key
  buffers and release the model on lock.
- Automatically lock on configured inactivity, workstation lock, and suspend.
- Never include entry contents or master-password values in logs, exceptions,
  window titles, notifications, analytics, or settings.
- Disable process crash dumps where practical and ship no telemetry SDK.

### Clipboard disclosure

- Set `ClipboardContentOptions.IsAllowedInHistory=false` and
  `IsRoamable=false` for copied secrets.
- Clear after 30 seconds by default, but only if Finalpass still owns the same
  clipboard content so newer user data is not erased.
- Apply the same policy to secret custom fields; username clearing is optional.

### Supply chain and deployment

- Pin .NET, Windows App SDK, test, MVVM, and libsodium versions.
- Verify the SHA-256 digest and release signature of downloaded libsodium native
  artifacts before packaging.
- Generate checksums and an SBOM for releases.
- Use a Windows CI runner for the release build and standard-user VM smoke test.
- The installed application contains no HTTP client, updater, telemetry, remote
  account, or synchronization component.

## Release security gate

Version 1 cannot ship until cryptographic test vectors, file mutation tests,
resource-limit tests, atomic-save failure injection, clipboard tests, dependency
review, and a focused review of `VAULT_FORMAT.md` and the implementation have all
passed.
