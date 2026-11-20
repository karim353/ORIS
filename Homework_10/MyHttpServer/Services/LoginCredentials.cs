using System.Security.Cryptography;

namespace MyHttpServer;

public sealed class LoginCredentials
{
    private readonly string email;
    private readonly byte[] salt = RandomNumberGenerator.GetBytes(16);
    private readonly byte[] hash;

    public LoginCredentials(string email, string password)
    {
        this.email = email.Trim();
        hash = Digest(password);
    }

    public bool Matches(string suppliedEmail, string suppliedPassword)
    {
        if (suppliedPassword.Length > 128)
            return false;
        bool correct = CryptographicOperations.FixedTimeEquals(hash, Digest(suppliedPassword));
        return correct && email.Equals(suppliedEmail.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static LoginCredentials? FromEnvironment()
    {
        string? email = Environment.GetEnvironmentVariable("ORIS_LOGIN");
        string? password = Environment.GetEnvironmentVariable("ORIS_PASSWORD");
        return !string.IsNullOrWhiteSpace(email) && password is { Length: >= 12 and <= 128 }
            ? new LoginCredentials(email, password)
            : null;
    }

    private byte[] Digest(string text) =>
        Rfc2898DeriveBytes.Pbkdf2(text, salt, 120_000, HashAlgorithmName.SHA256, 32);
}
