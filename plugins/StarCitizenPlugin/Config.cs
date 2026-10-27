using SharedLib;

using YawGLAPI;

namespace YawVR_Game_Engine.Plugin
{
    public struct Config
    {
        [Info(Description = "IP Address to listen on (default 127.0.0.1)", Name = "IP Address", RegexValidator = ConfigValidator.IPValidator)]
        public string IP = "127.0.0.1";

        [Info(Description = "Port SC_Motion_Server.py forwards the game's events to (default 61703)", Name = "Port", RegexValidator = ConfigValidator.PortRange)]
        public int Port = 61703;

        public Config() { }
    }
}
