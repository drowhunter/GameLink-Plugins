using YawGLAPI;

namespace YawVR_Game_Engine.Plugin
{
    public struct Config
    {
        [Info(Description = "IP Address to listen on (default 127.0.0.1)", Name = "IP Address", RegexValidator = ConfigValidator.IPValidator)]
        public string IP = "127.0.0.1";

        [Info(Description = "Port shadPS4 sends Rush of Blood cart telemetry on (25151)", Name = "Port", RegexValidator = ConfigValidator.PortRange)]
        public int Port = 25151;

        public Config() { }
    }
}
