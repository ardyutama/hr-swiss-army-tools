namespace hr_sat.Application.Abstractions.Email;

// The values the connection probe tests. A null Password falls back to the stored
// row's password (the upsert's keep-semantics applied to testing, issue 02 decision
// 3's "test the current values" when the write-only field was left untouched).
public sealed record SmtpTestRequest(string Host, int Port, string Username, string? Password);
