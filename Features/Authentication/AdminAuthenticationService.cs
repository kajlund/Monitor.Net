using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Monitor.Features.Authentication;

public class AdminAuthenticationService(
  IOptions<AdminAuthenticationOptions> options)
{
  /* public string ConfiguredUsername => _options.Username;

  public bool IsConfigured =>
    !string.IsNullOrWhiteSpace(_options.PasswordHash);
 */
  private readonly AdminAuthenticationOptions _options = options.Value;
  private readonly PasswordHasher<string> _hasher = new();

  public bool ValidateCredentials(string username, string password)
  {
    if (string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrEmpty(password) ||
        string.IsNullOrWhiteSpace(_options.PasswordHash))
    {
      return false;
    }

    if (!string.Equals(
          username,
          _options.Username,
          StringComparison.Ordinal))
    {
      return false;
    }

    try
    {
      var result = _hasher.VerifyHashedPassword(
        _options.Username,
        _options.PasswordHash,
        password);

      return result != PasswordVerificationResult.Failed;
    }
    catch (FormatException)
    {
      return false;
    }
  }
}