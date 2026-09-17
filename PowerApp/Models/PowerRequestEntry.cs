using System;
using PowerApp.Native;

namespace PowerApp.Models
{
    public class PowerRequestEntry
    {
        public PowerRequestTypeInternal Category { get; set; }
        public RequesterType CallerType { get; set; }
        public string RequesterTypeName => CallerType switch
        {
            RequesterType.KernelRequester => "DRIVER",
            RequesterType.UserProcessRequester => "PROCESS",
            RequesterType.UserSharedServiceRequester => "SERVICE",
            _ => "UNKNOWN"
        };

        public string Name { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string DisplayDetails => string.IsNullOrWhiteSpace(Details) ? string.Empty : Details;
        public uint? ProcessId { get; set; }
        public uint ServiceTag { get; set; }
        public uint Count { get; set; } = 1;
        public string Reason { get; set; } = string.Empty;

        public string DisplayHeader
        {
            get
            {
                if (CallerType == RequesterType.KernelRequester)
                {
                    return !string.IsNullOrEmpty(Name) ? Name : "Kernel Driver";
                }
                else if (CallerType == RequesterType.UserSharedServiceRequester)
                {
                    var svcName = !string.IsNullOrEmpty(Details) ? $" ({Details})" : "";
                    var pidText = ProcessId.HasValue ? $" [PID {ProcessId.Value}]" : "";
                    return $"{Name}{svcName}{pidText}";
                }
                else
                {
                    var pidText = ProcessId.HasValue ? $" [PID {ProcessId.Value}]" : "";
                    return $"{Name}{pidText}";
                }
            }
        }
    }
}
