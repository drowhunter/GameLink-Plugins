using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SharedLib
{
    internal class GithubHelper : IDisposable
    {
        private const string _baseUrl = "https://github.com";
        public List<string> TempFiles { get; private set; } = new List<string>();

        public string UsernameOrOrg { get; }
        public string Repo { get; }
        public GithubOptions Options { get; }

        public GithubHelper(string usernameOrOrg, string repo, GithubOptions options = null)
        {
            UsernameOrOrg = usernameOrOrg;
            Repo = repo;
            Options = options ?? new GithubOptions();
        }


        public void Dispose()
        {
            if (this.Options.CleanTempFiles)
                CLeanTempFiles();
        }

        public void CLeanTempFiles()
        {
            foreach (var file in TempFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error deleting temp file " + file);
                }
            }
        }

        public async Task<List<GithubRelease>> GetLatestReleasesAsync(params string[] assets)
        {

            var retval = new List<GithubRelease>();

            var url = $"{_baseUrl}/{UsernameOrOrg}/{Repo}/releases/latest";

            using var _client = new HttpClient();



            Uri u = new(url);

            var response = await _client.GetAsync(u);


            if (response.IsSuccessStatusCode)
            {
                var html = await response.Content.ReadAsStringAsync();

                var m = Regex.Match(html, @$"include-fragment.+?src=""(?<assets>{_baseUrl}/{UsernameOrOrg}/{Repo}/releases/expanded_assets/(?<version>.+?))""", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var assetsurl = m.Groups["assets"].Value;
                    string version = m.Groups["version"].Value.Replace(',', '.');

                    response = await _client.GetAsync(assetsurl);

                    if (response.IsSuccessStatusCode)
                    {
                        var html2 = await response.Content.ReadAsStringAsync();

                        var a = Regex.Matches(html2, @$"href=""(?<dl>/{UsernameOrOrg}/{Repo}/releases/download/.+?)""", RegexOptions.Multiline | RegexOptions.IgnoreCase);

                        if (a.All(_ => _.Success))
                        {
                            retval = a.Select(_ => new GithubRelease { Name = Path.GetFileName(_.Groups["dl"].Value), Version = version, Href = _baseUrl + _.Groups["dl"].Value }).ToList();

                            //var dlUrl = a.Groups["dl"].Value;

                            //var dl = "https://github.com" + dlUrl;


                            if (assets.Any())
                            {
                                retval = retval.Where(r => assets.Any(a => r.Name.Contains(a))).ToList();
                            }

                            foreach (var filt in retval)
                            {
                                var tempFile = await DownloadHelper.DownloadTempFileAsync(filt.Href);
                                if (tempFile != null)
                                {
                                    this.TempFiles.Add(tempFile);
                                    filt.Href = tempFile;
                                }
                                else
                                {
                                    Console.WriteLine("Error downloading file " + filt.Href);

                                    break;
                                }
                            }
                        }
                    }
                }
            }


            if (assets.Any())
            {
                retval = retval.Where(_ => assets.Any(a => _.Name.Contains(a))).ToList();
            }

            return retval;
        }




        internal class GithubRelease
        {
            public string Name { get; set; }

            public string Version { get; set; }

            public string Href { get; set; }

            public Task<string> DownloadTempFileAsync()
            {
                return DownloadHelper.DownloadTempFileAsync(Href);
            }
        }

        internal class GithubOptions
        {
            /// <summary>
            /// if true, temp files will be deleted when the object is disposed
            /// </summary>
            public bool CleanTempFiles { get; set; } = true;
        }

    }
}
