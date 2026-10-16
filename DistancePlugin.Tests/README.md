# DistancePlugin.Tests

Simulates Distance-TelemetryMod output for extreme car maneuvers and runs it through
`DistancePlugin` and the real GameLink.Core profile math (shared harness in
`..\GameLink.Tests.Shared`).

```powershell
dotnet test DistancePlugin.Tests\DistancePlugin.Tests.csproj
# optional: '-p:GameLinkCoreSource=D:\path\to\GameLink.Core'
```

## Pipeline

1. `DistanceModSimulator` ports the mod's `TelemetryExtractor.ExtractTelemetry(EulerType.Coaster)`
   (Unity 5.5 semantics) and emits byte-identical `ModPacket`s at 50 Hz.
2. Packets are decoded with the plugin's struct, flattened with `InputHelper.GetValues`
   and scaled with `DistancePlugin.ScaleInput` (device pitch/roll limits).
3. Inputs go through Core's `DoMaths` with the default profile, an orientation-only
   profile, or an isolated 1:1 Yaw profile. The final `SetOrientation` values are checked.

## Maneuvers

Flat spins, barrel rolls, loops (nose up/down), a loop while turning, a corkscrew,
wall rides, driving upside down, steering on a vertical wall, 60 consecutive spins,
and four seeded 60-second random tumbles.

## Why Distance is smooth

- **Yaw** is the integral of the local angular velocity Y, not an Euler heading. It
  doesn't have a gimbal singularity. Loops and rolls leave yaw unchanged. The only
  numeric discontinuities are ±360 wraps, which are angularly continuous.
  `ConventionalEulerHeadingWouldFlipDuringTheSameManeuver` shows that an Euler heading
  flips about 178° during the same loops.
- **Pitch and roll** are the angles of the forward and right vectors to the horizontal
  plane. They fold at ±90 instead of wrapping, so they stay bounded and continuous.
  Unity's float `acos` adds about 0.03° of noise near level, which the tests tolerate.

## Notes

- The tests found a stack overflow: `InputHelper.GetValues` recursed forever through the
  static `Zero`/`UnitX`/... properties of `System.Numerics.Vector3`. This is fixed in
  `SharedLib\Helpers\InputHelper.cs`, which now enumerates only public instance
  properties and skips indexers.
- The default profile adds boost, tire and sway cues to the pitch and roll outputs. These
  can push the final values past the device limits, so the limit tests use an
  orientation-only profile.
