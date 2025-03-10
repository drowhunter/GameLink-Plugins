using YawGLAPI;

namespace Dirt2Plugin
{
    public struct Config
    {
        [Info(Description = "Port the game sends data on (20777)", Name = "Port", RegexValidator = ConfigValidator.PortRange)]
        public int Port = 20777;

        public Config() { }

    }
}
