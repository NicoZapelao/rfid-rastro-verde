using Newtonsoft.Json;
using RfidRastroVerde.API;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RfidRastroVerde.Api
{
    public sealed class ApiQueue : IDisposable
    {
        private readonly ApiClient _client;

        private readonly ConcurrentQueue<TagReadDto> _tagQ = new ConcurrentQueue<TagReadDto>();
        private readonly ConcurrentQueue<TraySnapshotDto> _snapQ = new ConcurrentQueue<TraySnapshotDto>();

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task _worker;

        public event Action<string> Log;

        public int PendingTags => _tagQ.Count;
        public int PendingSnapshots => _snapQ.Count;

        private DateTime _lastErrorLog = DateTime.MinValue;

        private const int MaxTagQueue = 5000;
        private const int MaxSnapQueue = 200;

        private readonly string _outboxDir;

        public ApiQueue(ApiClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));

            _outboxDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RfidRastroVerde", "outbox");

            Directory.CreateDirectory(_outboxDir);

            // carrega snapshots pendentes do disco
            LoadOutboxSnapshots();

            _worker = Task.Run(() => WorkerLoop(_cts.Token));
        }

        public void Enqueue(TagReadDto dto)
        {
            if (dto == null) return;

            if (_tagQ.Count >= MaxTagQueue)
            {
                EmitLogThrottled("[API] fila de TAG cheia, descartando envios.\r\n");
                return;
            }

            _tagQ.Enqueue(dto);
        }

        public void EnqueueSnapshot(TraySnapshotDto dto)
        {
            if (dto == null) return;

            if (_snapQ.Count >= MaxSnapQueue)
            {
                EmitLogThrottled("[API] fila de SNAPSHOT cheia, descartando snapshot.\r\n");
                return;
            }

            // persistir no disco ANTES de enfileirar (garante que não perde se app cair)
            PersistSnapshotToOutbox(dto);

            _snapQ.Enqueue(dto);
        }

        private async Task WorkerLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                // prioriza snapshots (mais importantes e menos volume)
                if (_snapQ.TryDequeue(out var snap))
                {
                    await SendSnapshotWithRetry(snap, ct).ConfigureAwait(false);
                    continue;
                }

                if (_tagQ.TryDequeue(out var tag))
                {
                    await SendTagWithRetry(tag, ct).ConfigureAwait(false);
                    continue;
                }

                await Task.Delay(60, ct).ConfigureAwait(false);
            }
        }

        private async Task SendTagWithRetry(TagReadDto item, CancellationToken ct)
        {
            bool ok = false;

            for (int attempt = 1; attempt <= 3 && !ct.IsCancellationRequested; attempt++)
            {
                try
                {
                    ok = await _client.PostTagAsync(item, ct).ConfigureAwait(false);
                    if (ok) break;
                }
                catch (Exception ex)
                {
                    EmitLogThrottled("[API] erro TAG: " + ex.Message + "\r\n");
                }

                await Task.Delay(250 * attempt, ct).ConfigureAwait(false);
            }

            if (!ok)
                EmitLog("[API] falhou enviar TAG EPC=" + item.Epc + "\r\n");
        }

        private async Task SendSnapshotWithRetry(TraySnapshotDto snap, CancellationToken ct)
        {
            bool ok = false;

            for (int attempt = 1; attempt <= 6 && !ct.IsCancellationRequested; attempt++)
            {
                try
                {
                    ok = await _client.PostTraySnapshotAsync(snap, ct).ConfigureAwait(false);
                    if (ok) break;
                }
                catch (Exception ex)
                {
                    EmitLogThrottled("[API] erro SNAPSHOT: " + ex.Message + "\r\n", 3000);
                }

                // backoff crescente
                await Task.Delay(400 * attempt, ct).ConfigureAwait(false);
            }

            if (ok)
            {
                MarkSnapshotAsSent(snap.SnapshotId);
                EmitLog("[API] snapshot enviado OK: " + snap.TrayEpc + " (" + snap.UniqueItemCount + " itens)\r\n");
            }
            else
            {
                EmitLog("[API] falhou enviar snapshot: " + snap.TrayEpc + " (vai ficar no outbox)\r\n");
                // não re-enfileira aqui: ele já está no outbox e será tentado no próximo start (ou pode criar retry timer)
            }
        }

        // -----------------------------
        // OUTBOX (snapshots)
        // -----------------------------
        private void PersistSnapshotToOutbox(TraySnapshotDto dto)
        {
            try
            {
                var json = JsonConvert.SerializeObject(dto);
                var file = Path.Combine(_outboxDir, dto.SnapshotId + ".json");
                File.WriteAllText(file, json, Encoding.UTF8);
            }
            catch { }
        }

        private void LoadOutboxSnapshots()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_outboxDir, "*.json"))
                {
                    var json = File.ReadAllText(file, Encoding.UTF8);
                    var dto = JsonConvert.DeserializeObject<TraySnapshotDto>(json);
                    if (dto != null)
                    {
                        // evita estourar memória se tiver lixo
                        if (_snapQ.Count < MaxSnapQueue)
                            _snapQ.Enqueue(dto);
                    }
                }

                if (_snapQ.Count > 0)
                    EmitLog("[API] outbox carregado: " + _snapQ.Count + " snapshots pendentes\r\n");
            }
            catch (Exception ex)
            {
                EmitLogThrottled("[API] falha ao carregar outbox: " + ex.Message + "\r\n");
            }
        }

        private void MarkSnapshotAsSent(string snapshotId)
        {
            try
            {
                var file = Path.Combine(_outboxDir, snapshotId + ".json");
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch { }
        }

        // -----------------------------
        // LOG
        // -----------------------------
        private void EmitLogThrottled(string s, int ms = 2000)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastErrorLog).TotalMilliseconds < ms) return;
            _lastErrorLog = now;
            EmitLog(s);
        }

        private void EmitLog(string s)
        {
            var h = Log;
            if (h != null) h(s);
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _worker.Wait(800); } catch { }
            _cts.Dispose();
        }
    }
}
