using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

            var matches = Regex.Matches(ini, @"^(\w+?)\s?=\s?(.+?)$", RegexOptions.Multiline);
            var dict = matches.ToDictionary(x => x.Groups[1].Value.Trim(), x => x.Groups[2].Value.Trim());

            //var dict = new Dictionary<string, string>();
            //foreach (var line in ini.Split('\n'))
            //{
                

                

            //    //if (parts.Count > 0)
            //    //{

            //    //}
            //    //    dict[parts[0].Trim()] = parts[1].Trim();
            //}
            return dict;
        }

    }
}
