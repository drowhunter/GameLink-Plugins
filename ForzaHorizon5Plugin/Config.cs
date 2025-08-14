using YawGLAPI;

namespace ForzaHorizon5Plugin
{
    public struct Config
    {
        [Info(Description = "UDP port the game is using (default: 20127)")]
        public int Port = 20127;


        [Info(Description = "IP Address the game is using (default: 0.0.0.0)",RegexValidator = ConfigValidator.IPValidator)]
        public string IPAddress = "0.0.0.0";

        public Config()
        {
        }
    }
}
