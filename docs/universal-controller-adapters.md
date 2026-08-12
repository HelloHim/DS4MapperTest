# Universal controller adapters

Step 3 adds an opt-in universal controller layer beside the existing mapper
runtime. It does not select profiles, invoke actions, send output, migrate
profile files or replace any native reader.

## Value conventions

Universal values keep the shape declared by `UniversalInputValueKind`.

* Buttons are pressed or released.
* Triggers are normalised to `0.0` released through `1.0` fully pressed.
* Sticks are normalised to `-1.0` through `1.0`; X is positive right and Y
  is positive up.
* Touch coordinates are normalised to `0.0` through `1.0` with the origin at
  the upper-left when the backend supplies that convention. Contact presence,
  contact ID, pressure and click state are separate.
* Gyroscope values are radians per second.
* Accelerometer values are metres per second squared.

The adapter layer performs only backend range and unit conversion. It does not
apply user calibration, deadzones, response curves, sensitivity, smoothing or
controller-specific motion correction.

SDL3 gamepad ranges follow the SDL3 documentation: gamepad stick axes report
`-32768` for up or left through `32767` for down or right, triggers report
`0` through `32767`, touchpad fingers report normalised `0..1` X and Y from
the upper-left, gyroscopes report radians per second and accelerometers report
metres per second squared.

## SDL3 production adapter

`SdlUniversalControllerBackend` reuses the Step 2 `ISdlDiagnosticApi` boundary
for SDL lifecycle, enumeration, hot-plug events and raw state refresh. It maps
only stable SDL gamepad controls:

* south, east, west and north face positions;
* d-pad directions;
* shoulders and stick clicks;
* `Start`, `Back` and `Guide` navigation controls;
* paired left and right sticks;
* analogue left and right triggers;
* SDL paddle buttons to neutral rear-control positions;
* SDL miscellaneous buttons to neutral numbered miscellaneous controls;
* a single SDL touchpad to the universal primary touch surface;
* enabled `Gyro` and `Accel` sensors using SDL's documented units.

SDL `Misc1` through `Misc6` remain neutral. They are not interpreted as Mute,
Capture, Share, Quick Access Menu or any controller-family label in this step.
A controller with two SDL touchpads is not assigned left/right universal pads
unless a later verified device metadata policy supplies that evidence.

## Native Steam Controller adapter

`SteamControllerUniversalController` observes the existing decoded
`SteamControllerState`. The existing mapper still owns the reader and device
lifetime. The adapter preserves the original Steam Controller's face buttons,
d-pad state, shoulders, analogue triggers, trigger clicks, single stick, stick
click, two independent touchpads, pad clicks, grips, gyro and accelerometer.

Calibration, lizard mode, haptics, device options, aliases and connection
preferences remain controller-specific and outside universal profile data.

## Arbitration

The universal manager treats backend session IDs as transient and hardware
identity as best-effort. It does not collapse identical controllers solely by
VID, PID or name.

Original Steam Controller 2015 hardware is owned by the native Steam Controller
backend. The SDL diagnostics window may still show the raw SDL device, but the
production SDL universal backend suppresses it and the universal manager will
not publish it as an SDL-owned authoritative logical controller.

## Inspection

The existing `SDL Diagnostics` window has a `Universal` tab. It translates the
selected raw SDL diagnostic snapshot into universal IDs and values for
inspection only. This view does not start another SDL lifecycle and does not
drive actions or output.
