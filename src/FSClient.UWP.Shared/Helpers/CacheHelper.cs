namespace FSClient.UWP.Shared.Helpers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Storage;

    public static class CacheHelper
    {
        private static Timer? cleanCacheTimer;
        private static bool isCleaning;

        public static bool IsAutoCleanPaused { get; set; }

        public static void StartAutoClean(TimeSpan? cleanInterval = null)
        {
            var interval = cleanInterval ?? TimeSpan.FromMinutes(10);

            if (cleanCacheTimer != null)
            {
                cleanCacheTimer.Change(interval, interval);
            }
            else
            {
                cleanCacheTimer = new Timer(
                    state => _ = !IsAutoCleanPaused && ClearCache(true), 
                    null, interval, interval);
            }
        }

        public static void StopAutoClean()
        {
            if (cleanCacheTimer != null)
            {
                cleanCacheTimer.Dispose();
                cleanCacheTimer = null;
            }
        }

        public static Task<bool> ClearCacheAsync(bool forceFolderClearing)
        {
            if (isCleaning)
            {
                return Task.FromResult(true);
            }

            return Task.Run(() => ClearCache(forceFolderClearing));
        }

        private static bool ClearCache(bool forceFolderClearing)
        {
            return !isCleaning
                   && DeleteLocalFolder("AC\\INetCache", forceFolderClearing)
                   && DeleteLocalFolder("AC\\INetHistory", forceFolderClearing);
        }

        private static bool DeleteLocalFolder(string folder, bool forceFolderClearing)
        {
            isCleaning = true;
            try
            {
                var localDirectory = ApplicationData.Current.LocalFolder;
                var iNetCacheDir = Path.GetFullPath(localDirectory.Path + "\\..\\" + folder);

                if (!Directory.Exists(iNetCacheDir))
                {
                    return true;
                }

                // We can't delete system folders, when application is in active state
                if (forceFolderClearing)
                {
                    ClearCacheFolder(iNetCacheDir);
                }
                else
                {
                    try
                    {
                        Directory.Delete(iNetCacheDir, true);
                    }
                    catch
                    {
                        ClearCacheFolder(iNetCacheDir);
                    }
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                isCleaning = false;
            }

            return true;

            static void ClearCacheFolder(string dirPath)
            {
                foreach (var dir in Directory.GetDirectories(dirPath))
                {
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }
    }
}
