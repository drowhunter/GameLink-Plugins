using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YawGLAPI;

namespace GT7Plugin
{
    public struct Config
    {
        [Info(Description = "UDP port the game is using")]
        public int Port = 33740;

        [Info(Description = "Where to send the heartbeat. 255.255.255.255 broadcasts to a PlayStation on the LAN; use 127.0.0.1 (or the PC's IP) for RyGT7 running on this PC", Name = "Game IP Address", RegexValidator = ConfigValidator.IPValidator)]
        public string IP = "255.255.255.255";

        [Info(Description = "Simulator Interface packet type: 1 = 'A' (0x128), 2 = 'B' (0x13C), 3 = '~' (0x158, GT7 1.42+). Type 3 is needed to detect the post-race autopilot", Name = "Packet Type")]
        public int PacketType = 3;

        public Config()
        {
        }
    }
}
