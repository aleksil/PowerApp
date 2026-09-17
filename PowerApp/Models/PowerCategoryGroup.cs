using System.Collections.Generic;
using PowerApp.Native;

namespace PowerApp.Models
{
    public class PowerCategoryGroup
    {
        public PowerRequestTypeInternal Category { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<PowerRequestEntry> Requests { get; set; } = new();

        public int ActiveCount => Requests.Count;
        public bool HasActiveRequests => Requests.Count > 0;

        public static string GetDefaultDescription(PowerRequestTypeInternal category) => category switch
        {
            PowerRequestTypeInternal.Display => "Prevents the display from dimming or turning off",
            PowerRequestTypeInternal.System => "Prevents the system from sleeping or shutting down",
            PowerRequestTypeInternal.AwayMode => "Allows background tasks while system appears asleep",
            PowerRequestTypeInternal.Execution => "Overrides Process Lifetime Management (PLM) suspension",
            PowerRequestTypeInternal.PerfBoost => "Requests system performance boost",
            PowerRequestTypeInternal.ActiveLockScreen => "Prevents system sleep while on lock screen",
            _ => "Power request"
        };
    }
}
