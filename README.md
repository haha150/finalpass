# Finalpass

Finalpass is being rebuilt as a completely offline password manager for
Windows 11 x64. The new desktop application is C#/.NET 10 with WinUI 3; the old
Go/Qt desktop and API remain in the repository as historical code and are not
part of the new release.

## What works in the rebuild

- One portable `.fpass` vault file with no plaintext database or temp file.
- Argon2id password derivation and XChaCha20-Poly1305 authenticated encryption
  through the official libsodium binary.
- Atomic saves, encrypted `.bak` recovery, verified manual backups, writer
  locking, and external-change detection.
- Create, open, edit, delete, save, back up, lock, and change-master-password
  workflows.
- Nested folder creation, rename, deletion with content preservation, entry
  assignment, and editable masked or plain custom fields.
- Live search, composable folder/tag/favorite filters with active filter chips,
  all six planned sort fields, and an explicit session-only option to search
  actual password values.
- Configurable CSPRNG password and offline passphrase generation. Password
  length/classes, ambiguous-character exclusion, passphrase word count,
  separator, capitalization, and optional numbers are supported.
- One-click, keyboard, and login-row context-menu username/password copying.
  Clipboard history and roaming are disabled, and unchanged copied content is
  cleared after the user-configured interval.
- Keyboard commands for save, open, new vault, lock, search, and credential
  copying, plus safe `http`/`https` website launching.
- Debounced autosave, a configurable idle lock, and immediate locking on
  Windows workstation lock or suspend.
- A locked vault can be reopened from the welcome screen using its remembered
  local path; the master password is never stored.
- Read-only opening when another Finalpass process owns the vault writer lock.
- Self-contained portable output and a current-user installer that uses
  `%LocalAppData%\Programs\Finalpass` and does not request elevation.
- Branded Windows assets in both publish forms, verified with the full WinUI
  compiler and a standard-user Windows 11 VM smoke test.

The rebuild is an engineering preview, not a security-audited 1.0 release. See
the remaining release gates in [the rebuild plan](docs/DESKTOP_REBUILD_PLAN.md).
Automatic `.fpass.bak` files can be opened directly from the app's Open dialog
if the primary vault is damaged.

## Install as a regular Windows user

Download `Finalpass-0.1.0-win-x64-setup.exe` and `SHA256SUMS.txt` from the
trusted release location, verify the checksum, and double-click the setup file.
The installer writes only to your `%LocalAppData%\Programs\Finalpass` directory
and current-user registry keys, so it does not request an administrator account
or a UAC elevation prompt. Launch Finalpass from the Start menu afterward.

Finalpass releases are intentionally not code-signed. Windows SmartScreen may
show an unknown-publisher warning; only continue after obtaining the file from
the trusted project release and verifying its SHA-256 checksum. This warning is
expected for this project's distribution model.

For a portable run, extract `Finalpass-0.1.0-win-x64-portable.zip` somewhere you
can write to and run `Finalpass.exe`. Uninstalling through **Settings > Apps >
Installed apps** removes the program but does not delete `.fpass` vaults saved
in Documents or elsewhere.

## Build on Windows 11

Prerequisites: PowerShell and the .NET 10.0.401 SDK. Dependency restoration and
publishing require internet access on the build machine; the resulting app does
not use the network.

```powershell
./eng/restore-libsodium.ps1
dotnet restore Finalpass.sln
dotnet test Finalpass.sln --configuration Release --no-restore
dotnet restore src/Finalpass.App/Finalpass.App.csproj `
  --runtime win-x64 `
  --property:PublishReadyToRun=true
dotnet publish src/Finalpass.App/Finalpass.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --no-restore `
  --output artifacts/publish/win-x64
```

Run `artifacts/publish/win-x64/Finalpass.exe` for the portable build. To produce
the no-admin installer:

```powershell
./eng/restore-inno.ps1
& artifacts/dependencies/inno/installed/ISCC.exe packaging/Finalpass.iss
```

The installer is written to `artifacts/installer`. Both dependency scripts pin
the upstream version and verify its SHA-256 digest before use.

Windows CI also creates an SPDX 2.2 SBOM, checks dependencies for known
vulnerabilities, asserts that application source does not use networking APIs,
and smoke-tests silent current-user install, update, file association, and
uninstall behavior.

## Design documents

- [Desktop rebuild plan](docs/DESKTOP_REBUILD_PLAN.md)
- [Vault format v1](docs/VAULT_FORMAT.md)
- [Threat model](docs/THREAT_MODEL.md)
- [Release checklist](docs/RELEASE_CHECKLIST.md)
- [Third-party notices](THIRD_PARTY_NOTICES.md)

## Security model in one paragraph

The master password is processed with Argon2id (default 256 MiB, three passes)
to unwrap a random vault data key. The complete payload is encrypted with
XChaCha20-Poly1305 using a fresh nonce on every save, and the file header is
authenticated as associated data. A closed vault is designed to resist offline
theft and detect tampering. Like every local password manager, Finalpass cannot
protect an unlocked vault from malware, keyloggers, an administrator, or a
compromised Windows installation. There is no recovery back door.
