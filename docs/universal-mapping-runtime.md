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

### Poll rate

The loop starts at 125 Hz and then follows
`UniversalMappingRuntime.ResolvePollRateHz`, re-read once per health window.
The answer is the fastest rate any connected controller actually reports,
multiplied by `PollRateOversampleFactor`, clamped to 125-1000 Hz, and then
clamped again by the user's optional ceiling.

Where the rate comes from, in order of preference:

1. `IUniversalController.ReportRateHz`, measured by `ControllerReportRateMeter`
   from `SDL_EVENT_GAMEPAD_UPDATE_COMPLETE`. SDL raises that once per input
   report for every controller, so this works on a pad with no motion sensor
   and on hardware SDL has no specific driver for.
2. `ControllerCapabilities.MotionSampleRateHz`, SDL's declared sensor rate.
   A fallback for a backend that cannot count reports; it answers only for
   devices with a gyro SDL recognises.
3. The 125 Hz floor, which is what the loop did before it adapted at all.

The oversample factor exists because polling at exactly the device rate is
not the same as keeping up with it: the two clocks drift, so some passes see
two reports and some see none. Twice the device rate removes the beat. It
does not extract data that is not there - measured on an Xbox pad, polling
28,860 times a second still yielded only 112 distinct samples a second.

The ceiling is `AppSettingsStore.PollRateCapHz`, applied only when
`PollRateOverrideEnabled` is set, and surfaced in the Polling Rate
panel in the window header. It is deliberately global rather than per
profile: there is one mapping loop for the whole app, so two controllers on
two profiles could not be given different rates. Default is 1000 Hz, which
is the absolute maximum, so out of the box the ceiling never decides the
rate.

Costs, measured on this hardware: a full controller read is about 14
microseconds and a full mapping pass with a migrated default profile is
about 39 microseconds, so 1000 Hz costs roughly 5% of one core and the
500 Hz a 250 Hz controller asks for costs roughly half that.
