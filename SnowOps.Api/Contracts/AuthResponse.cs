namespace SnowOps.Api.Contracts;

public sealed class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public UserDto User { get; set; } = new();
}