# Finalpass Vault Format (`.fpass`)

Status: version 1 implementation specification, 2026-09-09.

All multi-byte integers are unsigned and little-endian. All offsets below are
decimal bytes from the beginning of the file. Version 1 is intentionally a
single encrypted payload with no compression or attachments.

## Limits and identifiers

| Name | Value |
| --- | ---: |
| Magic | `46 50 41 53 53 44 42 00` (`FPASSDB\0`) |
| File format version | `1` |
| Header length | `156` bytes |
| Flags | `0` |
| KDF algorithm | `1` (Argon2id v1.3) |
| Default KDF operations | `3` |
| Default KDF memory | `268435456` bytes (256 MiB) |
| Accepted KDF operations | `1..10` |
| Accepted KDF memory | `67108864..1073741824` bytes |
| Maximum plaintext payload | `134217728` bytes (128 MiB) |
| Salt | `16` bytes |
| XChaCha20-Poly1305 nonce | `24` bytes |
| Vault data key | `32` bytes |
| Poly1305 authentication tag | `16` bytes |

The application stores numeric KDF parameters, not libsodium symbolic constants.
An implementation may offer a stronger setting within the accepted bounds. It
must never silently reduce an existing vault's parameters.

## Binary header

| Offset | Size | Field |
| ---: | ---: | --- |
| 0 | 8 | Magic |
| 8 | 2 | File format version |
| 10 | 2 | Header length |
| 12 | 4 | Flags |
| 16 | 4 | KDF algorithm |
| 20 | 8 | Argon2 operations limit |
| 28 | 8 | Argon2 memory limit in bytes |
| 36 | 16 | KDF salt |
| 52 | 24 | Wrapped-key nonce |
| 76 | 48 | Wrapped vault data key (32-byte key + 16-byte tag) |
| 124 | 24 | Payload nonce |
| 148 | 8 | Payload ciphertext length, including 16-byte tag |
| 156 | variable | Payload ciphertext |

Unknown versions, nonzero flags, unknown KDF identifiers, a header length other
than 156, out-of-policy KDF values, a payload length below 16, a plaintext length
above the limit, arithmetic overflow, truncation, or bytes after the declared
payload are fatal format errors. These checks happen before Argon2 is invoked.

## Password processing and key wrapping

Master-password Unicode is encoded as UTF-8 exactly as entered; it is not
normalized or case-folded. New-vault UI requires confirmation so visually
similar but byte-distinct input is caught at creation. The encoded value must be
between 1 and 1024 bytes.

Derive 32 bytes with libsodium `crypto_pwhash` using:

- algorithm `crypto_pwhash_ALG_ARGON2ID13`;
- the header's 16-byte salt;
- the header's numeric operations limit; and
- the header's numeric memory limit.

This result is the key-encryption key (KEK). Generate a vault data key with
`randombytes_buf` when creating a vault. Wrap its 32 bytes using
`crypto_aead_xchacha20poly1305_ietf_encrypt` with the KEK, the header's
wrapped-key nonce, and bytes `[0, 76)` as associated data. The result must be 48
bytes and is stored at offset 76.

After unwrapping succeeds, wipe the KEK and encoded master password. The
application does not retain either for ordinary saves.

## Payload encryption

Serialize the logical payload to UTF-8 JSON without a BOM. Generate a fresh
24-byte payload nonce for every save. Encrypt with XChaCha20-Poly1305 using the
vault data key and the complete 156-byte header as associated data. Because the
header includes the wrapped key, nonce, and declared ciphertext length, the
payload tag binds every byte of the envelope.

Ordinary saves retain bytes `[0, 124)` (KDF parameters and wrapped key), generate
a new payload nonce, update the ciphertext length, and encrypt the snapshot. A
master-password or KDF upgrade generates a new salt and wrapped-key nonce,
rewraps the same data key, and re-encrypts the payload because its associated
header changed.

Nonce generation is always random through `randombytes_buf`; callers cannot
provide nonces. A data key is never used with the same payload nonce twice.

## Logical JSON payload

Property names use camel case. Dates are UTC RFC 3339 strings with seven
fractional digits and a `Z` suffix. IDs are lowercase UUID strings. Unknown JSON
properties are ignored for forward compatibility, but missing required
properties, duplicate IDs, broken folder references, excessive field sizes, and
unsupported `schemaVersion` values are rejected.

```json
{
  "schemaVersion": 1,
  "vaultId": "0c2f47e1-5787-45bc-88b6-38d41ebf660f",
  "revision": 1,
  "name": "Personal",
  "createdUtc": "2026-09-09T12:00:00.0000000Z",
  "updatedUtc": "2026-09-09T12:00:00.0000000Z",
  "folders": [
    {
      "id": "d617af4b-711c-4c9d-92c3-7f48a3c5d9ae",
      "parentId": null,
      "name": "General",
      "createdUtc": "2026-09-09T12:00:00.0000000Z",
      "updatedUtc": "2026-09-09T12:00:00.0000000Z"
    }
  ],
  "entries": [
    {
      "id": "a96b650d-3047-451c-8c5f-8025fefde12c",
      "folderId": "d617af4b-711c-4c9d-92c3-7f48a3c5d9ae",
      "title": "Example",
      "username": "person@example.test",
      "password": "generated secret",
      "url": "https://example.test/",
      "notes": "",
      "tags": ["personal"],
      "favorite": false,
      "customFields": [
        { "name": "Account number", "value": "1234", "secret": false }
      ],
      "createdUtc": "2026-09-09T12:00:00.0000000Z",
      "updatedUtc": "2026-09-09T12:00:00.0000000Z"
    }
  ]
}
```

JSON strings shown as plaintext above exist only in process memory. Password and
secret custom-field values are never used by default search or list rendering.

## Open sequence

1. Open source read-only and obtain its stable identity/metadata.
2. Read exactly 156 bytes and perform all structural/resource checks.
3. Read exactly the declared payload and verify EOF.
4. Derive the KEK and authenticate/decrypt the wrapped data key.
5. Authenticate/decrypt the payload into a bounded buffer.
6. Parse and validate the complete logical model into a temporary snapshot.
7. Only after all steps succeed, publish the unlocked snapshot to the app.
8. Wipe KEK, password bytes, decrypted serialization buffers, and failed partial
   results as soon as they are no longer needed.

Any cryptographic failure is reported to the user as "The master password is
incorrect or the vault is damaged." Detailed errors must not expose key material
or plaintext.

## Save and recovery sequence

1. Increment the in-memory revision and serialize an immutable snapshot.
2. Encrypt it with a fresh payload nonce into a unique ciphertext temp file in
   the destination directory.
3. Flush file contents to stable storage.
4. Reopen, authenticate, decrypt, and validate the candidate using the retained
   data key without publishing the duplicate model.
5. Verify the original file fingerprint still matches the opened generation.
6. Atomically replace the original through the Windows replace-file API while
   retaining one encrypted `.bak` generation.
7. Update the in-memory fingerprint and clear dirty state.

If any step fails, the original remains the active vault and the application
reports unsaved changes. Recovery logic only promotes a temp or backup after it
fully authenticates. Non-atomic file systems must be detected or treated as a
reduced-guarantee destination rather than silently claiming atomic durability.

## Compatibility rules

- Readers must reject unknown file format versions.
- A future reader may migrate an authenticated older logical `schemaVersion` in
  memory and save only after explicit success.
- Writers always emit their single current file and schema versions.
- Version 1 has no extension records. Nonzero flags are reserved and rejected.
- Existing Go `.db` files are unrelated to this format and intentionally are not
  imported.
