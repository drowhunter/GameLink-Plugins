using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
#nullable disable
namespace SharedLib
{
    internal class GithubHelper : IDisposable
    {
        private const string _baseUrl = "https://github.com";
        public HashSet<string> TempFiles { get; private set; }

        public string UsernameOrOrg { get; }
        public string Repo { get; }
        public GithubOptions Options { get; }

        public GithubHelper(string usernameOrOrg, string repo, GithubOptions options = null)
        {
            UsernameOrOrg = usernameOrOrg;
            Repo = repo;
            Options = options ?? new GithubOptions();
            TempFiles = new HashSet<string>();
        }


        public void Dispose()
        {
            if (this.Options.CachingEnabled)
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

        public Task<List<GithubAsset>> DownloadAssetsByTagAsync(string tag, string[] filter = null, CancellationToken cancellationToken = default)
        {        
            Uri uri = new Uri($"{_baseUrl}/{UsernameOrOrg}/{Repo}/releases/tag/" + tag);

            return DownloadReleasesAsync(uri, filter, cancellationToken);
        }

        public Task<List<GithubAsset>> DownloadLatestAssetsAsync(string[] filter = null, CancellationToken cancellationToken = default)
        {
            Uri uri = new Uri($"{_baseUrl}/{UsernameOrOrg}/{Repo}/releases/latest");

            return DownloadReleasesAsync(uri, filter, cancellationToken);
        }

        public Task<List<GithubAsset>> GetAssetsByReleaseAsync(string release, string[] filter = null, CancellationToken cancellationToken = default)
        {
            Uri uri = new Uri($"{_baseUrl}/{UsernameOrOrg}/{Repo}/releases/tag/" + release);
            return DownloadReleasesAsync(uri, filter, cancellationToken);
        }

        private async Task<List<GithubAsset>> DownloadReleasesAsync(Uri uri, string[] filter = null, CancellationToken cancellationToken = default)
        {
            filter ??= Array.Empty<string>();   

            var assets = new List<GithubAsset>();

            var folder = Path.GetTempPath();
            

            using var httpClient = new HttpClient();

#if DEBUG
            Debugger.Launch();
#endif

            var response = await httpClient.GetAsync(uri, cancellationToken);


            if (response.IsSuccessStatusCode)
            {
                var html = await response.Content.ReadAsStringAsync(cancellationToken);

                var m = Regex.Match(html, @$"include-fragment.+?src=""(?<assets>{_baseUrl}/{UsernameOrOrg}/{Repo}/releases/expanded_assets/(?<version>.+?))""", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var assetsurl = m.Groups["assets"].Value;
                    string version = m.Groups["version"].Value.Replace(',', '.');

                    response = await httpClient.GetAsync(assetsurl);

                    if (response.IsSuccessStatusCode)
                    {
                        var html2 = await response.Content.ReadAsStringAsync(cancellationToken);

                        var dlLinks = Regex.Matches(html2, @$"href=""(?<dl>/{UsernameOrOrg}/{Repo}/releases/download/.+?)""", RegexOptions.Multiline | RegexOptions.IgnoreCase);

                        if (dlLinks.All(_ => _.Success))
                        {
                            assets = dlLinks.Select(_ => new GithubAsset { Name = Path.GetFileName(_.Groups["dl"].Value), Version = version, Href = _baseUrl + _.Groups["dl"].Value }).ToList();                            

                            if (filter.Any())
                            {
                                assets = assets.Where(r => filter.Any(a => r.Name.Contains(a))).ToList();
                            }

                            foreach (var asset in assets)
                            {
                                var fullPath = Path.Combine(folder, Path.GetFileName(asset.Href));

                                if (!Options.CachingEnabled || !File.Exists(fullPath))
                                {
                                    var tempFile = await DownloadHelper.DownloadFileAsync(asset.Href, folder, cancellationToken);
                                    if (tempFile != null)
                                    {
                                        
                                        TempFiles.Add(tempFile);
                                        asset.Href = tempFile;
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error downloading file " + asset.Href);

                                        break;
                                    }
                                }
                                else if(Options.CachingEnabled)
                                {
                                    Console.WriteLine("Using cached file " + fullPath);
                                }
                            }
                        }
                    }
                }
            }
            
            return assets;
        }




        internal class GithubAsset
        {
            public string Name { get; set; }

            public string Version { get; set; }

            public string Href { get; set; }

            public Task<string> DownloadTempFileAsync()
            {
                return DownloadHelper.DownloadFileAsync(Href);
            }
        }

        internal class GithubOptions
        {
            /// <summary>
            /// if true, temp files will be deleted when the object is disposed
            /// </summary>
            public bool CachingEnabled { get; set; } 
        }

    }
}
