using System;

namespace Contoso.Secrets
{
    /// <summary>
    /// Minimal helper so the sample does not need a JSON dependency just to pull
    /// <c>access_token</c> out of an OAuth response.
    /// </summary>
    internal static class TokenPayloadReader
    {
        private const string TokenKey = "\"access_token\":\"";

        public static string ReadAccessToken(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                throw new InvalidOperationException("Empty token response.");
            }

            var start = payload.IndexOf(TokenKey, StringComparison.Ordinal);
            if (start < 0)
            {
                throw new InvalidOperationException("Token response did not contain an access_token.");
            }

            start += TokenKey.Length;
            var end = payload.IndexOf('"', start);
            if (end < 0)
            {
                throw new InvalidOperationException("Token response was malformed.");
            }

            return payload.Substring(start, end - start);
        }
    }
}
