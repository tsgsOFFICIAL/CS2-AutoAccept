using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace CS2AutoAccept
{
    internal class Updater
    {
        private readonly string _repositoryOwner = "tsgsOFFICIAL";
        private readonly string _repositoryName = "CS2-AutoAccept.exe";
        private readonly string _folderPath = "CS2-AutoAccept.exe/bin/Release/net6.0-windows/publish/win-x86";
        private long _totalFileSize = 0;
        private long _downloadedFileSize = 0;
        private bool _downloadComplete = true;
        public event EventHandler<ProgressEventArgs>? ProgressUpdated;

        /// <summary>
        /// Download the update
        /// </summary>
        internal async void DownloadUpdate(string downloadDirectory)
        {
            string apiUrl = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/contents/{_folderPath}";


            using (HttpClient client = new HttpClient())
            {
                IProgress<int> progress = new Progress<int>(percentageComplete =>
                {
                    UpdateProgress(new ProgressEventArgs(percentageComplete, "good"));
                    Debug.WriteLine($"Progress: {percentageComplete}%");
                });

                client.DefaultRequestHeaders.Add("User-Agent", "request");
                _totalFileSize = await CalculateFolderSize(client, apiUrl);
                await DownloadFolderContents(client, apiUrl, downloadDirectory, progress);

                if (_downloadComplete)
                {
                    Debug.WriteLine("Download completed");
                    Process.Start(Path.Combine(downloadDirectory, "CS2-AutoAccept.exe"));
                    Environment.Exit(0);
                }

                UpdateProgress(new ProgressEventArgs(0, "bad"));
            }
        }
        /// <summary>
        /// Calculate file sizes, and add them
        /// </summary>
        /// <param Name="client"></param>
        /// <param Name="apiUrl"></param>
        /// <param Name="_updatePath"></param>
        /// <returns></returns>
        private async Task<long> CalculateFolderSize(HttpClient client, string apiUrl)
        {
            long totalSize = 0;
            string nextPageUrl = apiUrl;

            do
            {
                HttpResponseMessage response = await client.GetAsync(nextPageUrl);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    GitHubContent[] contents = JsonSerializer.Deserialize<GitHubContent[]>(json)!;

                    totalSize += contents.Sum(content => content.Size ?? 0);

                    // Check if there's a next page
                    if (response.Headers.TryGetValues("Link", out IEnumerable<string>? linkHeaders))
                    {
                        nextPageUrl = GetNextPageUrl(linkHeaders.First()); // Extract the URL for the next page
                    }
                    else
                    {
                        nextPageUrl = null!; // No next page
                    }
                }
                else
                {
                    Debug.WriteLine($"Failed to fetch folder contents. Status code: {response.StatusCode}");
                    _downloadComplete = false;
                    return totalSize; // Return the calculated size even if there was an error
                }
            }
            while (!string.IsNullOrEmpty(nextPageUrl)); // Continue until there are no more pages

            return totalSize;
        }
        /// <summary>
        /// Download folder content
        /// </summary>
        /// <param Name="client"></param>
        /// <param Name="apiUrl"></param>
        /// <param Name="_updatePath"></param>
        /// <param Name="progress"></param>
        /// <returns></returns>
        private async Task DownloadFolderContents(HttpClient client, string apiUrl, string downloadDirectory, IProgress<int> progress)
        {
            string nextPageUrl = apiUrl; // Start with the initial URL

            do
            {
                HttpResponseMessage response = await client.GetAsync(nextPageUrl);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    GitHubContent[] contents = JsonSerializer.Deserialize<GitHubContent[]>(json)!;

                    if (!Directory.Exists(downloadDirectory))
                    {
                        Directory.CreateDirectory(downloadDirectory);
                    }

                    List<Task> downloadTasks = new List<Task>();
                    List<Task> subfolderTasks = new List<Task>();

                    foreach (GitHubContent content in contents)
                    {
                        if (content.Type == "file")
                        {
                            string fileUrl = content.DownloadUrl!;
                            string filePath = Path.Combine(downloadDirectory, content.Name!);

                            downloadTasks.Add(DownloadFileAsync(client, fileUrl, filePath, progress));
                        }
                        else if (content.Type == "dir")
                        {
                            string subfolderPath = content.Path!;
                            string subfolderDownloadDirectory = Path.Combine(downloadDirectory, content.Name!);
                            subfolderTasks.Add(DownloadFolderContents(client, apiUrl.Replace(_folderPath, subfolderPath), subfolderDownloadDirectory, progress));
                        }
                    }

                    await Task.WhenAll(downloadTasks);

                    // Check if there's a next page
                    if (response.Headers.TryGetValues("Link", out IEnumerable<string>? linkHeaders))
                    {
                        nextPageUrl = GetNextPageUrl(linkHeaders.First()); // Extract the URL for the next page
                    }
                    else
                    {
                        nextPageUrl = null!; // No next page
                    }

                    await Task.WhenAll(subfolderTasks); // Wait for subfolder downloads to complete
                }
                else
                {
                    Debug.WriteLine($"Failed to fetch folder contents. Status code: {response.StatusCode}");
                    _downloadComplete = false;
                    return;
                }
            }
            while (!string.IsNullOrEmpty(nextPageUrl)); // Continue until there are no more pages
        }
        /// <summary>
        /// Get next paging url
        /// </summary>
        /// <param name="linkHeader"></param>
        /// <returns></returns>
        private string GetNextPageUrl(string linkHeader)
        {
            // Parse the Link header to extract the URL for the next page
            string[] parts = linkHeader.Split(',');
            foreach (string part in parts)
            {
                string[] subparts = part.Split(';');
                if (subparts.Length == 2 && subparts[1].Trim() == "rel=\"next\"")
                {
                    string url = subparts[0].Trim('<', '>');
                    return url;
                }
            }

            return null!; // No next page
        }
        /// <summary>
        /// Download a file async
        /// </summary>
        /// <param name="client"></param>
        /// <param name="fileUrl"></param>
        /// <param name="filePath"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        private async Task DownloadFileAsync(HttpClient client, string fileUrl, string filePath, IProgress<int> progress)
        {
            using (HttpResponseMessage fileResponse = await client.GetAsync(fileUrl))
            {
                if (fileResponse.IsSuccessStatusCode)
                {
                    byte[] bytes = await fileResponse.Content.ReadAsByteArrayAsync();
                    File.WriteAllBytes(filePath, bytes);
                    Debug.WriteLine($"Downloaded {Path.GetFileName(filePath)}");

                    // Increment _downloadedFileSize by the Size of the downloaded file
                    _downloadedFileSize += bytes.Length;

                    // Calculate progress as a percentage of _downloadedFileSize relative to _totalFileSize
                    int percentComplete = (int)(((double)_downloadedFileSize / _totalFileSize) * 100);
                    progress.Report(percentComplete);
                }
                else
                {
                    Debug.WriteLine($"Failed to download {Path.GetFileName(filePath)}");
                    _downloadComplete = false;
                }
            }
        }
        /// <summary>
        /// Raises ProgressUpdatedEvent
        /// </summary>
        /// <param Name="progress">An integer (0-100)</param>
        internal void UpdateProgress(ProgressEventArgs e)
        {
            // Raise the event to notify the subscribers
            ProgressUpdated?.Invoke(this, e);
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
    /// <summary>
    /// EventArgs for progress update
    /// </summary>
    internal class ProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Progress 0-100%
        /// </summary>
        internal int Progress { get; set; }
        /// <summary>
        /// Status, either good or bad
        /// </summary>
        internal string Status { get; set; }
        public ProgressEventArgs(int progress, string status)
        {
            Progress = progress;
            Status = status;
        }
    }
}