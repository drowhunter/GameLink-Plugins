# XWVMPlugin tests

Run from the solution directory:

```powershell
dotnet test .\tests\XWVMPlugin.Tests\XWVMPlugin.Tests.csproj -c Release
```

To compile the Core methods from another GameLink source copy:

```powershell
dotnet test .\tests\XWVMPlugin.Tests\XWVMPlugin.Tests.csproj -c Release `
  '-p:GameLinkCoreSource=D:\_srcyawvr\Gamelink\GameLink.Core\GameLink.Core'
```

The plugin reference stages its output in `obj\plugin-staging`, so test builds never
replace the installed GameLink plugin. The Core harness (`CoreSourceCompiler`,
`HeadlessCore.cs.txt`) is shared from `..\..\GameLink.Tests.Shared`; see
`RushOfBloodPlugin.Tests\README.md` for how it extracts and runs the real
`ProfileManager` calculations.

## Pipeline

Every pipeline sample is serialized to an XWVM JSON packet, merged by the plugin's
`XwvmConverter` (`SparseJsonDictConverter`), converted with `Telemetry.From`, read
with `InputHelper`, scaled with `XWVMPlugin.ScaleInput` (the read loop's method), and
fed to Core with the embedded `Default.yawglprofile`.

## Test data

- `Data\json-capture.jsonl`: 161 real JSON packets captured from XWVM on UDP 10555,
  including five laser-fire frames.
- `Data\simhub-capture.b64`: 40 real 1251-byte SimHub packets (base64, one per line).

## Coverage

`DecodeTests`

- Input order and indices (`GetInputData`), default-profile routing, embedded resources,
  export name and default port.
- Real JSON and SimHub captures: every input finite and in range, counters monotonic,
  exact laser frames, and captured angular rates agreeing with orientation changes.
- Sparse merging, reset, empty/non-object packets, vector strings (exponent,
  whitespace, partial, invalid) and arrays, Unity 0..360 to signed ±180 wrapping.
- Laser/warhead 0.2 s window, fire-count truncation, shake 0.5 s linear decay,
  ejection, neutral missing fields.
- Synthetic SimHub packets written at independently computed offsets: every mapped
  field, throttle ×100, pause and ejection flags, JSON-after-SimHub clearing `paused`,
  SimHub-after-JSON keeping orientation, short binary packets not treated as SimHub.
- Heading integration (rate × mission time, gaps > 1 s and backwards clocks resync,
  reset reseeds from `rot.y`), rate-based tilt scaled to `maxAVel`, and a live
  held-yaw-stick capture (`yaw-hold-capture.jsonl`) where Yaw must advance steadily
  in the stick direction with no reversals.

`DeviceLimitTests`

- `ScaleInput` with distinct forward/backward/roll limits over -180..180.
  Pitch is nose-down positive and not inverted by the profile, so positive pitch uses
  `PitchLimitF` and negative uses `PitchLimitB`. Other inputs pass through.
- Final Core pitch/roll outputs bounded and monotonic, and smooth through level.
  Samples ramp in from level because the pitch spike filter (limit 10) flattens the
  first jump.

`YawOutputTests`

- Multiple full turns at several tilts, simultaneous yaw/pitch/roll, full pitch and
  roll rotations, a seeded 20,000-step walk, a heading/tilt grid, 0/360 and
  Core ±180 wraps, yaw reset, the captured flight, and isolated 1:1 yaw.
  Packets come from `Packets.Flight`, which emits the `aVel` rates that make the
  decoded Yaw/Pitch/Roll hit the requested targets.
- Vibration: shake and warheads drive amplitude 50 at constant frequency 1, clamped to 254.

## Intentionally failing contracts

These are not skipped. They expose real defects:

- `WashoutMustNotCreateALargeFinalAngularJumpDuringASmoothTurn` (4): Core linearly
  interpolates wrapped yaw through the wrong side of the circle.
- `AntijumpMustNotDiscardASmoothTurnAtTheHeadingBoundary(1)`: Core's signed `%` at +180.
- `LaserFireMustProduceVibration`: the default profile (copied from X-Wing Alliance)
  inverts LaserFired (input 12), so Core's 0..254 amplitude clamp discards it.

## Sign conventions

XWVM yaws the ship about the normalized `Up - Forward` axis (`Ship.RotateFromInput`),
so holding yaw both yaws and banks the ship at 45°, and the world Euler yaw speeds up,
slows and can briefly reverse. The rig inputs therefore come from body rates:

- `Yaw`: heading seeded from `rot.y`, then integrated from `0.7071 × aVel.y` over `t`.
- `Pitch`: `aVel.x / maxAVel.x × 90`, nose-down positive.
- `Roll`: `-(aVel.z + 0.7071 × aVel.y) / maxAVel.z × 90`, left positive (includes the
  yaw-induced bank).
- `WorldYaw`/`WorldPitch`/`WorldRoll` (inputs 26–28): raw Unity Euler angles, pitch and
  roll wrapped to ±180. PitchInertia is nose-up positive and RollInertia is right positive.
  `CapturedAngularRatesMatchCapturedOrientationChanges` pins these conventions.
