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

`DeviceLimitTests`

- `ScaleInput` with distinct forward/backward/roll limits over -180..180.
  Pitch is nose-down positive and not inverted by the profile, so positive pitch uses
  `PitchLimitF` and negative uses `PitchLimitB`. Other inputs pass through.
- Final Core pitch/roll outputs bounded and monotonic, and smooth across the Unity
  0/360 boundary. Samples ramp in from level because the pitch spike filter (limit 10)
  flattens the first jump.

`YawOutputTests`

- Multiple full turns at several tilts, simultaneous yaw/pitch/roll, full pitch and
  roll rotations, a seeded 20,000-step walk, a heading/tilt grid, Unity 0/360 and
  Core ±180 wraps, yaw reset, the captured flight, and isolated 1:1 yaw.
- Vibration: shake and warheads drive amplitude 50 at constant frequency 1, clamped to 254.

## Intentionally failing contracts

These are not skipped. They expose real defects:

- `WashoutMustNotCreateALargeFinalAngularJumpDuringASmoothTurn` (4): Core linearly
  interpolates wrapped yaw through the wrong side of the circle.
- `AntijumpMustNotDiscardASmoothTurnAtTheHeadingBoundary(1)`: Core's signed `%` at +180.
- `LaserFireMustProduceVibration`: the default profile (copied from X-Wing Alliance)
  inverts LaserFired (input 12), so Core's 0..254 amplitude clamp discards it.

## Sign conventions

Pitch is nose-down positive and Roll is left positive (raw Unity Euler angles), while
PitchInertia is nose-up positive and RollInertia is right positive.
`CapturedAngularRatesMatchCapturedOrientationChanges` pins these conventions.
