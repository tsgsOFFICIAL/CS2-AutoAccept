using System.IO;
using System.Linq;
using System.Net.Http;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CS2AutoAccept
{
    internal class Updater
    {
        string _repositoryOwner = "tsgsOFFICIAL";
        string _repositoryName = "CS2-AutoAccept.exe";
        string _folderPath = "CS2-AutoAccept.exe/bin/Release/net6.0-windows/publish/win-x86";
        long _totalFileSize = 0;
        long _downloadedFileSize = 0;

        /// <summary>
        /// Download the update
        /// </summary>
        internal async void DownloadUpdate(string downloadDirectory)
        {
            string apiUrl = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/contents/{_folderPath}";

            using (HttpClient client = new HttpClient())
            {
                IProgress<int> progress = new Progress<int>(percentComplete =>
                {
                    Debug.WriteLine($"Progress: {percentComplete}%");
                });

                client.DefaultRequestHeaders.Add("User-Agent", "request");
                await CalculateFolderSize(client, apiUrl, downloadDirectory);
                await DownloadFolderContents(client, apiUrl, downloadDirectory, progress);
            }
        }
        /// <summary>
        /// Calculate file sizes, and add them
        /// </summary>
        /// <param name="client"></param>
        /// <param name="apiUrl"></param>
        /// <param name="downloadDirectory"></param>
        /// <returns></returns>
        internal async Task CalculateFolderSize(HttpClient client, string apiUrl, string downloadDirectory)
        {
            HttpResponseMessage response = await client.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                GitHubContent[] contents = JsonSerializer.Deserialize<GitHubContent[]>(json)!;
                _totalFileSize += (long)contents.Sum(content => content.Size)!;

                foreach (GitHubContent content in contents)
                {
                    if (content.Type == "dir")
                    {
                        string subfolderPath = content.Path!;
                        await CalculateFolderSize(client, apiUrl.Replace(_folderPath, subfolderPath), downloadDirectory);
                    }
                }
            }
            else
            {
                Debug.WriteLine($"Failed to fetch folder contents. Status code: {response.StatusCode}");
            }
        }
        /// <summary>
        /// Download folder content
        /// </summary>
        /// <param name="client"></param>
        /// <param name="apiUrl"></param>
        /// <param name="downloadDirectory"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        internal async Task DownloadFolderContents(HttpClient client, string apiUrl, string downloadDirectory, IProgress<int> progress)
        {
            HttpResponseMessage response = await client.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                GitHubContent[] contents = JsonSerializer.Deserialize<GitHubContent[]>(json)!;

                if (!Directory.Exists(downloadDirectory))
                {
                    Directory.CreateDirectory(downloadDirectory);
                }

                foreach (GitHubContent content in contents)
                {
                    if (content.Type == "file")
                    {

                        string fileUrl = content.Download_url!;
                        string filePath = Path.Combine(downloadDirectory, content.Name!);

                        using (HttpResponseMessage fileResponse = await client.GetAsync(fileUrl))
                        {
                            if (fileResponse.IsSuccessStatusCode)
                            {
                                byte[] bytes = await fileResponse.Content.ReadAsByteArrayAsync();
                                File.WriteAllBytes(filePath, bytes);
                                Debug.WriteLine($"Downloaded {content.Name}");

                                // Increment _downloadedFileSize by the size of the downloaded file
                                _downloadedFileSize += bytes.Length;

                                // Calculate progress as a percentage of _downloadedFileSize relative to _totalFileSize
                                int percentComplete = (int)(((double)_downloadedFileSize / _totalFileSize) * 100);
                                progress.Report(percentComplete);
                            }
                            else
                            {
                                Debug.WriteLine($"Failed to download {content.Name}");
                            }
                        }
                    }
                    else if (content.Type == "dir")
                    {

                        string subfolderPath = content.Path!;
                        string subfolderDownloadDirectory = Path.Combine(downloadDirectory, content.Name!);
                        await DownloadFolderContents(client, apiUrl.Replace(_folderPath, subfolderPath), subfolderDownloadDirectory, progress);
                    }
                }
            }
            else
            {
                Debug.WriteLine($"Failed to fetch folder contents. Status code: {response.StatusCode}");
            }
        }
    }
    /// <summary>
    /// This class holds githubs file properties
    /// </summary>
    internal class GitHubContent
    {
        internal string? Name { get; set; }
        internal string? Path { get; set; }
        internal string? Type { get; set; }
        internal string? Download_url { get; set; }
        internal long? Size { get; set; }
    }
}
