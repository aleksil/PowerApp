using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace PowerApp.Native
{
    public static class DevicePathResolver
    {
        private static readonly ConcurrentDictionary<string, string> DeviceMap = new(StringComparer.OrdinalIgnoreCase);
        private static DateTime _lastRefresh = DateTime.MinValue;

        public static void RefreshDriveMappings()
        {
            DeviceMap.Clear();
            var drives = DriveInfo.GetDrives();
            var targetPath = new StringBuilder(512);

            foreach (var drive in drives)
            {
                var driveLetter = drive.Name.TrimEnd('\\'); // e.g. "C:"
                targetPath.Clear();
                uint result = NativeMethods.QueryDosDeviceW(driveLetter, targetPath, (uint)targetPath.Capacity);
                if (result != 0)
                {
                    string ntPath = targetPath.ToString(); // e.g. "\Device\HarddiskVolume3"
                    DeviceMap[ntPath] = driveLetter;
                }
            }
            _lastRefresh = DateTime.UtcNow;
        }

        public static string ResolveToDosPath(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            if (!path.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase))
                return path;

            if (DateTime.UtcNow - _lastRefresh > TimeSpan.FromMinutes(5) || DeviceMap.IsEmpty)
            {
                RefreshDriveMappings();
            }

            foreach (var kvp in DeviceMap)
            {
                if (path.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value + path.Substring(kvp.Key.Length);
                }
            }

            return path;
        }
    }
}
