# Finalpass release checklist

The source implements the planned desktop feature set. A build is not a
production release until every item below has evidence attached to the release.

## Automated Windows gate

- Run `.github/workflows/windows.yml` from a clean checkout.
- Require all 49 unit/integration tests, formatting, dependency vulnerability,
  and offline-source checks to pass.
- Retain the portable zip, current-user installer, SPDX SBOM, and
  `SHA256SUMS.txt` produced by the same workflow run.
- Confirm the installer smoke test completed both install passes (fresh install
  and update), verified the HKCU `.fpass` association, uninstalled, and retained
  the external vault sentinel.

## Clean Windows 11 standard-user pass

- Disconnect networking before starting the test.
- Install without administrator credentials or a UAC elevation prompt.
- Launch from the Start menu and by double-clicking a `.fpass` file.
- Create, save, lock, reopen, back up, and restore a vault.
- Exercise folders, tags/filter chips, every sort choice, custom fields,
  configurable passwords/passphrases, shortcuts, URL launch, and settings.
- Open the same vault in a second process and confirm read-only behavior.
- Confirm idle lock, Windows workstation lock, and suspend discard the unlocked
  vault and clear Finalpass-owned clipboard content.
- Test with Narrator, keyboard only, 100/150/200% scaling, dark/light themes,
  and Windows high contrast.
- Load at least 10,000 entries and record search/filter/sort interaction latency.
- Uninstall and confirm user-created vault and backup files remain.

## Security and publication

- Run sustained coverage-guided fuzzing against the vault parser in addition to
  the deterministic randomized regression suite.
- Have an independent cryptography/security reviewer approve
  `VAULT_FORMAT.md`, `THREAT_MODEL.md`, the libsodium interop, and atomic-save
  behavior.
- Confirm the unsigned-distribution notice and expected Windows SmartScreen
  warning are included with the release.
- Verify SHA-256 checksums on a separately downloaded copy.
- Publish release notes that state the Windows baseline, offline scope, vault
  backup expectations, and absence of password recovery.
