namespace NovaAPI.Util;

public static class EmailConfig
{
    // Basic settings
    public static bool VerifyEmail { get; set; }
    public static bool PasswordReset { get; set; }
    
    // Outbound email settings
    public static int SMTPPort { get; set; }
    public static string SMTPHost { get; set; }
    public static string FromAddress { get; set; }
    public static string Username { get; set; }
    public static string Password { get; set; }
}