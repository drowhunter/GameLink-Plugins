using System.Text.Json;
using System.Text.Json.Serialization;

namespace YawVR_Game_Engine.Plugin
{
    // Plan:
    // - Define class `XwaPlayer` with properties mapped to JSON keys using `JsonPropertyName`.
    // - Use `JsonNumberHandling.AllowReadingFromString` to allow numeric/bool values represented as strings.
    // - Choose appropriate types: string for names, int for integral values, double for fractional values, bool where applicable.
    // - Provide a static `Deserialize` method to parse from JSON string using `System.Text.Json`.

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public struxt XwaPlayer
    {
        [JsonPropertyName("XWA.player.shipname")]
        public string? ShipName { get; set; }

        [JsonPropertyName("XWA.player.crafttypename")]
        public string? CraftTypeName { get; set; }

        [JsonPropertyName("XWA.player.shortcrafttypename")]
        public string? ShortCraftTypeName { get; set; }

        [JsonPropertyName("XWA.player.speed")]
        public int Speed { get; set; }

        [JsonPropertyName("XWA.player.throttle")]
        public int Throttle { get; set; }

        [JsonPropertyName("XWA.player.ELSlasers")]
        public int ELSlasers { get; set; }

        [JsonPropertyName("XWA.player.ELSshields")]
        public int ELSshields { get; set; }

        [JsonPropertyName("XWA.player.ELSbeam")]
        public int ELSbeam { get; set; }

        [JsonPropertyName("XWA.player.s-foils")]
        public int SFoils { get; set; }

        [JsonPropertyName("XWA.player.shielddirection")]
        public int ShieldDirection { get; set; }

        [JsonPropertyName("XWA.player.shieldfront")]
        public int ShieldFront { get; set; }

        [JsonPropertyName("XWA.player.shieldback")]
        public int ShieldBack { get; set; }

        [JsonPropertyName("XWA.player.hull")]
        public int Hull { get; set; }

        [JsonPropertyName("XWA.player.shake")]
        public int Shake { get; set; }

        [JsonPropertyName("XWA.player.beamactive")]
        public int BeamActive { get; set; }

        [JsonPropertyName("XWA.player.undertractorbeam")]
        public bool UnderTractorBeam { get; set; }

        [JsonPropertyName("XWA.player.underjammingbeam")]
        public bool UnderJammingBeam { get; set; }

        [JsonPropertyName("XWA.player.activeweapon")]
        public string? ActiveWeapon { get; set; }

        [JsonPropertyName("XWA.player.laserfired")]
        public bool LaserFired { get; set; }

        [JsonPropertyName("XWA.player.warheadfired")]
        public bool WarheadFired { get; set; }

        [JsonPropertyName("XWA.player.yaw_inertia")]
        public double YawInertia { get; set; }

        [JsonPropertyName("XWA.player.pitch_inertia")]
        public double PitchInertia { get; set; }

        [JsonPropertyName("XWA.player.roll_inertia")]
        public double RollInertia { get; set; }

        [JsonPropertyName("XWA.player.accel_inertia")]
        public double AccelInertia { get; set; }

        public static XwaPlayer? Deserialize(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false
            };
            return JsonSerializer.Deserialize<XwaPlayer>(json, options);
        }
    }
}