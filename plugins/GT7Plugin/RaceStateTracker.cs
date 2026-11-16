using System.Numerics;

namespace GT7Plugin
{
    /// <summary>
    /// Decides whether a packet is live driving that should move the rig.
    ///
    /// Besides the flag/lap checks, it rejects two post-race states that look like racing:
    /// <list type="bullet">
    /// <item>Autopilot: after the race ends (also when the player was lapped, so LapCount never
    /// exceeds LapsInRace) the game drives the car. In '~' packets Unk1 (0x13C) carries the
    /// player's raw throttle pedal; while the player drives it matches Throttle (0x91) apart from
    /// short traction-control/shift cuts, under autopilot they disagree for seconds.</item>
    /// <item>Results screen: the game keeps sending the last packet unchanged (time of day,
    /// position and rotation stop moving).</item>
    /// </list>
    /// Counts are in packets; the game sends 60 per second.
    /// </summary>
    public class RaceStateTracker
    {
        /// <summary>Pedal vs throttle difference that counts as a disagreement.</summary>
        public const int PedalTolerance = 8;

        /// <summary>Disagreement this long means autopilot. Ignoring packets with ASM/TCS active,
        /// live driving disagreed for at most 79 packets (Music Rally); autopilot runs were 131-974.</summary>
        public const int AutopilotPackets = 120;

        /// <summary>ASM and TCS cut the throttle below the pedal for seconds at a time while the player drives.</summary>
        private const SimulatorFlags DriverAssists = SimulatorFlags.ASMActive | SimulatorFlags.TCSActive;

        /// <summary>Agreement this long, with the pedal pressed, means the player is driving again
        /// (e.g. a pit-limiter cut, not autopilot).</summary>
        public const int ReleasePackets = 60;

        /// <summary>Minimum pedal for agreement to count towards <see cref="ReleasePackets"/>.</summary>
        public const int ReleaseMinPedal = 20;

        /// <summary>Unchanged packets in a row that mean the game is frozen (results screen).</summary>
        public const int FrozenPackets = 30;

        private int lastPacketId = int.MinValue;
        private short lastLapCount;
        private short lastLapsInRace;
        private TimeSpan lastTimeOfDay;
        private Vector3 lastPosition;
        private Quaternion lastRotation;

        private int mismatchCount;
        private int agreeCount;
        private int frozenCount;

        public bool AutopilotLatched { get; private set; }
        public bool Frozen => frozenCount >= FrozenPackets;
        public bool Active { get; private set; }
        public string Reason { get; private set; } = "no packets";

        /// <summary>Feeds one packet and returns whether it is live driving.</summary>
        public bool Update(SimulatorPacket sp)
        {
            if (sp.PacketId == lastPacketId)
                return Active; // duplicate send of the same frame

            bool onTrack = sp.Flags.HasFlag(SimulatorFlags.CarOnTrack);
            bool loading = sp.Flags.HasFlag(SimulatorFlags.LoadingOrProcessing);
            bool paused = sp.Flags.HasFlag(SimulatorFlags.Paused);

            // A new session (countdown, retry, restart, back to the menu) clears the autopilot latch.
            bool newSession = !onTrack || loading || sp.LapCount == 0
                || sp.LapCount < lastLapCount || sp.LapsInRace != lastLapsInRace;
            if (newSession)
            {
                AutopilotLatched = false;
                mismatchCount = 0;
                agreeCount = 0;
            }

            bool unchanged = lastPacketId != int.MinValue
                && sp.TimeOfDayProgression == lastTimeOfDay
                && sp.Position == lastPosition
                && sp.Rotation == lastRotation;
            frozenCount = unchanged ? frozenCount + 1 : 0;

            if (sp.Unk1.HasValue)
                TrackPedal(sp.Unk1.Value, sp.Throttle, (sp.Flags & DriverAssists) != 0);

            lastPacketId = sp.PacketId;
            lastLapCount = sp.LapCount;
            lastLapsInRace = sp.LapsInRace;
            lastTimeOfDay = sp.TimeOfDayProgression;
            lastPosition = sp.Position;
            lastRotation = sp.Rotation;

            // Events without a lap total (Music Rally, free run, time trial) report LapsInRace = 0 and
            // count laps from 1, so "finished" only applies when there is a total. 0/0 is the grid,
            // a rolling start or a menu demo.
            bool preRace = sp.LapsInRace == 0 && sp.LapCount == 0;
            bool raceFinished = sp.LapsInRace > 0 && sp.LapCount > sp.LapsInRace;

            Reason = !onTrack ? "not on track"
                : paused ? "paused"
                : loading ? "loading"
                : preRace ? "pre-race"
                : raceFinished ? "race finished"
                : AutopilotLatched ? "autopilot"
                : Frozen ? "frozen"
                : "live";
            Active = Reason == "live";
            return Active;
        }

        private void TrackPedal(byte pedal, byte throttle, bool assistActive)
        {
            if (assistActive)
            {
                // Throttle is being cut by an assist: neither evidence of autopilot nor of driving.
                mismatchCount = 0;
                return;
            }

            if (Math.Abs(pedal - throttle) > PedalTolerance)
            {
                agreeCount = 0;
                if (++mismatchCount >= AutopilotPackets)
                    AutopilotLatched = true;
                return;
            }

            mismatchCount = 0;
            if (AutopilotLatched && pedal >= ReleaseMinPedal && ++agreeCount >= ReleasePackets)
            {
                AutopilotLatched = false;
                agreeCount = 0;
            }
        }
    }
}
