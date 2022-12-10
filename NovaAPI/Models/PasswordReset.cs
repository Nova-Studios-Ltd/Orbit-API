namespace NovaAPI.Models;

public class PasswordReset
{
    public string Password { get; set; }
    public UserKey Key { get; set; }
}