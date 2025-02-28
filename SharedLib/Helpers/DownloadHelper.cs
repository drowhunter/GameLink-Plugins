using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

#nullable disable
namespace SharedLib
{

    internal class DownloadHelper
    {
        public static async Task<string> DownloadFileAsync(string url, string folder = null,  CancellationToken cancellationToken = default)
        {

            using var httpClient = new HttpClient();
            

            if(folder == null)
            {
                folder = Path.GetTempPath();
            }
            else if(!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            

            
            try
            {
                var response = await httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    //var fileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                    
                    var filename = response.Content.Headers?.ContentDisposition?.FileName ?? Path.GetFileName(Path.GetTempFileName());
                    
                    
                    var fullPath = Path.Combine(folder, filename);

                    using FileStream fileStream = File.Create(fullPath);

                    var s = await response.Content.ReadAsStreamAsync(cancellationToken);

                    await s.CopyToAsync(fileStream, cancellationToken);

                    //await File.WriteAllBytesAsync(fullPath, fileBytes, cancellationToken);

                    Console.WriteLine($@"Downloaded {url} to ""{fullPath}"" ");//({fileBytes.Length / 1024}) kb

                    return fullPath;
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
