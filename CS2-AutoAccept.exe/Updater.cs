using System;
using CS2_AutoAccept;
using System.Diagnostics;

namespace CS2AutoAccept
{
    internal class Updater
    {
        private readonly string _repositoryOwner = "tsgsOFFICIAL";
        private readonly string _repositoryName = "CS2-AutoAccept.exe";
        private readonly string _folderPath = "CS2-AutoAccept.exe/bin/Release/net6.0-windows/publish/win-x86";
        public event EventHandler<ProgressEventArgs>? DownloadProgress;

        /// <summary>
        /// Download the update
        /// </summary>
        /// <param name="downloadDirectory">Where to download to locally</param>
        internal async void DownloadUpdate(string downloadDirectory)
        {
            using (GitHubDirectoryDownloader downloader = new GitHubDirectoryDownloader(_repositoryOwner, _repositoryName, _folderPath))
            {
                downloader.ProgressUpdated += OnProgressChanged!;

                await downloader.DownloadDirectoryAsync(downloadDirectory);
            }
        }
        /// <summary>
        /// Raises the ProgressChanged event.
        /// </summary>
        /// <param name="e">An instance of ProgressEventArgs, can hold Progress as an int (0-100).</param>
        protected virtual void OnProgressChanged(object sender, ProgressEventArgs e)
        {
            DownloadProgress?.Invoke(this, e);
        }
    }
}