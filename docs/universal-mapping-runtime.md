# Universal mapping runtime

Step 5 makes the universal controller layer and universal profiles the
authoritative production mapping path.

The runtime flow is:

```text
UniversalControllerManager
  -> authoritative logical controller
  -> immutable UniversalControllerStateSnapshot
  -> UniversalMapperSession
  -> compiled UniversalProfile
  -> existing MapAction graph
  -> existing output services
```

The SDL diagnostic window remains observational. Diagnostic controllers use the
`diagnostic-observer` backend id and are ignored when mapper sessions are
created.

## Ownership

`UniversalControllerManager` arbitrates backend candidates before mapping:

* `steam-controller-native` owns original Steam Controller 2015 devices.
* `sdl3` owns supported modern SDL gamepads.
* duplicate backend session ids are collapsed within one refresh.
* controllers with the same model metadata but different session identities
  remain separate.

The old reader and mapper classes remain in the tree for comparison,
migration and later removal, but `BackendManager` starts the universal runtime
as the normal service path. Hotplug refreshes the universal runtime instead of
creating legacy mapper output for the same physical controller.

## Profile Activation

`UniversalMappingRuntime` runs the Step 4 migrator at startup against discovered
legacy profile sources, writes universal profiles through the universal store,
and leaves every legacy source unchanged. Migration warnings, conflicts and
failures are reported through `StartupMigrationReports`.

Universal profiles are compiled before activation. Compilation validates the
profile, translates supported bindings to the existing action graph, and keeps
unsupported or unmappable stored bindings in the source profile without
activating them for the current controller. A malformed profile or unsupported
future schema never becomes a partial mapper session.

## Value Dispatch

`UniversalMapper` dispatches only universal input ids and universal value
kinds:

* digital buttons preserve press, hold and release transitions;
* triggers stay analogue and keep separate full-pull clicks when exposed;
* sticks are dispatched as paired two-dimensional values;
* D-pad directions are recomposed into the existing D-pad action input;
* touch contacts and clicks stay independent;
* gyro and accelerometer values remain separate until passed into the existing
  gyro action context.

No controller-specific SDL enum, native Steam packet type or legacy input enum
is exposed at this mapper boundary. Hardware calibration and device-specific
configuration remain outside universal profiles.

## Transitional Editor State

The final profile editor conversion is deferred to Step 6. The Step 5 runtime
activates universal profiles, but this commit does not redesign labels, glyphs
or capability-filtered editor visibility.
