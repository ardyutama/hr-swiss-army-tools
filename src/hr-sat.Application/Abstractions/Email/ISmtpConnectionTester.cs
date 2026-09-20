namespace hr_sat.Application.Abstractions.Email;

// The connect + authenticate handshake behind the settings page's Test connection
// button (issue 02, decision 3). Tests the values it is given and persists nothing.
public interface ISmtpConnectionTester
{
    Task<SmtpTestResult> TestAsync(SmtpTestRequest request, CancellationToken cancellationToken);
}
