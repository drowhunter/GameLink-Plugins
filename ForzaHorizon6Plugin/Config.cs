using YawGLAPI;

namespace ForzaHorizon6Plugin
{
    public struct Config
    {
        [Info(Description = "UDP port the game is using")]
        public int Port = 20127;

        public Config()
        {
        }
    }
}

