using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Supershow.Services;

public class SecurityService
{
    private readonly IHttpContextAccessor ct;

    public SecurityService(IHttpContextAccessor ct)
    {
        this.ct = ct;
    }

    private readonly PasswordHasher<object> ph = new();

    public string HashPassword(string password)
    {
        return ph.HashPassword(0, password);
    }

    public bool VerifyPassword(string hash, string password)
    {
        return ph.VerifyHashedPassword(0, hash, password) == PasswordVerificationResult.Success;
    }

    public void SignIn(string accountId, string role, string sessionToken)
    {
        // Claim, identity and principal
        List<Claim> claims = [
            new(ClaimTypes.Name, accountId),
            new(ClaimTypes.Role, role),
            new("SessionToken", sessionToken)
        ];

        ClaimsIdentity identity = new(claims, "Cookies");

        ClaimsPrincipal principal = new(identity);

        // Remember me
        AuthenticationProperties properties = new()
        {
            IsPersistent = true
        };

        // Sign in
        ct.HttpContext!.SignInAsync(principal, properties);
    }

    public void SignOut()
    {
        // Sign out
        ct.HttpContext!.SignOutAsync();
    }
}