using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RfidRastroVerde.Services
{
    public class LocalApiServer
    {
        private readonly HttpListener _listener = new HttpListener();
        private CancellationTokenSource _cts;

        private readonly Func<object> _getStatus;
        private readonly Func<object> _getCurrentSession;
        private readonly Func<object> _getSettings;
        private readonly Action<dynamic> _updateSettings;
        private readonly Action<string> _startSession;
        private readonly Action _resetSession;
        private readonly Func<string> _capturePhoto;

        public LocalApiServer(
            Func<object> getStatus,
            Func<object> getCurrentSession,
            Func<object> getSettings,
            Action<dynamic> updateSettings,
            Action<string> startSession,
            Action resetSession,
            Func<string> capturePhoto)
        {
            _getStatus = getStatus;
            _getCurrentSession = getCurrentSession;
            _getSettings = getSettings;
            _updateSettings = updateSettings;
            _startSession = startSession;
            _resetSession = resetSession;
            _capturePhoto = capturePhoto;
        }

        public void Start(string prefix = "http://localhost:8085/")
        {
            if (_listener.IsListening) return;

            _cts = new CancellationTokenSource();
            _listener.Prefixes.Add(prefix);
            _listener.Start();

            Task.Run(() => ListenLoop(_cts.Token));
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                if (_listener.IsListening)
                    _listener.Stop();
            }
            catch { }
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener.IsListening)
            {
                HttpListenerContext ctx = null;
                try
                {
                    ctx = await _listener.GetContextAsync();
                    _ = Task.Run(() => Handle(ctx), token);
                }
                catch
                {
                    if (!_listener.IsListening) return;
                }
            }
        }

        private void Handle(HttpListenerContext ctx)
        {
            try
            {
                AddCors(ctx.Response);

                if (ctx.Request.HttpMethod == "OPTIONS")
                {
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                    return;
                }

                var path = ctx.Request.Url.AbsolutePath.ToLowerInvariant();

                if (ctx.Request.HttpMethod == "GET" && path == "/api/status")
                {
                    WriteJson(ctx.Response, _getStatus());
                    return;
                }

                if (ctx.Request.HttpMethod == "GET" && path == "/api/session/current")
                {
                    WriteJson(ctx.Response, _getCurrentSession());
                    return;
                }

                if (ctx.Request.HttpMethod == "GET" && path == "/api/settings")
                {
                    WriteJson(ctx.Response, _getSettings());
                    return;
                }

                if (ctx.Request.HttpMethod == "PUT" && path == "/api/settings")
                {
                    var payload = ReadBody(ctx.Request);
                    dynamic body = JsonConvert.DeserializeObject(payload);
                    _updateSettings(body);
                    WriteJson(ctx.Response, new { ok = true, message = "Configurações atualizadas" });
                    return;
                }

                if (ctx.Request.HttpMethod == "POST" && path == "/api/session/start")
                {
                    var payload = ReadBody(ctx.Request);
                    dynamic body = JsonConvert.DeserializeObject(payload);
                    string bandeja = body?.bandeja != null ? (string)body.bandeja : null;

                    _startSession(bandeja);
                    WriteJson(ctx.Response, new { ok = true, message = "Sessão iniciada" });
                    return;
                }

                if (ctx.Request.HttpMethod == "POST" && path == "/api/session/reset")
                {
                    _resetSession();
                    WriteJson(ctx.Response, new { ok = true, message = "Sessão resetada" });
                    return;
                }

                if (ctx.Request.HttpMethod == "POST" && path == "/api/session/capture-photo")
                {
                    var fileName = _capturePhoto();
                    WriteJson(ctx.Response, new { ok = true, fileName });
                    return;
                }

                ctx.Response.StatusCode = 404;
                WriteJson(ctx.Response, new { ok = false, message = "Rota não encontrada" });
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 500;
                WriteJson(ctx.Response, new { ok = false, message = ex.Message });
            }
        }

        private static string ReadBody(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                return reader.ReadToEnd();
        }

        private static void AddCors(HttpListenerResponse response)
        {
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            response.Headers.Add("Access-Control-Allow-Methods", "GET,POST,PUT,OPTIONS");
        }

        private static void WriteJson(HttpListenerResponse response, object obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            var buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }
    }
}