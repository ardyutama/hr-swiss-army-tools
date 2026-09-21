namespace hr_sat.Application.Abstractions.Email;

// The dispatch pre-flight's answer (issue 03, decision 20): one readiness concept
// replacing the sync IsConfigured. Password auth checks completeness only (no
// network); Microsoft Account auth performs one silent token acquisition — a dead or
// unreachable grant reads SignInExpired, refusing the run amber instead of failing
// every dispatch in it (decision 7).
public enum SmtpReadiness
{
    Configured,
    NotConfigured,
    SignInExpired,
}
