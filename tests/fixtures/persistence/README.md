# Legacy persistence fixtures

These small binary fixtures freeze the current C++ `GameStateManager` persistence contract for
cross-language compatibility tests. They are test inputs, not example player saves.

| Path | Provenance | Coverage |
| --- | --- | --- |
| `cpp-v2/save_1.vsav` | Authentic output from the public C++ `captureSnapshot` + `saveToSlot` path | Every `GameSnapshot` scalar field, all variable types, UTF-8 and empty strings, two distinct sprites, and BGM/SFX/voice state |
| `cpp-v2/persistent.vsav` | Authentic output from the public C++ `setPersistent` + `savePersistent` path | String, number, and boolean values, including UTF-8 and an empty string |
| `synthetic-v1-host-native/save_1.vsav` | **Synthetic** output constructed from the current legacy-reader contract; it is not an authentic historical user save | All three v1 variable tags and the complete v1 sprite record |

The committed v2 save was generated once by the C++ oracle on little-endian arm64 macOS. Its
timestamp is the one wall-clock value assigned by `captureSnapshot`; tests require a populated
timestamp but deliberately do not require a particular instant. CBOR byte ordering is not part of
the compatibility contract.

`loadFromSlot` captures a loaded snapshot into new history and therefore assigns a fresh in-memory
`id` and timestamp. Oracle verification checks the stored timestamp through slot metadata and all
remaining values through the loaded snapshot; the authentic file's stored `id` is `0`, from the
single first capture.

## Container schemas

VSAV v2 is the four-byte `VSAV` magic, a two-byte little-endian version (`2`), and a CBOR snapshot
object. VPER v2 uses the same header shape with `VPER` magic and a CBOR object containing tagged
persistent values. The generator delegates both v2 payloads and headers to `GameStateManager` so
the files exercise the production writer rather than a duplicate serializer.

The synthetic v1 file has the normal `VSAV` header and little-endian version field (`1`), followed
by the host-native payload consumed by `legacySnapshot`:

```text
uint64 id
uint64 scriptLine
string label, background, speaker, text
uint32 variableCount
  repeated: string name, uint8 tag, tag-specific value
uint32 spriteCount
  repeated: string id, string image, float x/y/alpha/scaleX/scaleY
```

Each string is a host-native `uint32` byte length followed by unmodified bytes. Variable tags are
`0` (string), `1` (double), and `2` (one-byte boolean). Numeric v1 payload fields are host-native,
which is why the directory name explicitly says so. The committed fixture targets Velora's
currently supported little-endian x64/arm64 hosts and must not be described as a recovered save.

## Regeneration and verification

From the repository root:

```sh
cmake --preset debug
cmake --build --preset debug --target VeloraLegacyFixtureGenerator
out/build/debug/bin/VeloraLegacyFixtureGenerator \
  --generate tests/fixtures/persistence
ctest --test-dir out/build/debug -R VeloraLegacyPersistenceFixtures --output-on-failure
```

On a multi-configuration generator, add `--config Debug` to the build and use the configuration
subdirectory containing the executable. `--generate` always requires an explicit output directory,
writes only the three paths listed above, and immediately reads them back through the C++ oracle.
CTest uses `--verify`, which is read-only and fails when any expected semantic value changes.
