using System;
using System.Security.Cryptography;
using System.Text;

namespace AutoTrade.Trading.Handlers.Broker
{
    /// <summary>
    /// Protects provider tokens at rest. Provider refresh tokens never expire and grant weeks of access,
    /// so they must not be readable from a database copy or a backup.
    /// </summary>
    public interface IBrokerTokenProtector
    {
        string Protect(string plaintext);

        string Unprotect(string ciphertext);

        bool TryUnprotect(string ciphertext, out string plaintext);
    }

    /// <summary>
    /// AES-256-GCM with a key supplied by configuration. The stored value is a versioned envelope
    /// (<c>v1.nonce.tag.ciphertext</c>, all base64) so the format can change without misreading old rows.
    /// A tampered or truncated value fails authentication instead of returning garbage.
    /// </summary>
    public sealed class BrokerTokenProtector : IBrokerTokenProtector
    {
        private const string EnvelopePrefix = "v1";
        private const int KeySizeBytes = 32;
        private const int NonceSizeBytes = 12;
        private const int TagSizeBytes = 16;

        private readonly byte[] key;

        public BrokerTokenProtector(byte[] key)
        {
            if (key == null || key.Length != KeySizeBytes)
            {
                throw new ArgumentException("The broker token key must be 256 bits.", nameof(key));
            }

            this.key = key;
        }

        /// <summary>
        /// Decodes a configured key. The comparison is a fixed size check only: no secret material is
        /// included in the failure message.
        /// </summary>
        public static bool TryCreateKey(string configuredKey, out byte[] key)
        {
            key = null;

            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                return false;
            }

            try
            {
                byte[] decoded = Convert.FromBase64String(configuredKey.Trim());
                if (decoded.Length != KeySizeBytes)
                {
                    return false;
                }

                key = decoded;

                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public string Protect(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
            {
                throw new ArgumentException("Nothing to protect.", nameof(plaintext));
            }

            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] tag = new byte[TagSizeBytes];

            using (AesGcm aes = new AesGcm(key, TagSizeBytes))
            {
                aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
            }

            return string.Join(
              ".",
              EnvelopePrefix,
              Convert.ToBase64String(nonce),
              Convert.ToBase64String(tag),
              Convert.ToBase64String(ciphertext));
        }

        public string Unprotect(string ciphertext)
        {
            string plaintext;

            if (!TryUnprotect(ciphertext, out plaintext))
            {
                throw new CryptographicException("The stored broker token could not be decrypted.");
            }

            return plaintext;
        }

        public bool TryUnprotect(string ciphertext, out string plaintext)
        {
            plaintext = null;

            if (string.IsNullOrWhiteSpace(ciphertext))
            {
                return false;
            }

            string[] parts = ciphertext.Split('.');
            if (parts.Length != 4 || parts[0] != EnvelopePrefix)
            {
                return false;
            }

            try
            {
                byte[] nonce = Convert.FromBase64String(parts[1]);
                byte[] tag = Convert.FromBase64String(parts[2]);
                byte[] payload = Convert.FromBase64String(parts[3]);

                if (nonce.Length != NonceSizeBytes || tag.Length != TagSizeBytes)
                {
                    return false;
                }

                byte[] plaintextBytes = new byte[payload.Length];

                using (AesGcm aes = new AesGcm(key, TagSizeBytes))
                {
                    aes.Decrypt(nonce, payload, tag, plaintextBytes);
                }

                plaintext = Encoding.UTF8.GetString(plaintextBytes);

                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Used when the broker is not configured. It never protects or reveals anything: touching the
    /// token store without a key is a programming error, not a degraded mode.
    /// </summary>
    public sealed class UnconfiguredBrokerTokenProtector : IBrokerTokenProtector
    {
        public string Protect(string plaintext)
        {
            throw new InvalidOperationException("The broker is not configured: Trading:Broker:TokenKey is missing.");
        }

        public string Unprotect(string ciphertext)
        {
            throw new InvalidOperationException("The broker is not configured: Trading:Broker:TokenKey is missing.");
        }

        public bool TryUnprotect(string ciphertext, out string plaintext)
        {
            throw new InvalidOperationException("The broker is not configured: Trading:Broker:TokenKey is missing.");
        }
    }
}
