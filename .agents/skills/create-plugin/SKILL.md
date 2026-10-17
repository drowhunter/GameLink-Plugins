---
name: create-plugin
description: Create a new YawVR GameLink game plugin in the GameLink-Plugins solution (modeled on DistancePlugin / RushOfBloodPlugin). Use when asked to add, scaffold, or write a plugin for a game that emits motion telemetry (UDP, TCP, or shared memory), including deriving the telemetry struct from a game/mod/bridge sample, writing description.html, a default profile, and generating original logo/banner images.
---

# create-plugin

Scaffold, implement, build, and verify a new GameLink plugin in this repo.
Reference implementations: `DistancePlugin` (UDP + Unity mod patcher) and
`RushOfBloodPlugin` (UDP from an emulator, non-Steam, no patcher).

All paths below are relative to the solution root (the folder containing `Plugins.slnx`).
Templates live in `.agents/skills/create-plugin/templates/`, scripts in `.agents/skills/create-plugin/scripts/`.

## 0. Gather inputs (ask the user if unknown, one question at a time)

| Input | Example | Notes |
|---|---|---|
| `GameName` (PascalCase, no spaces) | `RushOfBlood` | Folder = `{GameName}Plugin`, assembly = `{GameName}Plugin` |
| Display title | `Until Dawn: Rush of Blood` | Goes in `ExportMetadata("Name", ...)`; full name, may contain spaces/punctuation |
| Telemetry source | sample code, packet spec, mod repo | Required to derive the struct |
| Transport | UDP / TCP / MMF | Prefer UDP when the game supports it |
| Steam AppId | `233610` or none | Non-Steam games: `STEAM_ID => 0` |
| Process name | `shadps4` | Executable name without `.exe` |
| Install/download source | GitHub repo/releases URL | Used in description.html |
| Patch/mod installer? | Unity BepInEx mod repo or none | Determines `PATCH_AVAILABLE` / `PatchGame()` |
| Author | `Trevor Jones (Drowhunter)` | Copy from sibling plugins if not given |

## 1. Analyze the telemetry source

Read every file of the sample (emitter, bridge, decoder, demo). Produce a table of the **wire
format the plugin will actually receive**:

- Byte order. `MarshalByteConverter` is little-endian on x86. If the wire is big-endian,
  write a custom `IByteConverter<T>` that uses `BinaryPrimitives`.
- Exact field order, types, sizes, total length, and any padding variants (e.g. 52 vs 56 bytes).
- Units and axis conventions (m/s vs km/h, degrees vs radians, Y-up vs Z-up, quaternion vs Euler).
- Flag bit meanings (in-play, paused, etc.) used to gate motion.
- Default host/port and how the game enables/disables telemetry.

If a bridge re-serves the data (e.g. UDP → TCP), prefer consuming the **original** feed directly
so users need no extra process. Note this in the description.

Validate quaternion-to-Euler helpers against the emitter's physical axes before reusing them.
A bridge/demo can round-trip its own formulas while labeling the axes incorrectly. For a
Y-up, Z-forward cart, yaw is about Y, pitch about X and roll about Z. Test individual axes
and combined rotations, including heading sweeps through 90 and 180 degrees. Keep yaw as
an absolute heading for GameLink, which already wraps yaw; do not mask incorrect axis
conversions with smoothing or angle unwrapping.

## 2. Create the project

```
{GameName}Plugin/
  {GameName}Plugin.csproj        <- templates/Plugin.csproj.template
  {GameName}Plugin.cs            <- templates/Plugin.cs.template
  {GameName}TelemetryData.cs     <- templates/TelemetryData.cs.template
  Config.cs                      <- templates/Config.cs.template
  Resources/description.html     <- templates/description.html.template
  Resources/Default.yawglprofile <- adapted from a similar plugin (step 7)
  Resources/logo.png   220x175   <- scripts/generate_images.py (step 8)
  Resources/recent.png 100x100
  Resources/wide.png   1087x287
```

Replace every `{{Token}}` in the templates: `{{GameName}}`, `{{Title}}`, `{{SteamId}}`,
`{{SteamIdComment}}`, `{{ProcessName}}`, `{{Author}}`, `{{DefaultIP}}`, `{{DefaultPort}}`,
`{{PortDescription}}`, `{{PatchAvailable}}`, `{{LedColor}}`, and the description placeholders.
Search for `{{` afterwards to confirm none remain.

Then: `dotnet sln Plugins.slnx add plugins/{GameName}Plugin/{GameName}Plugin.csproj`

**Resources must be `EmbeddedResource`.** The csproj template already does this.
`ResourceHelper` resolves `{AssemblyName}.Resources.{file}`, so AssemblyName must be `{GameName}Plugin`.

## 3. Telemetry struct rules

- `[StructLayout(LayoutKind.Sequential, Pack = 1)]`, `internal struct`.
- **Private fields** mirror the wire format exactly (order + types). Use `float`/`int`/`uint`/
  `byte`. Do not use `bool` for wire fields (it marshals as 4 bytes). Do not use `Vector3`/`Quaternion`
  (reflection in `InputHelper` recurses into their static members).
- **Public get-only properties** are the plugin inputs. `InputHelper.GetValues` uses
  properties when any exist, in declaration order. That order is the input index used by
  the profile. Put primary motion inputs first (Speed, Yaw, Pitch, Roll, G-forces), then
  secondary values, then flags.
- Derive Euler angles, unit conversions, and flag bits (`bool` props) in properties.
- Verify `Marshal.SizeOf<T>()` equals the packet size. A larger received buffer is fine;
  a smaller one is not.

## 4. Plugin class rules

- `[Export(typeof(Game))]`, `[ExportMetadata("Name", "{{Title}}")]`, `[ExportMetadata("Version", "1.0")]`.
- Use the SharedLib telemetry helpers. Do not hand-roll sockets:
  - UDP: `new UdpTelemetry<T>(new UdpTelemetryConfig { ReceiveAddress = new IPEndPoint(IPAddress.Parse(settings.IP), settings.Port), ReceiveTimeout = 1000 }, new MarshalByteConverter<T>())`
    (`SharedMode` defaults to true so other apps can share the port).
  - TCP: `TcpTelemetry<T>` + `TcpTelemetryConfig`. Shared memory: `MmfTelemetry<T>` + `MmfTelemetryConfig`.
  - Other converters: `JsonByteConverter<T>`, `StringByteConverter`, `RawBytesConverter`.
- `GetInputData() => InputHelper.GetValues<T>(default).Keys();`
- Read loop: `foreach (var (i, key, value) in InputHelper.GetValues(data).WithIndex())`. Scale
  Pitch/Roll with `MathsF.ScalePitchRoll(value, -90, 90, -deviceParameters.PitchLimitF, deviceParameters.PitchLimitB)`
  / `RollLimit`, pass everything else through, then `controller.SetInput(i, nv)`.
- Gate motion on the game's in-play/paused flags with the `isResetting` debounce from Distance.
- Catch `SocketException` (receive timeouts) inside the loop; break on `ObjectDisposedException`.
- `Exit()` sets `running = false` and disposes telemetry.
- Non-Steam: `STEAM_ID => 0`. No mod installer: `PATCH_AVAILABLE => false` and empty `PatchGame()`.
  Unity BepInEx mod: copy Distance's `UnityPatcher.Create<UnityPatcher>(...)` block and set `PATCH_AVAILABLE => true`.

## 5. Config

`public struct Config` with `[Info(...)]` attributes, `RegexValidator = ConfigValidator.IPValidator`
/ `ConfigValidator.PortRange`. Default IP: `127.0.0.1` if the game sends to loopback,
`0.0.0.0` if it may come from another machine. Default port = the game's default.

## 6. description.html

Use the template (same CSS as other plugins). It must include:
1. What the game is and how it runs (platform, emulator, mod).
2. **Where to get it**: link `releases/latest` and the project page, the minimum version that emits telemetry, and any legal note (e.g. no game files provided).
3. Step-by-step: start plugin → launch game → play.
4. Notes: default IP/port, how telemetry is enabled/disabled, any optional bridge tools that are not required.
5. `Version x.y.z` footer matching the csproj.

## 7. Default profile

Copy the `Default.yawglprofile` of the most similar existing plugin (coaster → `NoLimits2Plugin`,
car → a racing plugin, flight → a flight plugin). Remap every `input_index` to the new input
order. Set `inverse: true` where the reference plugin negated values in code. Optionally recolor
`effects.colors`. Check that it is valid JSON. Verify output indices independently of the
reference profile: GameLink uses 0 = yaw, 1 = pitch, 2 = roll. A copied profile may have
pitch and roll crossed, even if its input names look correct.

## 8. Images (original artwork only)

Never copy official key art or logos (copyright). Generate original art:

```
python .agents/skills/create-plugin/scripts/generate_images.py --out {GameName}Plugin/Resources \
  --title "RUSH OF BLOOD" --subtitle "UNTIL DAWN" --short "ROB" --theme coaster \
  --accent 200,10,10 --glow 110,10,14 --drips
```

Themes: `coaster`, `road`, `sky`. Requires Pillow (`pip install pillow`). View all three
images afterwards and iterate on colors and text until they are legible.

## 9. Build and verify

1. `dotnet build {GameName}Plugin/{GameName}Plugin.csproj -c Debug` → 0 errors.
2. Decode check: follow `scripts/verify_decode.md`. Feed sample packets (from the game's own
   encoder when available) through the built DLL and confirm every property matches,
   including padded packet variants and flag bits. Confirm all 5 resources are embedded and
   `GetInputData()` returns the expected order.
3. Delete scratch test projects. Do not add test projects to the solution.

## 10. Report

Summarize the files created, the wire format, the input list (with indices), the defaults, and anything not
yet verified in-game (e.g. axis signs on real hardware).
