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

## Loop pacing and timer resolution

The mapping loop runs at 125 Hz and is paced by `PrecisionLoopTimer`, which
uses a Windows high-resolution waitable timer rather than `Thread.Sleep`.

This matters more than it looks. `Thread.Sleep` rounds up to the process
timer resolution, and since Windows 10 2004 that resolution is per process,
defaulting to 15.625 ms. The app asks for 1 ms with `timeBeginPeriod` at
startup, but Windows ignores that request for a process it has placed under
power throttling, which it does to processes that have been in the
background for a while. The loop then cannot hit an 8 ms period at all, and
gyro output becomes stuttery and laggy with nothing in the app having
changed. Measured on the old pacing, in a process whose timer resolution
request is not being honoured: 100 Hz average with individual periods
spiking to 23.5 ms, against a steady 125 Hz on the waitable timer.

Two other pieces guard the same property, and all three should stay:

- `App.RunStartup` calls `Util.DisableProcessPowerThrottling`, so the
  `timeBeginPeriod(1)` request keeps being honoured in the background.
- The loop measures its own achieved rate over a five second window and logs
  a warning if it falls below 105 Hz, then an entry when it recovers. If a
  user reports stuttering gyro, that pair of log lines is the first thing to
  look for.
