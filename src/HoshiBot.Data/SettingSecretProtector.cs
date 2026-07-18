using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace HoshiBot.Data;

// Transparently encrypts secret-typed feature settings (today: the per-guild Gemini API key) at
// rest with AES-256-GCM, so a DB dump doesn't leak live third-party credentials. The symmetric key
// comes from config — Secrets:EncryptionKey, injected via env/user-secrets exactly like
// Discord:Token — as any string; it's SHA-256'd to a 256-bit key. Keep it STABLE: changing it makes
// every existing encrypted value undecryptable.
//
// Stored form is "enc:v1:" + base64(nonce ‖ tag ‖ ciphertext). A value without that prefix is
// treated as legacy plaintext and returned as-is, so keys stored before encryption existed keep
// working (and get upgraded to ciphertext on the next read/write). When no key is configured,
// Protect is a passthrough — dev environments work without the secret; encryption is a deployment
// concern. Reused for any future per-guild secret, not just the AI-chat key.
public sealed class SettingSecretProtector
{
    private const string Prefix = "enc:v1:";
    private const int NonceSize = 12; // AES-GCM standard nonce
    private const int TagSize = 16;   // AES-GCM authentication tag

    private readonly byte[]? _key;

    public SettingSecretProtector(IConfiguration configuration)
    {
        var configured = configuration["Secrets:EncryptionKey"];
        _key = string.IsNullOrWhiteSpace(configured)
            ? null
            : SHA256.HashData(Encoding.UTF8.GetBytes(configured));
    }

    // Whether an encryption key is configured. False ⇒ Protect stores plaintext (unconfigured dev).
    public bool IsConfigured => _key is not null;

    // True for a value this protector produced (needs decrypting) — i.e. not legacy plaintext.
    public static bool IsProtected(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);

    public string Protect(string plaintext)
    {
        if (_key is null)
            return plaintext; // no key configured — store as-is

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var blob = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(blob, 0);
        tag.CopyTo(blob, NonceSize);
        cipher.CopyTo(blob, NonceSize + TagSize);
        return Prefix + Convert.ToBase64String(blob);
    }

    public string Unprotect(string stored)
    {
        if (!IsProtected(stored))
            return stored; // legacy plaintext (or written while unconfigured)

        if (_key is null)
            throw new InvalidOperationException(
                "An encrypted setting was read but Secrets:EncryptionKey is not configured.");

        var blob = Convert.FromBase64String(stored[Prefix.Length..]);
        var nonce = blob.AsSpan(0, NonceSize);
        var tag = blob.AsSpan(NonceSize, TagSize);
        var cipher = blob.AsSpan(NonceSize + TagSize);
        var plainBytes = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plainBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
