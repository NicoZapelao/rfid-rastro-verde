using RfidRastroVerde.API;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RfidRastroVerde.Api
{
    public sealed class ApiQueue : IDisposable
    {
        private readonly ApiClient _client;
        private readonly ConcurrentQueue<TagReadDto> _q = new ConcurrentQueue<TagReadDto>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task _worker;

        public event Action<string> Log;

        public int Pending => _q.Count;

        private DateTime _lastErrorLog = DateTime.MinValue;

        private const int MaxQueue = 5000;

        private void EmitLogThrottled(string s, int ms = 2000)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastErrorLog).TotalMilliseconds < ms) return;
                _lastErrorLog = now;
                EmitLog(s);
        }

        public ApiQueue(ApiClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _worker = Task.Run(() => WorkerLoop(_cts.Token));
        }

        public void Enqueue(TagReadDto dto)
        {
            if (dto == null) return;

            if (_q.Count >= MaxQueue)
            {
                // fila cheia, descarta silenciosamente ou loga throttled
                EmitLogThrottled("[API] fila cheia, descartando envios.\r\n");
                return;
            }

            _q.Enqueue(dto);
        }

        private async Task WorkerLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TagReadDto item;
                if (!_q.TryDequeue(out item))
                {
                    await Task.Delay(50, ct).ConfigureAwait(false);
                    continue;
                }

                bool ok = false;

                // retry simples (3 tentativas)
                for (int attempt = 1; attempt <= 3 && !ct.IsCancellationRequested; attempt++)
                {
                    try
                    {
                        ok = await _client.PostTagAsync(item, ct).ConfigureAwait(false);
                        if (ok) break;
                    }
                    catch (Exception ex)
                    {
                        EmitLogThrottled("[API] erro: " + ex.Message + "\r\n");
                    }

                    await Task.Delay(200 * attempt, ct).ConfigureAwait(false);
                }

                if (!ok)
                {
                    EmitLog("[API] falhou enviar EPC " + item.Epc + " (fila segue).\r\n");
                    // opcional: re-enfileirar no final
                    // _q.Enqueue(item);
                }
            }
        }

        private void EmitLog(string s)
        {
            var h = Log;
            if (h != null) h(s);
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _worker.Wait(500); } catch { }
            _cts.Dispose();
        }
    }
}
