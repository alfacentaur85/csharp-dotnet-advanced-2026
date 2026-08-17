using System.Security.Cryptography;
using System.Text;
using EventService.Application.Interfaces;

namespace EventService.Infrastructure.Security;

public sealed class Sha256PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    public bool Verify(string password, string passwordHash)
        => Hash(password) == passwordHash;
}
