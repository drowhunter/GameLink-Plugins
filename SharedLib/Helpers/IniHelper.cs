using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLib
{
    internal class IniHelper
    {
        public static async Task<Dictionary<string, string>> LoadFileAsync(string path, CancellationToken cancellationToken = default)
            => Parse(await File.ReadAllTextAsync(path, cancellationToken));

        public static Dictionary<string, string> Parse(string ini)
        {
            var dict = new Dictionary<string, string>();
            foreach (var line in ini.Split('\n'))
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                    dict[parts[0].Trim()] = parts[1].Trim();
            }
            return dict;
        }

    }
}
