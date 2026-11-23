namespace Monitor.Features.Authentication;

public class AdminAuthenticationOptions
{
  public string Username { get; set; } = "admin";
  public string PasswordHash { get; set; } = "";
}