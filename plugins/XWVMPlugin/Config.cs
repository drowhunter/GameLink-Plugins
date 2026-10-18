using SharedLib;

using YawGLAPI;

namespace YawVR_Game_Engine.Plugin
{
    public struct Config
    {
        [Info(Description = "IP Address to listen on (default 127.0.0.1)", Name = "IP Address", RegexValidator = ConfigValidator.IPValidator)]
        public string IP = "127.0.0.1";

        [Info(Description = "Port the game sends telemetry on (default 10555)", Name = "Port", RegexValidator = ConfigValidator.PortRange)]
        public int Port = 10555;

        public Config() { }
    }
}
