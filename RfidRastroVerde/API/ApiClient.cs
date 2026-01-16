using RfidRastroVerde.API;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RfidRastroVerde.Api
{
    public sealed class ApiClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly ApiConfig _cfg;

        public ApiClient(ApiConfig cfg)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));

            // força TLS moderno em alguns ambientes .NET Framework
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }

            _http = new HttpClient
            {
                BaseAddress = new Uri(_cfg.BaseUrl, UriKind.Absolute),
                Timeout = _cfg.Timeout
            };

            _http.DefaultRequestHeaders.Add("User-Agent", "RfidRastroVerde/1.0");
            if (!string.IsNullOrEmpty(_cfg.ApiKey))
                _http.DefaultRequestHeaders.Add("X-Api-Key", _cfg.ApiKey);
        }

        public async Task<bool> PostTagAsync(TagReadDto dto, CancellationToken ct)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            // serializa JSON
            string json = SerializeJson(dto);

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var resp = await _http.PostAsync("rfid/tags", content, ct).ConfigureAwait(false))
            {
                return resp.IsSuccessStatusCode;
            }
        }

        private static string SerializeJson<T>(T obj)
        {
            var ser = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                ser.WriteObject(ms, obj);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }
}
