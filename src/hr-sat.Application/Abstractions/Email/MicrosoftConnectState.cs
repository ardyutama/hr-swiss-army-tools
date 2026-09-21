namespace hr_sat.Application.Abstractions.Email;

// The connect attempt state (issue 03, decision 21): Idle when no attempt exists
// (first visit, process restart); Pending while the user signs in; the terminal
// states hold until the next begin so a late poll still sees the outcome.
public enum MicrosoftConnectState
{
    Idle,
    Pending,
    Succeeded,
    Failed,
    Expired,
}

public static class MicrosoftConnectStateExtensions
{
    public static string ToApiValue(this MicrosoftConnectState state) => state switch
    {
        MicrosoftConnectState.Idle => "idle",
        MicrosoftConnectState.Pending => "pending",
        MicrosoftConnectState.Succeeded => "succeeded",
        MicrosoftConnectState.Failed => "failed",
        MicrosoftConnectState.Expired => "expired",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown connect state."),
    };
}
