using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Contoso.Secrets
{
    /// <summary>
    /// Stamps an outbound correlation id on every vault request. The legacy
    /// <c>KeyVaultClient</c> takes <see cref="DelegatingHandler"/> instances directly in its
    /// constructor.
    /// </summary>
    public class CorrelationIdHandler : DelegatingHandler
    {
        public const string HeaderName = "x-contoso-correlation-id";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!request.Headers.Contains(HeaderName))
            {
                request.Headers.Add(HeaderName, CorrelationScope.Current);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Ambient correlation id for the current logical operation.
    /// </summary>
    public static class CorrelationScope
    {
        private static readonly AsyncLocal<string> Value = new AsyncLocal<string>();

        public static string Current
        {
            get { return Value.Value ?? "unset"; }
            set { Value.Value = value; }
        }
    }
}
