namespace SnowOps.Api.Contracts;

public sealed class RegisterUserRequest
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}