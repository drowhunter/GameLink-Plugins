using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLib
{
    #nullable disable
    internal class IniHelper
    {
        readonly static Regex heading = new Regex(@"\[(.+?)\]");
        readonly static Regex kvp = new Regex(@"(.+?)=(.*)");

       
        public static async Task<Dictionary<string, Dictionary<string, string>>> LoadAsync(string path, CancellationToken cancellationToken = default)
        {
             var dict = new Dictionary<string, Dictionary<string, string>>();
            string k = null;
            
            if(!File.Exists(path))
                throw new FileNotFoundException("File not found", path);

            int i = 0;
            await foreach (var line in File.ReadLinesAsync(path))
            {
                i++;   
                var hm = heading.Match(line);
                if (hm.Success)
                {
                    k = hm.Groups[1].Value.Trim();
                    dict[k] = [];
                    continue;
                }

                var m = kvp.Match(line);
                if (m.Success)
                {
                    if(k != null) { 
                        dict[k][m.Groups[1].Value] = m.Groups[2].Value?.Trim();
                    }
                    else
                    {
                        throw new Exception(string.Format("Invalid INI file. Line {0} - {1}", i, line));
                    }
                }
            }
            
            return dict;
        }



    }
}
