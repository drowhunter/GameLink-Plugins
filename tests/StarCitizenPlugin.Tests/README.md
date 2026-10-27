# StarCitizenPlugin tests

Run from the solution directory:

```powershell
dotnet test .\tests\StarCitizenPlugin.Tests\StarCitizenPlugin.Tests.csproj -c Release
```

These tests do not need the GameLink.Core harness.

## Parity with SC-Motion-Haptic-Bridge

`BridgeParityTests` replays `Data\events.ndjson` (a synthetic flight in the capture server's
JSON format: parked, lift-off, flight with boost, a glitch frame, a data gap, a rotational
shock, a hull hit, touchdown, seat exit) through `ScBridge` and compares every 100 Hz tick
with `Data\expected.ndjson`. The expected values come from the bridge's own Python code
(`lm_simhub3.Path` / `Seat`, `lm_simhub4.Haptics`), run tick for tick like `lm_simhub4.run()`.

Regenerate both files after a change to the bridge (`LM_SCALE=1`: the plugin reports m/s2,
not the bridge's 0.1 scaled SimHub values):

```bash
LM_SCALE=1 python3 tests/StarCitizenPlugin.Tests/gen_parity.py <SC-Motion-Haptic-Bridge>/scripts tests/StarCitizenPlugin.Tests/Data
```

`ScenarioExercisesEveryRule` fails if a regenerated scenario stops covering the stale ramp,
seat fade, impact pulse, ground changes or boost.

## Other tests

- `DecodeTests`: input order and default-profile routing, embedded resources, export name,
  default port, capture-server JSON parsing (vectors, quads, booleans, nulls, mark lines).
- `MotionTests`: axis mapping, glitch hold, landed gate, seat gate and fade-out, stale ramp,
  yaw heading.
