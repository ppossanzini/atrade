using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrade.Trading.Handlers.Broker.OAuth
{
    /// <summary>
    /// Provider token response. Property names match the documented JSON keys exactly, so a change on the
    /// provider side fails to bind instead of silently yielding an empty token.
    /// </summary>
    public class BrokerTokenResponse
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; }

        [JsonPropertyName("tokenType")]
        public string TokenType { get; set; }

        [JsonPropertyName("expiresIn")]
        public long ExpiresIn { get; set; }

        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("errorCode")]
        public string ErrorCode { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// A response counts as successful only when both tokens are present: a rotating grant without a new
        /// refresh token would leave the application unable to renew, so it must not be stored as valid.
        /// </summary>
        public bool HasTokens
        {
            get { return !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(RefreshToken); }
        }
    }

    public class BrokerTokenExchangeResult
    {
        public bool IsSuccess { get; set; }

        public BrokerTokenResponse Tokens { get; set; }

        /// <summary>Provider error description or transport detail. Never carries a token.</summary>
        public string ErrorDetail { get; set; }
    }

    public interface IBrokerTokenClient
    {
        Task<BrokerTokenExchangeResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken);

        Task<BrokerTokenExchangeResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Talks to the provider token endpoint exactly as documented: <c>authorization_code</c> is exchanged
    /// with a GET, <c>refresh_token</c> with a POST, and both carry the client credentials in the query
    /// string. The request URI contains the client secret, so it is never logged.
    /// </summary>
    public class BrokerTokenClient(BrokerOptions options, HttpClient httpClient) : IBrokerTokenClient
    {
        private const string AuthorizationCodeGrant = "authorization_code";
        private const string RefreshTokenGrant = "refresh_token";

        public async Task<BrokerTokenExchangeResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken)
        {
            List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
      {
        new KeyValuePair<string, string>("grant_type", AuthorizationCodeGrant),
        new KeyValuePair<string, string>("code", code),
        new KeyValuePair<string, string>("redirect_uri", options.RedirectUri)
      };

            return await SendAsync(parameters, HttpMethod.Get, cancellationToken);
        }

        public async Task<BrokerTokenExchangeResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
      {
        new KeyValuePair<string, string>("grant_type", RefreshTokenGrant),
        new KeyValuePair<string, string>("refresh_token", refreshToken)
      };

            return await SendAsync(parameters, HttpMethod.Post, cancellationToken);
        }

        private async Task<BrokerTokenExchangeResult> SendAsync(List<KeyValuePair<string, string>> parameters, HttpMethod method, CancellationToken cancellationToken)
        {
            parameters.Add(new KeyValuePair<string, string>("client_id", options.ClientId));
            parameters.Add(new KeyValuePair<string, string>("client_secret", options.ClientSecret));

            string url = options.TokenEndpoint + "?" + BuildQuery(parameters);

            try
            {
                using (HttpRequestMessage request = new HttpRequestMessage(method, url))
                {
                    request.Headers.Add("Accept", "application/json");

                    using (HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken))
                    {
                        string body = await response.Content.ReadAsStringAsync(cancellationToken);

                        return Parse(body, (int)response.StatusCode);
                    }
                }
            }
            catch (HttpRequestException error)
            {
                return new BrokerTokenExchangeResult
                {
                    IsSuccess = false,
                    ErrorDetail = "Transport failure: " + error.Message
                };
            }
            catch (TaskCanceledException)
            {
                return new BrokerTokenExchangeResult
                {
                    IsSuccess = false,
                    ErrorDetail = "The token endpoint did not answer in time."
                };
            }
        }

        private static BrokerTokenExchangeResult Parse(string body, int statusCode)
        {
            BrokerTokenResponse tokens;

            try
            {
                tokens = JsonSerializer.Deserialize<BrokerTokenResponse>(body);
            }
            catch (JsonException)
            {
                return new BrokerTokenExchangeResult
                {
                    IsSuccess = false,
                    ErrorDetail = "The token endpoint returned an unreadable response (status " + statusCode + ")."
                };
            }

            if (tokens != null && tokens.HasTokens && statusCode >= 200 && statusCode < 300)
            {
                return new BrokerTokenExchangeResult
                {
                    IsSuccess = true,
                    Tokens = tokens
                };
            }

            string detail = tokens != null && !string.IsNullOrWhiteSpace(tokens.Description)
              ? tokens.Description
              : "The token endpoint rejected the request (status " + statusCode + ").";

            return new BrokerTokenExchangeResult
            {
                IsSuccess = false,
                Tokens = tokens,
                ErrorDetail = detail
            };
        }

        private static string BuildQuery(List<KeyValuePair<string, string>> parameters)
        {
            List<string> encoded = new List<string>();

            foreach (KeyValuePair<string, string> parameter in parameters)
            {
                encoded.Add(Uri.EscapeDataString(parameter.Key) + "=" + Uri.EscapeDataString(parameter.Value ?? string.Empty));
            }

            return string.Join("&", encoded);
        }
    }
}
