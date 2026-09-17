using System;

namespace PowerApp.Native
{
    public enum PowerRequestTypeInternal : int
    {
        Display = 0,
        System = 1,
        AwayMode = 2,
        Execution = 3,
        PerfBoost = 4,
        ActiveLockScreen = 5
    }

    public enum RequesterType : int
    {
        KernelRequester = 0,
        UserProcessRequester = 1,
        UserSharedServiceRequester = 2
    }

    [Flags]
    public enum DiagnosticReasonFlags : uint
    {
        SimpleString = 0x00000001,
        DetailedString = 0x00000002,
        NotSpecified = 0x80000000
    }
}
