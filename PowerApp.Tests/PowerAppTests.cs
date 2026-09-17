using System;
using System.Collections.Generic;
using PowerApp.Models;
using PowerApp.Native;
using Xunit;

namespace PowerApp.Tests
{
    public class PowerAppTests
    {
        [Fact]
        public void DevicePathResolver_ResolvesOrPreservesNormalPath()
        {
            string normalPath = @"C:\Windows\System32\cmd.exe";
            string resolved = DevicePathResolver.ResolveToDosPath(normalPath);
            Assert.Equal(normalPath, resolved);
        }

        [Fact]
        public void DevicePathResolver_HandlesNullAndEmpty()
        {
            Assert.Equal(string.Empty, DevicePathResolver.ResolveToDosPath(null));
            Assert.Equal(string.Empty, DevicePathResolver.ResolveToDosPath(""));
        }

        [Fact]
        public void PowerCategoryGroup_HasCorrectDefaultDescriptions()
        {
            string displayDesc = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.Display);
            string systemDesc = PowerCategoryGroup.GetDefaultDescription(PowerRequestTypeInternal.System);

            Assert.Contains("display", displayDesc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("system", systemDesc, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PowerSnapshot_GeneratesValidPowercfgFormat()
        {
            var snapshot = new PowerSnapshot
            {
                Categories = new List<PowerCategoryGroup>
                {
                    new()
                    {
                        Category = PowerRequestTypeInternal.Display,
                        Title = "DISPLAY",
                        Requests = new List<PowerRequestEntry>
                        {
                            new()
                            {
                                Category = PowerRequestTypeInternal.Display,
                                CallerType = RequesterType.UserProcessRequester,
                                Name = @"C:\Program Files\Mozilla Firefox\firefox.exe",
                                Reason = "display request",
                                Count = 1
                            }
                        }
                    },
                    new()
                    {
                        Category = PowerRequestTypeInternal.System,
                        Title = "SYSTEM",
                        Requests = new List<PowerRequestEntry>
                        {
                            new()
                            {
                                Category = PowerRequestTypeInternal.System,
                                CallerType = RequesterType.KernelRequester,
                                Name = "NVIDIA High Definition Audio",
                                Details = @"HDAUDIO\FUNC_01&VEN_10DE",
                                Reason = "An audio stream is currently in use.",
                                Count = 1
                            }
                        }
                    },
                    new()
                    {
                        Category = PowerRequestTypeInternal.AwayMode,
                        Title = "AWAYMODE",
                        Requests = new List<PowerRequestEntry>()
                    }
                }
            };

            string output = snapshot.ToPowercfgOutput();

            Assert.Contains("DISPLAY:", output);
            Assert.Contains("[PROCESS] C:\\Program Files\\Mozilla Firefox\\firefox.exe", output);
            Assert.Contains("display request", output);

            Assert.Contains("SYSTEM:", output);
            Assert.Contains("[DRIVER] NVIDIA High Definition Audio (HDAUDIO\\FUNC_01&VEN_10DE)", output);
            Assert.Contains("An audio stream is currently in use.", output);

            Assert.Contains("AWAYMODE:", output);
            Assert.Contains("None.", output);

            Assert.True(snapshot.HasActiveRequests);
            Assert.Equal(2, snapshot.TotalActiveRequests);
        }

        [Fact]
        public void PowerSnapshot_SummaryText_ReportsCorrectState()
        {
            var idleSnapshot = new PowerSnapshot
            {
                Categories = new List<PowerCategoryGroup>
                {
                    new() { Category = PowerRequestTypeInternal.Display, Title = "DISPLAY" }
                }
            };

            Assert.False(idleSnapshot.HasActiveRequests);
            Assert.Contains("No wake requests", idleSnapshot.SummaryText);

            var activeSnapshot = new PowerSnapshot
            {
                Categories = new List<PowerCategoryGroup>
                {
                    new()
                    {
                        Category = PowerRequestTypeInternal.System,
                        Title = "SYSTEM",
                        Requests = new List<PowerRequestEntry> { new() { Name = "test.exe" } }
                    }
                }
            };

            Assert.True(activeSnapshot.HasActiveRequests);
            Assert.Contains("Wake Requests Active", activeSnapshot.SummaryText);
            Assert.Contains("SYSTEM: 1", activeSnapshot.SummaryText);
        }

        [Fact]
        public void PowerRequestEntry_FormatsDisplayHeaderCorrectly()
        {
            var processEntry = new PowerRequestEntry
            {
                CallerType = RequesterType.UserProcessRequester,
                Name = "chrome.exe",
                ProcessId = 1234
            };
            Assert.Equal("chrome.exe [PID 1234]", processEntry.DisplayHeader);

            var serviceEntry = new PowerRequestEntry
            {
                CallerType = RequesterType.UserSharedServiceRequester,
                Name = "svchost.exe",
                Details = "AudioSrv",
                ProcessId = 5678
            };
            Assert.Equal("svchost.exe (AudioSrv) [PID 5678]", serviceEntry.DisplayHeader);

            var driverEntry = new PowerRequestEntry
            {
                CallerType = RequesterType.KernelRequester,
                Name = "Realtek Audio Driver"
            };
            Assert.Equal("Realtek Audio Driver", driverEntry.DisplayHeader);
        }

        [Fact]
        public void ParsePowerRequestList_ParsesSyntheticBufferCorrectly()
        {
            const int bufferSize = 1024;
            IntPtr pBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(bufferSize);
            try
            {
                // Clear memory
                for (int b = 0; b < bufferSize; b++)
                {
                    System.Runtime.InteropServices.Marshal.WriteByte(pBuffer, b, 0);
                }

                // 1. POWER_REQUEST_LIST
                // Count = 1
                System.Runtime.InteropServices.Marshal.WriteInt64(pBuffer, 0, 1);
                // Offset to first request = 64
                System.Runtime.InteropServices.Marshal.WriteInt64(pBuffer, 8, 64);

                // 2. POWER_REQUEST at offset 64
                // SupportedRequestMask
                System.Runtime.InteropServices.Marshal.WriteInt32(pBuffer, 64, 0x3F);
                // PowerRequestCount[6]
                System.Runtime.InteropServices.Marshal.WriteInt32(pBuffer, 68, 1); // DISPLAY = 1
                System.Runtime.InteropServices.Marshal.WriteInt32(pBuffer, 72, 2); // SYSTEM = 2
                System.Runtime.InteropServices.Marshal.WriteInt32(pBuffer, 76, 0); // AWAYMODE = 0
                System.Runtime.InteropServices.Marshal.WriteInt32(pBuffer, 80, 0); // EXECUTION = 0
                System.Runtime.InteropServices.Marshal.WriteInt32(pBuffer, 84, 0); // PERFBOOST = 0
                System.Runtime.InteropServices.Marshal.WriteInt32(pBuffer, 88, 0); // ACTIVELOCKSCREEN = 0

                // 3. DIAGNOSTIC_BUFFER at offset 64 + 32 = 96
                const int diagOffset = 96;
                System.Runtime.InteropServices.Marshal.WriteInt64(pBuffer, diagOffset + 0, 120); // Size
                System.Runtime.InteropServices.Marshal.WriteInt32(pBuffer, diagOffset + 8, (int)RequesterType.UserProcessRequester); // CallerType = 1

                // ProcessImageNameOffset relative to diagOffset
                const int strProcOffset = 200;
                System.Runtime.InteropServices.Marshal.WriteInt64(pBuffer, diagOffset + 16, strProcOffset - diagOffset);
                System.Runtime.InteropServices.Marshal.WriteInt32(pBuffer, diagOffset + 24, 4321); // PID
                System.Runtime.InteropServices.Marshal.WriteInt32(pBuffer, diagOffset + 28, 0);    // ServiceTag

                // ReasonOffset relative to diagOffset
                const int reasonOffset = 300;
                System.Runtime.InteropServices.Marshal.WriteInt64(pBuffer, diagOffset + 32, reasonOffset - diagOffset);

                // Write Process name at offset 200
                string procName = @"C:\Firefox\firefox.exe";
                byte[] procBytes = System.Text.Encoding.Unicode.GetBytes(procName + "\0");
                System.Runtime.InteropServices.Marshal.Copy(procBytes, 0, IntPtr.Add(pBuffer, strProcOffset), procBytes.Length);

                // 4. COUNTED_REASON_CONTEXT_RELATIVE at offset 300
                // Flags = 1 (POWER_REQUEST_CONTEXT_SIMPLE_STRING)
                System.Runtime.InteropServices.Marshal.WriteInt32(pBuffer, reasonOffset + 0, 1);
                // SimpleStringOffset relative to reasonOffset
                const int reasonStrOffset = 400;
                System.Runtime.InteropServices.Marshal.WriteInt64(pBuffer, reasonOffset + 8, reasonStrOffset - reasonOffset);

                // Write reason string at offset 400
                string reasonText = "Playing YouTube Video";
                byte[] reasonBytes = System.Text.Encoding.Unicode.GetBytes(reasonText + "\0");
                System.Runtime.InteropServices.Marshal.Copy(reasonBytes, 0, IntPtr.Add(pBuffer, reasonStrOffset), reasonBytes.Length);

                // Parse with V4 (supportedModeCount = 6, diagnosticBufferOffset = 32)
                var snapshot = PowerRequestReader.ParsePowerRequestList(pBuffer, bufferSize, 6, 32, isElevated: true);

                Assert.NotNull(snapshot);
                Assert.True(snapshot.HasActiveRequests);
                Assert.Equal(2, snapshot.TotalActiveRequests);

                var displayGroup = snapshot.Categories.Find(c => c.Category == PowerRequestTypeInternal.Display);
                Assert.NotNull(displayGroup);
                Assert.Single(displayGroup.Requests);
                var displayReq = displayGroup.Requests[0];
                Assert.Equal(procName, displayReq.Name);
                Assert.Equal((uint)4321, displayReq.ProcessId);
                Assert.Equal((uint)1, displayReq.Count);
                Assert.Equal(reasonText, displayReq.Reason);
                Assert.Equal(RequesterType.UserProcessRequester, displayReq.CallerType);

                var systemGroup = snapshot.Categories.Find(c => c.Category == PowerRequestTypeInternal.System);
                Assert.NotNull(systemGroup);
                Assert.Single(systemGroup.Requests);
                var systemReq = systemGroup.Requests[0];
                Assert.Equal(procName, systemReq.Name);
                Assert.Equal((uint)4321, systemReq.ProcessId);
                Assert.Equal((uint)2, systemReq.Count);
                Assert.Equal(reasonText, systemReq.Reason);

                var awayGroup = snapshot.Categories.Find(c => c.Category == PowerRequestTypeInternal.AwayMode);
                Assert.NotNull(awayGroup);
                Assert.Empty(awayGroup.Requests);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(pBuffer);
            }
        }
    }
}
