using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Arcademia.Achievements
{
    internal sealed class SandboxResponse
    {
        public int StatusCode;
        public string Body;
        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
    }

    internal static class SandboxTransport
    {
        public const string KeyHeader = "X-Arcademia-Key";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public static async Task<byte[]> GetBytesAsync(string url, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource(timeout))
            using (var response = await Http.GetAsync(url, cts.Token))
            {
                if (!response.IsSuccessStatusCode)
                    return null;
                return await response.Content.ReadAsByteArrayAsync();
            }
        }

        public static async Task<SandboxResponse> SendAsync(
            HttpMethod method,
            string apiBase,
            string path,
            string apiKey,
            string jsonBody
        )
        {
            var url = apiBase.TrimEnd('/') + path;
            using (var request = new HttpRequestMessage(method, url))
            {
                request.Headers.TryAddWithoutValidation(KeyHeader, apiKey ?? "");
                if (jsonBody != null)
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                using (var response = await Http.SendAsync(request))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return new SandboxResponse { StatusCode = (int)response.StatusCode, Body = body };
                }
            }
        }
    }
}
