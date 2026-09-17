using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using PowerApp.Models;

namespace PowerApp.Native
{
    public static class PowerRequestReader
    {
        public static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static PowerSnapshot CreateDemoSnapshot()
        {
            return new PowerSnapshot
            {
                Timestamp = DateTime.Now,
                IsElevated = true,
                ErrorMessage = null,
                Categories = new List<PowerCategoryGroup>
                {
                    new()
                    {
                        Category = PowerRequestTypeInternal.Display,
                        Title = "DISPLAY",
                        Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.Display),
                        Requests = new List<PowerRequestEntry>
                        {
                            new()
                            {
                                Category = PowerRequestTypeInternal.Display,
                                CallerType = RequesterType.UserProcessRequester,
                                Name = @"C:\Program Files\Mozilla Firefox\firefox.exe",
                                Details = @"\Device\HarddiskVolume3\Program Files\Mozilla Firefox\firefox.exe",
                                ProcessId = 12548,
                                Count = 1,
                                Reason = "display request"
                            }
                        }
                    },
                    new()
                    {
                        Category = PowerRequestTypeInternal.System,
                        Title = "SYSTEM",
                        Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.System),
                        Requests = new List<PowerRequestEntry>
                        {
                            new()
                            {
                                Category = PowerRequestTypeInternal.System,
                                CallerType = RequesterType.KernelRequester,
                                Name = "NVIDIA High Definition Audio",
                                Details = @"HDAUDIO\FUNC_01&VEN_10DE&DEV_00A4&SUBSYS_196E13BD&REV_1001\5&6069183&0&0001",
                                Count = 1,
                                Reason = "An audio stream is currently in use."
                            }
                        }
                    },
                    new()
                    {
                        Category = PowerRequestTypeInternal.AwayMode,
                        Title = "AWAYMODE",
                        Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.AwayMode),
                        Requests = new List<PowerRequestEntry>()
                    },
                    new()
                    {
                        Category = PowerRequestTypeInternal.Execution,
                        Title = "EXECUTION",
                        Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.Execution),
                        Requests = new List<PowerRequestEntry>
                        {
                            new()
                            {
                                Category = PowerRequestTypeInternal.Execution,
                                CallerType = RequesterType.UserProcessRequester,
                                Name = @"C:\Program Files\Mozilla Firefox\firefox.exe",
                                Details = @"\Device\HarddiskVolume3\Program Files\Mozilla Firefox\firefox.exe",
                                ProcessId = 12548,
                                Count = 1,
                                Reason = "non-display request"
                            }
                        }
                    },
                    new()
                    {
                        Category = PowerRequestTypeInternal.PerfBoost,
                        Title = "PERFBOOST",
                        Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.PerfBoost),
                        Requests = new List<PowerRequestEntry>()
                    },
                    new()
                    {
                        Category = PowerRequestTypeInternal.ActiveLockScreen,
                        Title = "ACTIVELOCKSCREEN",
                        Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.ActiveLockScreen),
                        Requests = new List<PowerRequestEntry>()
                    }
                }
            };
        }

        public static PowerSnapshot QueryPowerRequests()
        {
            var snapshot = new PowerSnapshot
            {
                Timestamp = DateTime.Now,
                IsElevated = IsRunningAsAdministrator()
            };

            // Define the 6 standard categories
            var categories = new List<PowerCategoryGroup>
            {
                new() { Category = PowerRequestTypeInternal.Display, Title = "DISPLAY", Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.Display) },
                new() { Category = PowerRequestTypeInternal.System, Title = "SYSTEM", Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.System) },
                new() { Category = PowerRequestTypeInternal.AwayMode, Title = "AWAYMODE", Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.AwayMode) },
                new() { Category = PowerRequestTypeInternal.Execution, Title = "EXECUTION", Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.Execution) },
                new() { Category = PowerRequestTypeInternal.PerfBoost, Title = "PERFBOOST", Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.PerfBoost) },
                new() { Category = PowerRequestTypeInternal.ActiveLockScreen, Title = "ACTIVELOCKSCREEN", Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.ActiveLockScreen) },
            };

            snapshot.Categories = categories;

            // Determine supported mode count and DiagnosticBuffer offset based on OS version
            int major = Environment.OSVersion.Version.Major;
            int minor = Environment.OSVersion.Version.Minor;
            int build = Environment.OSVersion.Version.Build;

            int supportedModeCount;
            int diagnosticBufferOffset;

            if (major > 10 || (major == 10 && (minor > 0 || build >= 14393)))
            {
                // Windows 10 RS1+ / Windows 11 (V4)
                supportedModeCount = 6;
                diagnosticBufferOffset = 32;
            }
            else if (major > 6 || (major == 6 && minor >= 3))
            {
                // Windows 8.1 / Win 10 TH1-TH2 (V3)
                supportedModeCount = 5;
                diagnosticBufferOffset = 24;
            }
            else if (major == 6 && minor == 2)
            {
                // Windows 8 (V2)
                supportedModeCount = 9;
                diagnosticBufferOffset = 40;
            }
            else
            {
                // Windows 7 (V1)
                supportedModeCount = 3;
                diagnosticBufferOffset = 16;
            }

            int bufferSize = 4096;
            IntPtr pBuffer = IntPtr.Zero;

            try
            {
                int status;
                while (true)
                {
                    pBuffer = Marshal.AllocHGlobal(bufferSize);
                    status = NativeMethods.NtPowerInformation(
                        NativeMethods.PowerInformationLevel_GetPowerRequestList,
                        IntPtr.Zero,
                        0,
                        pBuffer,
                        (uint)bufferSize);

                    if (status == NativeMethods.STATUS_SUCCESS)
                    {
                        break;
                    }

                    Marshal.FreeHGlobal(pBuffer);
                    pBuffer = IntPtr.Zero;

                    if (status == NativeMethods.STATUS_BUFFER_TOO_SMALL)
                    {
                        bufferSize += 4096;
                        if (bufferSize > 10 * 1024 * 1024)
                        {
                            snapshot.ErrorMessage = "Buffer size exceeded 10MB safety limit.";
                            return snapshot;
                        }
                        continue;
                    }

                    if (status == NativeMethods.STATUS_ACCESS_DENIED)
                    {
                        snapshot.ErrorMessage = "Administrator privileges are required to query power requests.";
                        return snapshot;
                    }

                    snapshot.ErrorMessage = $"NtPowerInformation failed with NTSTATUS 0x{status:X8}.";
                    return snapshot;
                }

                return ParsePowerRequestList(pBuffer, bufferSize, supportedModeCount, diagnosticBufferOffset, snapshot.IsElevated);
            }
            catch (Exception ex)
            {
                snapshot.ErrorMessage = ex.Message;
                return snapshot;
            }
            finally
            {
                if (pBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pBuffer);
                }
            }
        }

        internal static PowerSnapshot ParsePowerRequestList(
            IntPtr pBuffer,
            int bufferSize,
            int supportedModeCount,
            int diagnosticBufferOffset,
            bool isElevated)
        {
            var snapshot = new PowerSnapshot
            {
                Timestamp = DateTime.Now,
                IsElevated = isElevated
            };

            var categories = new List<PowerCategoryGroup>
            {
                new() { Category = PowerRequestTypeInternal.Display, Title = "DISPLAY", Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.Display) },
                new() { Category = PowerRequestTypeInternal.System, Title = "SYSTEM", Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.System) },
                new() { Category = PowerRequestTypeInternal.AwayMode, Title = "AWAYMODE", Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.AwayMode) },
                new() { Category = PowerRequestTypeInternal.Execution, Title = "EXECUTION", Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.Execution) },
                new() { Category = PowerRequestTypeInternal.PerfBoost, Title = "PERFBOOST", Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.PerfBoost) },
                new() { Category = PowerRequestTypeInternal.ActiveLockScreen, Title = "ACTIVELOCKSCREEN", Description = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.ActiveLockScreen) },
            };

            snapshot.Categories = categories;

            if (pBuffer == IntPtr.Zero || bufferSize < 8)
                return snapshot;

            // Buffer successfully retrieved, parse entries
            long count = Marshal.ReadInt64(pBuffer, 0);

            for (int i = 0; i < count; i++)
            {
                long reqOffset = Marshal.ReadInt64(pBuffer, 8 + i * 8);
                if (reqOffset <= 0 || reqOffset >= bufferSize)
                    continue;

                IntPtr pReq = IntPtr.Add(pBuffer, (int)reqOffset);

                // Read power request counts for each mode
                uint[] modeCounts = new uint[supportedModeCount];
                for (int m = 0; m < supportedModeCount; m++)
                {
                    modeCounts[m] = (uint)Marshal.ReadInt32(pReq, 4 + m * 4);
                }

                IntPtr pDiag = IntPtr.Add(pReq, diagnosticBufferOffset);
                int callerTypeVal = Marshal.ReadInt32(pDiag, 8);
                var callerType = (RequesterType)callerTypeVal;

                string requesterName = "Unknown";
                string requesterDetails = string.Empty;
                uint? pid = null;
                uint serviceTag = 0;

                if (callerType == RequesterType.KernelRequester)
                {
                    long devDescOffset = Marshal.ReadInt64(pDiag, 16);
                    long devPathOffset = Marshal.ReadInt64(pDiag, 24);

                    if (devDescOffset != 0)
                    {
                        IntPtr pStr = IntPtr.Add(pDiag, (int)devDescOffset);
                        requesterName = Marshal.PtrToStringUni(pStr) ?? "Legacy Kernel Caller";
                    }
                    else
                    {
                        requesterName = "Legacy Kernel Caller";
                    }

                    if (devPathOffset != 0)
                    {
                        IntPtr pStr = IntPtr.Add(pDiag, (int)devPathOffset);
                        requesterDetails = Marshal.PtrToStringUni(pStr) ?? string.Empty;
                    }
                }
                else if (callerType == RequesterType.UserProcessRequester || callerType == RequesterType.UserSharedServiceRequester)
                {
                    long procImageOffset = Marshal.ReadInt64(pDiag, 16);
                    pid = (uint)Marshal.ReadInt32(pDiag, 24);
                    serviceTag = (uint)Marshal.ReadInt32(pDiag, 28);

                    if (procImageOffset != 0)
                    {
                        IntPtr pStr = IntPtr.Add(pDiag, (int)procImageOffset);
                        string rawPath = Marshal.PtrToStringUni(pStr) ?? "Process";
                        requesterName = DevicePathResolver.ResolveToDosPath(rawPath);
                    }

                    if (callerType == RequesterType.UserSharedServiceRequester && pid.HasValue)
                    {
                        string svcName = ResolveServiceTag(pid.Value, serviceTag);
                        if (!string.IsNullOrEmpty(svcName))
                        {
                            requesterDetails = svcName;
                        }
                    }
                }

                // Parse diagnostic reason
                string reasonText = string.Empty;
                long reasonOffset = Marshal.ReadInt64(pDiag, 32);
                if (reasonOffset != 0)
                {
                    IntPtr pReason = IntPtr.Add(pDiag, (int)reasonOffset);
                    uint flags = (uint)Marshal.ReadInt32(pReason, 0);

                    if ((flags & (uint)DiagnosticReasonFlags.SimpleString) != 0)
                    {
                        long simpleStrOffset = Marshal.ReadInt64(pReason, 8);
                        if (simpleStrOffset != 0)
                        {
                            IntPtr pStr = IntPtr.Add(pReason, (int)simpleStrOffset);
                            reasonText = Marshal.PtrToStringUni(pStr) ?? string.Empty;
                        }
                    }
                    else if ((flags & (uint)DiagnosticReasonFlags.DetailedString) != 0)
                    {
                        long resFileOffset = Marshal.ReadInt64(pReason, 8);
                        ushort resId = (ushort)Marshal.ReadInt16(pReason, 16);
                        if (resFileOffset != 0)
                        {
                            IntPtr pResFile = IntPtr.Add(pReason, (int)resFileOffset);
                            string rawResFile = Marshal.PtrToStringUni(pResFile) ?? string.Empty;
                            reasonText = LoadResourceReason(rawResFile, resId);
                        }
                    }
                }

                // For each supported mode where count > 0, create an entry under that category
                int maxCategories = Math.Min(supportedModeCount, categories.Count);
                for (int m = 0; m < maxCategories; m++)
                {
                    if (modeCounts[m] > 0)
                    {
                        var entry = new PowerRequestEntry
                        {
                            Category = (PowerRequestTypeInternal)m,
                            CallerType = callerType,
                            Name = requesterName,
                            Details = requesterDetails,
                            ProcessId = pid,
                            ServiceTag = serviceTag,
                            Count = modeCounts[m],
                            Reason = reasonText
                        };
                        categories[m].Requests.Add(entry);
                    }
                }
            }

            return snapshot;
        }

        private static string LoadResourceReason(string resourceFileName, ushort messageId)
        {
            if (string.IsNullOrEmpty(resourceFileName))
                return string.Empty;

            try
            {
                string expandedPath = Environment.ExpandEnvironmentVariables(resourceFileName);
                expandedPath = DevicePathResolver.ResolveToDosPath(expandedPath);

                IntPtr hModule = NativeMethods.LoadLibraryExW(
                    expandedPath,
                    IntPtr.Zero,
                    NativeMethods.LOAD_LIBRARY_AS_DATAFILE | NativeMethods.LOAD_LIBRARY_AS_IMAGE_RESOURCE);

                if (hModule == IntPtr.Zero)
                    return string.Empty;

                try
                {
                    var sb = new StringBuilder(1024);
                    int len = NativeMethods.LoadStringW(hModule, messageId, sb, sb.Capacity);
                    if (len > 0)
                    {
                        return sb.ToString().Trim();
                    }
                }
                finally
                {
                    NativeMethods.FreeLibrary(hModule);
                }
            }
            catch
            {
                // Ignore load failures
            }

            return string.Empty;
        }

        private static string ResolveServiceTag(uint pid, uint tag)
        {
            if (tag == 0)
                return string.Empty;

            try
            {
                var info = new NativeMethods.TagInfoNameFromTag
                {
                    InPid = pid,
                    InTag = tag,
                    OutName = IntPtr.Zero
                };

                uint result = NativeMethods.I_QueryTagInformation(IntPtr.Zero, 1, ref info);
                if (result == 0 && info.OutName != IntPtr.Zero)
                {
                    try
                    {
                        return Marshal.PtrToStringUni(info.OutName) ?? string.Empty;
                    }
                    finally
                    {
                        NativeMethods.LocalFree(info.OutName);
                    }
                }
            }
            catch
            {
                // In case advapi32 query fails
            }

            return string.Empty;
        }
    }
}
