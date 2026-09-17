using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PowerApp.Native
{
    public static class NativeMethods
    {
        public const int PowerInformationLevel_GetPowerRequestList = 45;

        public const int STATUS_SUCCESS = 0x00000000;
        public const int STATUS_BUFFER_TOO_SMALL = unchecked((int)0xC0000023);
        public const int STATUS_ACCESS_DENIED = unchecked((int)0xC0000022);

        public const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        public const uint LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020;

        [DllImport("ntdll.dll", SetLastError = false)]
        public static extern int NtPowerInformation(
            int informationLevel,
            IntPtr inputBuffer,
            uint inputBufferLength,
            IntPtr outputBuffer,
            uint outputBufferLength);

        [StructLayout(LayoutKind.Sequential)]
        public struct TagInfoNameFromTag
        {
            public uint InPid;
            public uint InTag;
            public IntPtr OutName;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern uint I_QueryTagInformation(
            IntPtr reserved,
            int infoLevel,
            ref TagInfoNameFromTag tagInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadLibraryExW(string lpLibFileName, IntPtr hFile, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int LoadStringW(IntPtr hInstance, uint uID, StringBuilder lpBuffer, int nBufferMax);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hLibModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint QueryDosDeviceW(string? lpDeviceName, StringBuilder lpTargetPath, uint ucchMax);
    }
}
