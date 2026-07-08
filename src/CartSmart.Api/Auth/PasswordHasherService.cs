using CartSmart.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace CartSmart.Api.Auth;

public interface IPasswordHasherService
{
    string HashPassword(string password);
    bool VerifyPassword(string hash, string password);
}

public class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(string password) => _hasher.HashPassword(null!, password);

    public bool VerifyPassword(string hash, string password) =>
        _hasher.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;
}
