using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CS2_AutoAccept
{
    /// <summary>
    /// Downloads a directory from a GitHub repository with support for async operations, progress reporting, and proper disposal.
    /// </summary>
    public class GitHubDirectoryDownloader : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _repositoryOwner;
        private readonly string _repositoryName;
        private readonly string _folderPath;
        private long _totalFileSize = 0;
        private long _downloadedFileSize = 0;
        private object _lockTotalSize = new object();
        private object _lockDownloadedSize = new object();
        private List<Task> _downloadTasks;
        private List<Task> _subfolderTasks;
        public event EventHandler<ProgressEventArgs>? ProgressUpdated;

        /// <summary>
        /// Initializes a new instance of the GitHubDirectoryDownloader class.
        /// </summary>
        /// <param name="repositoryOwner">Repository Owner</param>
        /// <param name="repositoryName">Repository Name</param>
        /// <param name="folderPath">Folder path in repository</param>
        public GitHubDirectoryDownloader(string repositoryOwner, string repositoryName, string folderPath)
        {
            #region HttpClient Settings
            //specify to use TLS 1.2 as default connection
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.AllowAutoRedirect = true;
            // Set the SSL/TLS version (for example, TLS 1.2)
            httpClientHandler.SslProtocols = SslProtocols.Tls12;
            //httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
            //{
            //    return true;
            //};

            _httpClient = new HttpClient(httpClientHandler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "GitHubDirectoryDownloader");
            #endregion

            _repositoryOwner = repositoryOwner;
            _repositoryName = repositoryName;
            _folderPath = folderPath;
            _downloadTasks = new List<Task>();
            _subfolderTasks = new List<Task>();
        }
        /// <summary>
        /// Starts a Task that downloads a GitHub directory asynchronously
        /// </summary>
        /// <param name="downloadPath">Where to download the directory to</param>
        /// <returns>This method returns a Task, meaning it's awaitable</returns>
        public async Task DownloadDirectoryAsync(string downloadPath)
        {
            await StartDownloadAsync(downloadPath);

            Debug.WriteLine("waiting for tasks to complete");
            await Task.WhenAll(_downloadTasks);
            await Task.WhenAll(_subfolderTasks);
        }
        /// <summary>
        /// Downloads a GitHub directory asynchronously
        /// </summary>
        /// <param name="downloadPath">Where to download the directory to</param>
        /// <returns>This method returns a Task, meaning it's awaitable</returns>
        private async Task StartDownloadAsync(string downloadPath, string apiUrl = null!)
        {
            // Construct the API url
            apiUrl ??= $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/contents/{_folderPath}";

            HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
            string responseMessage = await response.Content.ReadAsStringAsync();

            // Rate limit was reached.
            if (responseMessage.Contains("API rate limit exceeded"))
            {
                OnProgressChanged(new ProgressEventArgs(0, "API rate limit exceeded"));
                Dispose();
                return;
            }

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                GitHubContent[] contents = JsonSerializer.Deserialize<GitHubContent[]>(json) ?? Array.Empty<GitHubContent>();

                if (!Directory.Exists(downloadPath))
                {
                    Directory.CreateDirectory(downloadPath);
                }

                lock (_lockTotalSize)
                {
                    _totalFileSize += contents.Sum(content => content.Size ?? 0);
                }

                foreach (GitHubContent item in contents)
                {
                    switch (item.Type)
                    {
                        case "file":
                            string downloadUrl = item.DownloadUrl!;
                            string localFilePath = Path.Combine(downloadPath, item.Name!);

                            lock (_downloadTasks)
                            {
                                _downloadTasks.Add(DownloadFileAsync(downloadUrl!, localFilePath));
                            }
                            break;
                        case "dir":
                            string subFolderPath = item.Path!;
                            string subfolderDownloadDirectory = Path.Combine(downloadPath, item.Name!);

                            lock (_subfolderTasks)
                            {
                                _subfolderTasks.Add(StartDownloadAsync(subfolderDownloadDirectory, apiUrl.Replace(_folderPath, subFolderPath)));
                            }
                            break;
                    }
                }
            }
            else
            {
                OnProgressChanged(new ProgressEventArgs(0, "Something went wrong"));
                Dispose();
            }
        }
        /// <summary>
        /// Downloads a file from a URL and saves it to a local file path.
        /// </summary>
        /// <param name="downloadUrl">The URL of the file to download.</param>
        /// <param name="filePath">The local file path where the downloaded file will be saved.</param>
        private async Task DownloadFileAsync(string downloadUrl, string filePath)
        {
            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(downloadUrl);

                if (response.IsSuccessStatusCode)
                {
                    using FileStream fileStream = File.Create(filePath);
                    await response.Content.CopyToAsync(fileStream);

                    lock (_lockDownloadedSize)
                    {
                        _downloadedFileSize += fileStream.Length;
                        OnProgressChanged(new ProgressEventArgs((int)(((double)_downloadedFileSize / _totalFileSize) * 100)));
                    }
                }
                else
                {
                    Debug.WriteLine($"Failed to download a file: {response.Content}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                OnProgressChanged(new ProgressEventArgs(0, ex.Message));
                Dispose();
            }
        }
        /// <summary>
        /// Raises the ProgressChanged event.
        /// </summary>
        /// <param name="e">An instance of ProgressEventArgs, can hold Progress as an int (0-100).</param>
        protected virtual void OnProgressChanged(ProgressEventArgs e)
        {
            ProgressUpdated?.Invoke(this, e);
        }
        /// <summary>
        /// Disposes of the GitHubDirectoryDownloader instance.
        /// </summary>
        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
    /// <summary>
    /// EventArgs for progress update
    /// </summary>
    public class ProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Progress 0-100%
        /// </summary>
        public int Progress { get; set; }
        /// <summary>
        /// Status, either good or bad
        /// </summary>
        public string Status { get; set; }
        public ProgressEventArgs(int progress, string status = "")
        {
            Progress = progress;
            Status = status;
        }
    }
    /// <summary>
    /// Represents a GitHub repository content item.
    /// </summary>
    public class GitHubContent
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("path")]
        public string? Path { get; set; }
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
        [JsonPropertyName("size")]
        public int? Size { get; set; }
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
        [JsonPropertyName("git_url")]
        public string? GitUrl { get; set; }
        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        [JsonPropertyName("_links")]
        public GitHubContentLinks? Links { get; set; }

        /// <summary>
        /// Represents links inside of GitHub content.
        /// </summary>
        public class GitHubContentLinks
        {
            [JsonPropertyName("self")]
            public string? Self { get; set; }
            [JsonPropertyName("git")]
            public string? Git { get; set; }
            [JsonPropertyName("html")]
            public string? Html { get; set; }
        }
    }
}