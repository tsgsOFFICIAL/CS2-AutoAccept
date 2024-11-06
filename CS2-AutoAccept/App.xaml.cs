using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Windows;
using System.IO;
using System;

namespace CS2_AutoAccept
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Check if another instance of the app is already running
            Mutex mutex = new Mutex(true, "CS2-AutoAccept by tsgsOFFICIAL", out bool createdNew);
            string[] args = Environment.GetCommandLineArgs();
            bool updated = false;

            foreach (string arg in args)
            {
                // Application was updated
                if (arg.ToLower().Equals("--updated"))
                {
                    updated = true;
                    break;
                }
            }

            // If another instance exists, trigger the event and exit
            if (!createdNew && !updated)
            {
                // Create a MemoryMappedFile to notify the other instance
                using (MemoryMappedFile mmf = MemoryMappedFile.CreateOrOpen("CS2_AutoAccept_MMF", 1024))
                using (MemoryMappedViewStream view = mmf.CreateViewStream())
                {
                    BinaryWriter writer = new BinaryWriter(view);
                    EventWaitHandle signal = new EventWaitHandle(false, EventResetMode.AutoReset, "CS2_AutoAccept_Event");
                    writer.Write("New instance started");
                    signal.Set(); // Signal the other instance that it should come to the front
                }

                // Shutdown the second instance
                Current.Shutdown();
                mutex.ReleaseMutex();
                return;
            }

            // Run the WPF application
            base.OnStartup(e);
        }
    }
}