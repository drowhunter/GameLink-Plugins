using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using YawGLAPI;

namespace SharedLib
{
    internal class DownloadHelper
    {
        public static async Task<string?> DownloadFileAsync(string url, CancellationToken cancellationToken = default)
        {

            using var httpClient = new System.Net.Http.HttpClient();
            string filename = Path.GetTempFileName();
            Console.WriteLine($"Downloading {url} to {filename}");
            try
            {
                var response = await httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var fileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                    var fn = response.Content.Headers?.ContentDisposition?.FileName;
                    Console.WriteLine($"Downloaded {fn} {fileBytes.Length} bytes");

                    await File.WriteAllBytesAsync(filename, fileBytes, cancellationToken);

                    return filename;
                }
                else
                {
                    throw new Exception("Error downloading file: " + response.ReasonPhrase);

                }
            }
            catch (Exception e)
            {
                
            }

            return null;
        }
    }
}
