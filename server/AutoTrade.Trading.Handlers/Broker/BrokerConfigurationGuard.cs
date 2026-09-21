using System;

namespace AutoTrade.Trading.Handlers.Broker
{
    /// <summary>
    /// Fail-closed startup validation of the broker configuration.
    ///
    /// The rule is: as soon as the broker is enabled (a client id is present), every dependent setting
    /// must be valid, otherwise the host refuses to start. A broker that is not configured at all is a
    /// supported state, so the rest of the application keeps working without broker credentials.
    /// </summary>
    public static class BrokerConfigurationGuard
    {
        public static void EnsureValid(BrokerOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            bool hasClientId = !string.IsNullOrWhiteSpace(options.ClientId);
            bool hasClientSecret = !string.IsNullOrWhiteSpace(options.ClientSecret);

            if (!hasClientId && !hasClientSecret)
            {
                return;
            }

            if (!hasClientId || !hasClientSecret)
            {
                throw new InvalidOperationException(
                  "Broker configuration is incomplete: Trading:Broker:ClientId and Trading:Broker:ClientSecret must both be set.");
            }

            byte[] key;

            if (!BrokerTokenProtector.TryCreateKey(options.TokenKey, out key))
            {
                throw new InvalidOperationException(
                  "Broker is configured but Trading:Broker:TokenKey is missing or is not a base64 encoded 256 bit key.");
            }

            Uri redirectUri;

            if (!Uri.TryCreate(options.RedirectUri, UriKind.Absolute, out redirectUri) || !IsHttpRedirect(redirectUri))
            {
                throw new InvalidOperationException(
                  "Broker is configured but Trading:Broker:RedirectUri is missing or is not an absolute http(s) URI.");
            }

            if (string.IsNullOrWhiteSpace(options.Scope))
            {
                throw new InvalidOperationException(
                  "Broker is configured but Trading:Broker:Scope is empty.");
            }
        }

        /// <summary>
        /// The scheme must be checked explicitly: on Unix a value such as <c>/api/callback</c> is accepted by
        /// <see cref="Uri.TryCreate(string, UriKind, out Uri)"/> as an absolute <c>file</c> URI, which would
        /// let a relative redirect URI pass validation.
        /// </summary>
        private static bool IsHttpRedirect(Uri redirectUri)
        {
            return string.Equals(redirectUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
              || string.Equals(redirectUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }
    }
}
