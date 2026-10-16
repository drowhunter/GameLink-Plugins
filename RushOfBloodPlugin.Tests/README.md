# RushOfBloodPlugin final-yaw tests

Run from the solution directory:

```powershell
dotnet test .\RushOfBloodPlugin.Tests\RushOfBloodPlugin.Tests.csproj -c Release
```

The plugin project reference redirects its deployment target into this project's
`obj\plugin-staging` directory. Running this test project does not replace the
installed GameLink plugin or move hardware.

## Source of the calculations

The default reference folder is the attached
`D:\Ripped\YawVR GameLink 2\GameLink.Core`. To use another copy:

```powershell
dotnet test .\RushOfBloodPlugin.Tests\RushOfBloodPlugin.Tests.csproj -c Release `
  '-p:GameLinkCoreSource=D:\path\to\GameLink.Core'
```

The tests compile **the actual method bodies** from that folder at runtime:
`ProfileManager.CalculateComponent`, `CalculateOutput`, `DoConditions`,
`DoMaths`, `Deadzone`, `AntiRollover`, and the helper methods they use.
There is no hand-written replacement for the profile calculations. Missing or
incompatible source fails the tests rather than falling back to a simplified model.
The attached decompiled project has duplicate fields and cannot be compiled
unchanged as a whole, so only these methods are extracted using Roslyn.

The harness (`CoreSourceCompiler.cs`, `HeadlessCore.cs.txt`, and
`GameLinkCoreHarness.props`) lives in `..\GameLink.Tests.Shared` and is shared
with `DistancePlugin.Tests`.

`HeadlessCore.cs.txt` supplies state and replaces application lifecycle, singleton
initialization, USB (absent), notifications, and device I/O. Its device controller
captures the **final `SetOrientation` yaw**, after profile inversion, offset reset,
anti-jump and global washout. Integer output constants match Core's `OutputType`
enum; this permits the decompiled source's otherwise-invalid enum array indices.
LimitSmooth is disabled, since it only affects pitch/roll; enabling it fails
explicitly in this fixture.

## Pipeline and coverage

Each sample is encoded into a real 52- or 56-byte little-endian packet, decoded
through the plugin's `MarshalByteConverter`, read with its `InputHelper`, and
scaled as in its read loop. The built DLL's embedded `Default.yawglprofile`
provides the actual components. All generated samples are in play and unpaused.

Coverage includes:

- Multiple positive/negative full yaw turns in 0.25-degree steps, including
  0, +/-90 and +/-180, with level and tilted carts up to +/-89-degree pitch.
- Every heading degree from -360 through +360 at 11 pitch values and
  13 roll values: 103,103 final outputs.
- Simultaneous varying yaw/pitch/roll, full roll rotations, full pitch rotations,
  and a reproducible 20,000-step orientation walk.
- Alternating quaternion signs (`q` and `-q`), both packet lengths, input ordering,
  profile output routing, yaw offset resets, anti-jump on/off, and washout
  factors 1, 0.5 and 0.02 (Core's temporary-washout starting value).

This is a finite deterministic simulation, not a recording of every pose the
game can emit or proof of hardware behavior. The emitter sends quaternions, not
independent Euler angles; generated Y-X-Z rotations exercise that wire contract.
Frame timing, packet loss, USB components and edited user profiles are not simulated.

## What "smooth" means

Both numeric and shortest-arc angular changes matter. Without smoothing,
`+179.75 -> -179.75` is a large numeric change but a small, forward 0.5-degree
rotation. One test explicitly asserts this distinction on the **final output**.
Comparing only raw yaw, or simply ignoring all boundary jumps, would miss Core's
washout defect: it linearly interpolates the wrapped numbers through the wrong
side of the circle.

Ordinary output steps must match the expected direction and magnitude within
0.02 degrees. Washout must not turn a 0.25-degree input step into a larger angular
step. Full pitch rotations permit at most a 1-degree final-yaw step; they expose
the undefined heading at vertical pitch rather than excluding it from coverage.

The suite intentionally includes failing smoothness contracts for defects in the
current plugin/Core combination. Failures are not skipped or converted into
"expected failure" successes. In particular, smooth output cannot be claimed for
all poses/settings: vertical-pitch Euler singularities, anti-jump's signed `%`
boundary handling, and linear washout of wrapped yaw need separate fixes.
The suite does not modify the external Core source or the installed application.

## Temporary yaw-only isolation

```powershell
dotnet test .\RushOfBloodPlugin.Tests\RushOfBloodPlugin.Tests.csproj -c Release `
  --filter "FullyQualifiedName~Isolated"
```

These tests clone the embedded profile in memory and disable every component
except input Yaw (1) to output Yaw (0). Inversion, offsets, deadzone, conditions,
math, spike filtering and limits are disabled; both multipliers and smoothing
are 1. Core anti-jump is off, global washout is 1, and no yaw reset is requested.
The saved and embedded production profiles are not modified.

Each sample asserts final yaw equals decoded plugin yaw numerically within
0.02 degrees (allowing the equivalent +/-180 endpoint because Core always
normalizes yaw), and all other output channels are zero. The numeric-jump
contracts still fail at these wrap crossings; they are not hidden. Separate smoothness
contracts check both numeric and shortest-arc steps (maximum 1 degree) over
four complete turns of yaw, pitch, roll, and simultaneous yaw/pitch/roll,
in both directions at 0.25-degree increments. Every trajectory runs to completion
before reporting its worst discontinuity. These contracts can fail even with
perfect 1:1 mapping: wrapped headings have numeric jumps, while vertical-pitch
Euler singularities can create actual angular jumps.

`IsolatedYawStaysSmoothWithinNonLoopingCoasterLimits` restricts pitch to
[-70, +70] degrees and roll to [-45, +45], with smooth non-looping oscillations
that reach both endpoints. Yaw makes twenty unrestricted turns in each
direction at quarter-degree steps (57,602 samples total). With the yaw-only 1:1
profile, every final angular step must match the signed input step within
0.02 degrees, and final heading must match the simulated heading. Numeric wraps
are counted and checked at the +/-180 boundary, not confused with angular
reversals. The test does not claim that a bounded wrapped heading is numerically
continuous: no processing-free output in [-180,180] can be numerically continuous
through unlimited full turns.

`IsolatedYawStaysSmoothWithPitchLoopsAndLimitedRoll` uses the same isolated
profile and final signed-yaw-step expectation, but replaces the bounded pitch
oscillation with twenty full pitch loops. Roll remains within +/-45 degrees,
and yaw completes twenty turns. All four combinations of yaw/pitch direction
are tested (115,204 samples), including vertical-pitch crossings. Each trajectory
finishes before asserting its worst error, so a singularity does not truncate
coverage. This smoothness contract is expected to expose the current Euler
heading discontinuity rather than hide it with a relaxed threshold.

## Device limits

`DeviceLimitTests` exercises the same `ScaleInput` method used by the live receive
loop, with distinct forward/backward pitch and roll limits and deliberately
different maximum capabilities. It checks every integer input from -180 to 180,
including clamping, zero limits and unchanged yaw. Final-output tests execute
the attached Core methods with the embedded default profile over -90..90 degrees
in quarter-degree steps, checking bounded, monotonic output including endpoints:
positive final pitch uses `PitchLimitF` and negative final pitch uses `PitchLimitB`
(matching GameLink's calibration sweep and DistancePlugin),
and both roll directions use `RollLimit`. Pitch pre-scaling accounts for the
default profile's inversion. Custom profile changes are not a device safety clamp.
