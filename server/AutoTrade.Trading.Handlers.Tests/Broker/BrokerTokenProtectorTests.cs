using System;
using System.Security.Cryptography;
using System.Text;
using AutoTrade.Trading.Handlers.Broker;
using AutoTrade.Trading.Core.Enums;
using Xunit;

namespace AutoTrade.Trading.Handlers.Tests.Broker
{
  /// <summary>
  /// The protector is the only thing standing between a database copy and a non-expiring provider
  /// refresh token, so its failure modes matter more than its happy path.
  /// </summary>
  public class BrokerTokenProtectorTests
  {
    private static byte[] CreateKey(byte fill)
    {
      byte[] key = new byte[32];
      Array.Fill(key, fill);

      return key;
    }

    [Fact]
    public void ProtectThenUnprotect_RoundTripsThePlaintext()
    {
      BrokerTokenProtector protector = new BrokerTokenProtector(CreateKey(7));
      const string token = "VCuafFhy81AFZjsWkbuEzdOhhRj5YTWz8fWUwHam7KM";

      string protectedToken = protector.Protect(token);

      Assert.NotEqual(token, protectedToken);
      Assert.DoesNotContain(token, protectedToken, StringComparison.Ordinal);
      Assert.StartsWith("v1.", protectedToken, StringComparison.Ordinal);
      Assert.Equal(token, protector.Unprotect(protectedToken));
    }

    [Fact]
    public void Protect_WithTheSamePlaintext_ProducesDifferentCiphertext()
    {
      BrokerTokenProtector protector = new BrokerTokenProtector(CreateKey(3));

      string first = protector.Protect("same-token");
      string second = protector.Protect("same-token");

      Assert.NotEqual(first, second);
      Assert.Equal("same-token", protector.Unprotect(first));
      Assert.Equal("same-token", protector.Unprotect(second));
    }

    [Fact]
    public void Unprotect_WithATamperedPayload_FailsInsteadOfReturningGarbage()
    {
      BrokerTokenProtector protector = new BrokerTokenProtector(CreateKey(11));
      string protectedToken = protector.Protect("token-value");

      string[] parts = protectedToken.Split('.');
      byte[] payload = Convert.FromBase64String(parts[3]);
      payload[0] = (byte)(payload[0] ^ 0xFF);
      string tampered = string.Join(".", parts[0], parts[1], parts[2], Convert.ToBase64String(payload));

      string plaintext;
      Assert.False(protector.TryUnprotect(tampered, out plaintext));
      Assert.Null(plaintext);
      Assert.Throws<CryptographicException>(() => protector.Unprotect(tampered));
    }

    [Fact]
    public void Unprotect_WithADifferentKey_Fails()
    {
      BrokerTokenProtector writer = new BrokerTokenProtector(CreateKey(1));
      BrokerTokenProtector reader = new BrokerTokenProtector(CreateKey(2));
      string protectedToken = writer.Protect("token-value");

      string plaintext;
      Assert.False(reader.TryUnprotect(protectedToken, out plaintext));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("plain-token-without-envelope")]
    [InlineData("v2.AAAA.AAAA.AAAA")]
    [InlineData("v1.AAAA.AAAA")]
    public void Unprotect_WithMalformedInput_ReturnsFalseInsteadOfThrowing(string candidate)
    {
      BrokerTokenProtector protector = new BrokerTokenProtector(CreateKey(5));

      string plaintext;
      Assert.False(protector.TryUnprotect(candidate, out plaintext));
    }

    [Fact]
    public void Constructor_WithAKeyOfTheWrongSize_IsRejected()
    {
      Assert.Throws<ArgumentException>(() => new BrokerTokenProtector(new byte[16]));
      Assert.Throws<ArgumentException>(() => new BrokerTokenProtector(null));
    }

    [Fact]
    public void TryCreateKey_AcceptsABase64KeyOfExactly32Bytes()
    {
      string configuredKey = Convert.ToBase64String(CreateKey(9));

      byte[] key;
      Assert.True(BrokerTokenProtector.TryCreateKey(configuredKey, out key));
      Assert.Equal(32, key.Length);
      Assert.Equal(configuredKey, Convert.ToBase64String(key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64!!")]
    [InlineData("c2hvcnQ=")]
    public void TryCreateKey_RejectsMissingOrMalformedKeys(string configuredKey)
    {
      byte[] key;
      Assert.False(BrokerTokenProtector.TryCreateKey(configuredKey, out key));
      Assert.Null(key);
    }

    [Fact]
    public void UnconfiguredProtector_RefusesEveryOperation()
    {
      UnconfiguredBrokerTokenProtector protector = new UnconfiguredBrokerTokenProtector();

      Assert.Throws<InvalidOperationException>(() => protector.Protect("token"));
      Assert.Throws<InvalidOperationException>(() => protector.Unprotect("v1.a.b.c"));

      string plaintext;
      Assert.Throws<InvalidOperationException>(() => protector.TryUnprotect("v1.a.b.c", out plaintext));
    }

    [Fact]
    public void Protect_SupportsNonAsciiTokenMaterial()
    {
      BrokerTokenProtector protector = new BrokerTokenProtector(CreateKey(4));
      string token = "token-con-caratteri-àèìòù-€";

      Assert.Equal(token, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(protector.Unprotect(protector.Protect(token)))));
    }
  }

  /// <summary>
  /// The guard is the fail-closed gate at startup: an enabled broker without a usable key must stop the
  /// host, while an unconfigured broker must stay a supported state.
  /// </summary>
  public class BrokerConfigurationGuardTests
  {
    private static BrokerOptions CreateConfiguredOptions()
    {
      return new BrokerOptions
      {
        ClientId = "client-id",
        ClientSecret = "client-secret",
        TokenKey = Convert.ToBase64String(new byte[32]),
        RedirectUri = "http://127.0.0.1:5271/api/broker/callback",
        Environment = TradingEnvironment.Demo
      };
    }

    [Fact]
    public void EnsureValid_WithAFullyConfiguredBroker_Passes()
    {
      BrokerConfigurationGuard.EnsureValid(CreateConfiguredOptions());
    }

    [Fact]
    public void EnsureValid_WithNoBrokerConfiguration_IsASupportedState()
    {
      BrokerConfigurationGuard.EnsureValid(new BrokerOptions());
    }

    [Fact]
    public void EnsureValid_WithAClientIdButNoSecret_FailsClosed()
    {
      BrokerOptions options = CreateConfiguredOptions();
      options.ClientSecret = null;

      Assert.Throws<InvalidOperationException>(() => BrokerConfigurationGuard.EnsureValid(options));
    }

    [Fact]
    public void EnsureValid_WithASecretButNoClientId_FailsClosed()
    {
      BrokerOptions options = CreateConfiguredOptions();
      options.ClientId = "  ";

      Assert.Throws<InvalidOperationException>(() => BrokerConfigurationGuard.EnsureValid(options));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64")]
    [InlineData("c2hvcnQ=")]
    public void EnsureValid_WithAnUnusableTokenKey_FailsClosed(string tokenKey)
    {
      BrokerOptions options = CreateConfiguredOptions();
      options.TokenKey = tokenKey;

      Assert.Throws<InvalidOperationException>(() => BrokerConfigurationGuard.EnsureValid(options));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/api/broker/callback")]
    [InlineData("file:///api/broker/callback")]
    [InlineData("ftp://broker.example/callback")]
    public void EnsureValid_WithANonHttpsRedirectUri_FailsClosed(string redirectUri)
    {
      BrokerOptions options = CreateConfiguredOptions();
      options.RedirectUri = redirectUri;

      Assert.Throws<InvalidOperationException>(() => BrokerConfigurationGuard.EnsureValid(options));
    }

    [Fact]
    public void EnsureValid_WithAnEmptyScope_FailsClosed()
    {
      BrokerOptions options = CreateConfiguredOptions();
      options.Scope = "";

      Assert.Throws<InvalidOperationException>(() => BrokerConfigurationGuard.EnsureValid(options));
    }

    [Fact]
    public void EnsureValid_WithoutOptions_IsRejected()
    {
      Assert.Throws<ArgumentNullException>(() => BrokerConfigurationGuard.EnsureValid(null));
    }
  }
}
