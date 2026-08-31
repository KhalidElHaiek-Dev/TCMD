namespace TCMD.Api.Authentication;

public sealed record LoginRequest(string? UserName, string? Password);
