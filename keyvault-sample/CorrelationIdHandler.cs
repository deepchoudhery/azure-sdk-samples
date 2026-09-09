using System.Threading;
using Azure.Core;
using Azure.Core.Pipeline;

namespace Contoso.Secrets
{
    /// <summary>
    /// Stamps one outbound correlation id on a logical vault operation. Registered in
    /// the per-call pipeline so retries retain the same header value.
    /// </summary>
    public class CorrelationIdHandler : HttpPipelineSynchronousPolicy
    {
        public const string HeaderName = "x-contoso-correlation-id";

        public override void OnSendingRequest(HttpMessage message)
        {
            if (!message.Request.Headers.Contains(HeaderName))
            {
                message.Request.Headers.Add(HeaderName, CorrelationScope.Current);
            }
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
