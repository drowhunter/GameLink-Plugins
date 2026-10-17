using System.Numerics;
using System.Runtime.InteropServices;

namespace DistancePlugin.Tests;

/// <summary>
/// Byte-for-byte mirror of com.drowhunter.DistanceTelemetryMod.DistanceTelemetryData
/// (Distance-TelemetryMod\DistanceTelemetryData.cs). The mod sends it with Marshal.StructureToPtr.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ModPacket
{
    public bool GamePaused;
    public bool IsRacing;
    public float KPH;

    public float Pitch;
    public float Yaw;
    public float Roll;

    public float AngularVelocityX;
    public float AngularVelocityY;
    public float AngularVelocityZ;

    public float cForce;

    public float VelocityX;
    public float VelocityY;
    public float VelocityZ;

    public float AccelX;
    public float AccelY;
    public float AccelZ;

    public bool Boost;
    public bool Grip;
    public bool WingsOpen;

    public bool IsCarEnabled;
    public bool IsCarIsActive;
    public bool IsCarDestroyed;
    public bool AllWheelsOnGround;
    public bool IsGrav;

    public float TireFL;
    public float TireFR;
    public float TireBL;
    public float TireBR;

    public float OrientationX;
    public float OrientationY;
    public float OrientationZ;
    public float OrientationW;

    public byte[] ToBytes()
    {
        int size = Marshal.SizeOf<ModPacket>();
        var bytes = new byte[size];
        nint buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(this, buffer, false);
            Marshal.Copy(buffer, bytes, 0, size);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return bytes;
    }
}

/// <summary>
/// Port of the mod's FixedUpdate + SharedLib-legacy TelemetryExtractor in EulerType.Coaster mode.
/// Unity and System.Numerics share quaternion algebra (Unity's q * v equals Vector3.Transform),
/// so a Unity rotation is represented directly by a System.Numerics quaternion with the same xyzw.
/// </summary>
internal sealed class DistanceModSimulator
{
    public const float FixedDeltaTime = 0.02f;
    private const float Rad2Deg = 57.29578f;

    private float _yaw;

    public Quaternion Rotation { get; private set; } = Quaternion.Identity;
    public Vector3 LocalAngularVelocity { get; private set; }
    public Vector3 LocalVelocity { get; set; } = new(0, 0, 40);

    public Vector3 Forward => Vector3.Transform(Vector3.UnitZ, Rotation);
    public Vector3 Right => Vector3.Transform(Vector3.UnitX, Rotation);

    public DistanceModSimulator(Quaternion? start = null)
    {
        if (start.HasValue) Rotation = Quaternion.Normalize(start.Value);
    }

    /// <summary>Advances one physics step by rotating about the car's own axes (rad/s).</summary>
    public ModPacket Step(Vector3 localAngularVelocity)
    {
        float angle = localAngularVelocity.Length() * FixedDeltaTime;
        if (angle > 0)
            Rotation = Quaternion.Normalize(Rotation * Quaternion.CreateFromAxisAngle(
                Vector3.Normalize(localAngularVelocity), angle));
        LocalAngularVelocity = localAngularVelocity;
        return Extract();
    }

    // TelemetryExtractor.ExtractTelemetry(EulerType.Coaster) followed by the mod's packet fill.
    private ModPacket Extract()
    {
        _yaw += LocalAngularVelocity.Y * FixedDeltaTime;
        var forward = Forward;
        var right = Right;
        float yaw = HemiCircle(_yaw * Rad2Deg % 360);
        float pitch = CopySign(Angle(new Vector3(forward.X, 0, forward.Z), forward), forward.Y);
        float roll = CopySign(Angle(new Vector3(right.X, 0, right.Z), right), right.Y);

        return new ModPacket
        {
            IsRacing = true,
            KPH = LocalVelocity.Length() * 3.6f,
            Pitch = pitch,
            Yaw = yaw,
            Roll = roll,
            AngularVelocityX = LocalAngularVelocity.X,
            AngularVelocityY = LocalAngularVelocity.Y,
            AngularVelocityZ = LocalAngularVelocity.Z,
            cForce = LocalVelocity.Length() * LocalAngularVelocity.Length() * UnitySign(LocalAngularVelocity.Y),
            VelocityX = LocalVelocity.X,
            VelocityY = LocalVelocity.Y,
            VelocityZ = LocalVelocity.Z,
            IsCarEnabled = true,
            IsCarIsActive = true,
            AllWheelsOnGround = true,
            IsGrav = true,
            OrientationX = forward.X,
            OrientationY = Rotation.Y,
            OrientationZ = Rotation.Z,
            OrientationW = Rotation.W,
        };
    }

    // Sharedlib.Maths.HemiCircle
    private static float HemiCircle(float degrees) => degrees >= 180 ? degrees - 360 : degrees;

    // Sharedlib.Maths.CopySign uses Mathf.Sign, which returns +1 for zero.
    private static float CopySign(float x, float y) => UnitySign(y) * Math.Abs(x);

    private static float UnitySign(float f) => f >= 0f ? 1f : -1f;

    // Unity 5.5 Vector3.Angle: acos(clamp(dot(from.normalized, to.normalized))) where
    // normalized returns zero when magnitude <= 1e-5 (so straight up/down yields 90 deg).
    private static float Angle(Vector3 from, Vector3 to) =>
        MathF.Acos(Math.Clamp(Vector3.Dot(UnityNormalized(from), UnityNormalized(to)), -1f, 1f)) * Rad2Deg;

    private static Vector3 UnityNormalized(Vector3 v)
    {
        float magnitude = v.Length();
        return magnitude > 1e-5f ? v / magnitude : Vector3.Zero;
    }
}
