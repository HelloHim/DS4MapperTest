# Universal Profiles

Step 4 introduces universal profile files beside the existing legacy
controller-specific profile system. The current mapper and editor still use
legacy profiles until the later runtime conversion steps.

## File Location

Universal profiles are stored as flat files under the application profile root:

- development builds: `%APPDATA%\DS4TestUniversalDev\Profiles`
- release builds: `%APPDATA%\DS4Test\Profiles`

Universal files use the extension `.universal-profile.json`. They are not
partitioned into controller-family subdirectories. Legacy controller-specific
directories remain in place and are not modified by migration.

## Schema Version

The initial schema version is `1` and is stored in `schemaVersion`. A missing
version, malformed JSON, an older unsupported version, or a newer future
version fails with a clear load error. A newer profile is never rewritten by an
older application.

Unknown root fields are preserved as extension data when a supported profile is
loaded and saved. Unknown universal input tokens are rejected for schema `1`
because rewriting them could silently change or drop user bindings.

## Identity And Names

`profileId` is the stable profile identity and is independent of both
`displayName` and the physical filename. The store writes files as:

```text
{profileId}.universal-profile.json
```

Renaming `displayName` does not change `profileId` or the filename. The store
rejects rooted paths, traversal attempts and reserved Windows device names when
resolving user-supplied filenames.

## Bindings

Bindings use stable kebab-case universal input tokens derived from
`UniversalInputId`, for example:

```json
{
  "actionSet": 0,
  "actionLayer": 0,
  "input": "face-button-south",
  "valueKind": "DigitalButton",
  "action": 1
}
```

Bindings are stored without consulting controller capabilities. Unsupported
inputs remain present during load and save. Capabilities and editor visibility
belong to later runtime/UI steps and must not prune profile data.

Bindings do not store SDL enums, native handles, HID paths, VID/PID values,
controller labels, glyph keys or calibration data.

## Actions

Actions are stored with stable `id` and `type` fields plus a `payload` object.
During Step 4 the payload preserves the existing legacy action JSON so the
later mapper conversion can consume the same behaviour without changing the
current runtime. Action payloads may include output settings, timing, toggles,
double-press data, curves, sensitivities and other mapping behaviour.

Controller-specific hardware configuration remains outside the universal
profile. The migrator copies controller-independent profile settings such as
output gamepad settings and game sensitivity calibration, but it does not copy
device identity, controller folders, backend handles, HID paths or reader
configuration.

## Storage

Saves are atomic:

1. serialize and validate in memory;
2. write to a temporary file in the universal profile root;
3. flush the file;
4. replace an existing profile with `File.Replace`, or move the temp file into
   place for new files;
5. clean only the temporary file created by that save operation.

Failed serialization or validation leaves the previous profile file unchanged.

## Migration

`LegacyProfileMigrator` supports preview and write phases. Preview performs no
profile writes. Write creates a universal profile and records a manifest entry
in `_universal-profile-migration-manifest.json`.

Manifest entries contain only machine-independent provenance:

- legacy profile family;
- relative source identity;
- source content hash;
- resulting universal profile id;
- migration schema version;
- outcome and warnings.

They do not contain absolute paths, user names, serial numbers, HID paths or
transient controller IDs.

Migration is idempotent for unchanged sources. If a legacy source changes after
migration, the migrator reports a conflict and does not overwrite the existing
universal profile.

Unknown controls or behaviour-losing conversion issues fail that profile and do
not write a partial universal file. One failed source does not prevent reports
for the rest of a batch.

## Legacy Input Mapping Policy

Face buttons are mapped by physical position, not printed label:

- PlayStation `Cross`, Xbox-style `A` and Nintendo `B` become
  `face-button-south`;
- PlayStation `Circle`, Xbox-style `B` and Nintendo `A` become
  `face-button-east`;
- PlayStation `Square`, Xbox-style `X` and Nintendo `Y` become
  `face-button-west`;
- PlayStation `Triangle`, Xbox-style `Y` and Nintendo `X` become
  `face-button-north`.

PlayStation single-touchpad profiles migrate touchpad bindings to the universal
primary touch surface. They are not split into left and right physical touch
surfaces. Original Steam Controller profiles preserve independent left and
right touch surfaces and clicks. Unknown special controls are reported rather
than guessed.
