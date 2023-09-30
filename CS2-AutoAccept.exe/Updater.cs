using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace CS2AutoAccept
{
    internal class Updater
    {
        private readonly string _repositoryOwner = "tsgsOFFICIAL";
        private readonly string _repositoryName = "CS2-AutoAccept.exe";
        private readonly string _folderPath = "CS2-AutoAccept.exe/bin/Release/net6.0-windows/publish/win-x86";
        private long _totalFileSize = 0;
        private long _downloadedFileSize = 0;
        public event EventHandler<int>? ProgressUpdated;

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
                    UpdateProgress(percentComplete);
                    Debug.WriteLine($"Progress: {percentComplete}%");
                });

                client.DefaultRequestHeaders.Add("User-Agent", "request");
                await CalculateFolderSize(client, apiUrl, downloadDirectory);
                await DownloadFolderContents(client, apiUrl, downloadDirectory, progress);

                Debug.WriteLine("Download completed");
                Process.Start(Path.Combine(downloadDirectory, "CS2-AutoAccept.exe"));
                Environment.Exit(0);
            }
        }
        /// <summary>
        /// Calculate file sizes, and add them
        /// </summary>
        /// <param Name="client"></param>
        /// <param Name="apiUrl"></param>
        /// <param Name="_updateDirectory"></param>
        /// <returns></returns>
        private async Task CalculateFolderSize(HttpClient client, string apiUrl, string downloadDirectory)
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
        /// <param Name="client"></param>
        /// <param Name="apiUrl"></param>
        /// <param Name="_updateDirectory"></param>
        /// <param Name="progress"></param>
        /// <returns></returns>
        private async Task DownloadFolderContents(HttpClient client, string apiUrl, string downloadDirectory, IProgress<int> progress)
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

                        string fileUrl = content.DownloadUrl!;
                        string filePath = Path.Combine(downloadDirectory, content.Name!);

                        using (HttpResponseMessage fileResponse = await client.GetAsync(fileUrl))
                        {
                            if (fileResponse.IsSuccessStatusCode)
                            {
                                byte[] bytes = await fileResponse.Content.ReadAsByteArrayAsync();
                                File.WriteAllBytes(filePath, bytes);
                                Debug.WriteLine($"Downloaded {content.Name}");

                                // Increment _downloadedFileSize by the Size of the downloaded file
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
                //await DownloadFolderContents(client, apiUrl, _updateDirectory, progress);
            }
        }
        /// <summary>
        /// Raises ProgressUpdatedEvent
        /// </summary>
        /// <param Name="progress">An integer (0-100)</param>
        public void UpdateProgress(int progress)
        {
            // Raise the event to notify the subscribers
            OnProgressUpdated(progress);
        }
        /// <summary>
        /// Raise the event
        /// </summary>
        /// <param Name="progress"></param>
        protected virtual void OnProgressUpdated(int progress)
        {
            ProgressUpdated?.Invoke(this, progress);
        }
    }
    /// <summary>
    /// This class holds githubs file properties
    /// </summary>
    internal class GitHubContent
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }
    }
}