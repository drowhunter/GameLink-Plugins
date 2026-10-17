# Verifying telemetry decoding

Use a throwaway console app **outside the solution** (e.g. a temp/scratch folder) that loads the
built plugin DLL by reflection. SharedLib types are `internal`, so reflection is required.

## 1. Produce sample packets

Prefer the game's own encoder (bridge/demo code) so expected values are authoritative.
Write one line per case: `<hex bytes> <expected values...>`. Example (Python):

```python
import sys
sys.path.insert(0, r"<path to telemetry sample>")
from <pkg>.feed_udp import encode_feed   # the game's encoder
from <pkg>.sample import CartSample
with open("cases.txt", "w") as f:
    for p, r, y in [(25, 0, 0), (-45, 8, 90), (-20, 35, -150)]:
        s = CartSample.from_euler_deg(p, r, y, speed=12.5)
        f.write(encode_feed(s).hex() + f" {p} {r} {y}\n")
```

If no encoder exists, build bytes with `struct.pack("<...", ...)` following the documented format.

## 2. Console app

`verify.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <Reference Include="{GameName}Plugin"><HintPath>{repo}\{GameName}Plugin\bin\Debug\net8.0\{GameName}Plugin.dll</HintPath></Reference>
    <Reference Include="YawGLAPI"><HintPath>{repo}\{GameName}Plugin\bin\Debug\net8.0\YawGLAPI.dll</HintPath></Reference>
  </ItemGroup>
</Project>
```

`Program.cs`:
```csharp
using System; using System.IO; using System.Linq; using System.Reflection; using System.Runtime.InteropServices;
var asm = Assembly.LoadFrom(@"{repo}\{GameName}Plugin\bin\Debug\net8.0\{GameName}Plugin.dll");
Console.WriteLine("Resources: " + string.Join(", ", asm.GetManifestResourceNames()));   // expect 5
var t = asm.GetType("YawVR_Game_Engine.Plugin.{GameName}TelemetryData");
Console.WriteLine("SizeOf=" + Marshal.SizeOf(t));                                        // expect packet size
var conv = Activator.CreateInstance(asm.GetType("SharedLib.TelemetryHelper.MarshalByteConverter`1").MakeGenericType(t));
var fromBytes = conv.GetType().GetMethod("FromBytes");
var plugin = Activator.CreateInstance(asm.GetType("YawVR_Game_Engine.Plugin.{GameName}Plugin"));
Console.WriteLine("Inputs: " + string.Join(",", (string[])plugin.GetType().GetMethod("GetInputData").Invoke(plugin, null)));
foreach (var line in File.ReadAllLines("cases.txt"))
{
    var parts = line.Split(' ');
    var bytes = Convert.FromHexString(parts[0]);
    var d = fromBytes.Invoke(conv, new object[] { bytes });
    var props = t.GetProperties().Select(p => $"{p.Name}={p.GetValue(d)}");
    Console.WriteLine($"expected[{string.Join(" ", parts.Skip(1))}] -> {string.Join(" ", props)}");
}
```

Run `dotnet run`. Compare actual and expected values for every case. Also test padded packet variants
(append zero bytes) and every flag bit. Delete the scratch folder afterwards.

## 3. Optional live check

Send the sample packets over UDP to the plugin's default port while GameLink runs the plugin,
and confirm the inputs move in the GameLink UI:

```python
import socket, time
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
for line in open("cases.txt"):
    s.sendto(bytes.fromhex(line.split()[0]), ("127.0.0.1", PORT)); time.sleep(1)
```
