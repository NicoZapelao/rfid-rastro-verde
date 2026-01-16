using System;
using System.Collections.Generic;
using System.Threading;
using RFID;
using RfidRastroVerde.Models_Proj;

namespace RfidRastroVerde.Driver_Proj
{
    public sealed class RfidReaderManager : IDisposable
    {
        private readonly List<RfidReaderDriver> _drivers = new List<RfidReaderDriver>();
        public IReadOnlyList<RfidReaderDriver> Drivers => _drivers;

        public event Action<string> Log;

        // ✅ EPC único global (só dispara 1 vez por EPC no ciclo)
        public event Action<TagRead> TagUnique;
        public event Action<int> GlobalProgress;
        public event Action<int> GlobalFinished;

        private readonly object _lock = new object();
        private readonly HashSet<string> _globalSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private int _targetGlobal;
        private bool _running;

        public int GlobalUniqueCount { get { lock (_lock) return _globalSeen.Count; } }
        public int TargetGlobal { get { lock (_lock) return _targetGlobal; } }
        public bool IsRunning { get { lock (_lock) return _running; } }

        public void ResetGlobal()
        {
            lock (_lock)
            {
                _globalSeen.Clear();
                _targetGlobal = 0;
                _running = false;
            }
        }

        public void OpenAllDetected(int pollIntervalMs)
        {
            CloseAll();
            ResetGlobal();

            int count = 0;
            try
            {
                lock (RfidReaderDriver.HidLock)
                    count = SWHidApi.SWHid_GetUsbCount();
            }
            catch (Exception ex)
            {
                EmitLog("Erro GetUsbCount: " + ex.Message + "\r\n");
                return;
            }

            EmitLog("USB detectados: " + count + "\r\n");

            for (int i = 0; i < count; i++)
            {
                var d = new RfidReaderDriver(pollIntervalMs);
                int localIndex = i;

                d.Log += (m) => EmitLog("[R" + localIndex + "] " + m);

                d.TagReceived += (t) =>
                {
                    // força índice estável
                    t.ReaderIndex = localIndex;

                    bool isNew = false;
                    int total = 0;
                    int target = 0;
                    bool finished = false;

                    lock (_lock)
                    {
                        if (_running)
                        {
                            // ✅ dedupe global por EPC
                            isNew = _globalSeen.Add(t.Epc);
                            total = _globalSeen.Count;
                            target = _targetGlobal;
                            finished = (target > 0 && total >= target);
                        }
                    }

                    if (isNew)
                    {
                        TagUnique?.Invoke(t);
                        GlobalProgress?.Invoke(total);

                        if (finished)
                        {
                            EmitLog("Meta global atingida: " + total + "\r\n");

                            // marca como parado ANTES de parar drivers
                            lock (_lock) _running = false;

                            StopAll();
                            GlobalFinished?.Invoke(total);
                        }
                    }
                };

                bool ok = d.Open(i);
                if (ok) _drivers.Add(d);
                else d.Dispose();
            }

            EmitLog("Readers abertos: " + _drivers.Count + "\r\n");
        }

        public void StartGlobalFresh(int targetGlobalUnique)
        {
            if (_drivers.Count == 0)
            {
                EmitLog("Nenhum leitor aberto.\r\n");
                return;
            }
            if (targetGlobalUnique <= 0)
            {
                EmitLog("Meta global inválida.\r\n");
                return;
            }

            // ✅ reset total do ciclo (sempre do zero)
            lock (_lock)
            {
                _globalSeen.Clear();
                _targetGlobal = targetGlobalUnique;
                _running = true;
            }

            // ✅ limpeza segura (DLL pode manter estado global)
            try
            {
                lock (RfidReaderDriver.HidLock)
                {
                    try { SWHidApi.SWHid_StopRead(0xFF); } catch { }
                    try { SWHidApi.SWHid_ClearTagBuf(); } catch { }
                }
            }
            catch { }

            // ✅ inicia leitura em todos os leitores abertos
            for (int i = 0; i < _drivers.Count; i++)
            {
                bool ok = _drivers[i].StartReading(clearBufferBeforeStart: false);
                if (!ok) EmitLog("[R" + i + "] StartReading falhou.\r\n");
            }

            EmitLog("StartGlobal OK. Meta global: " + targetGlobalUnique + "\r\n");
        }

        public void StopAll()
        {
            // marca como parado (evita aceitar tags durante stop)
            lock (_lock) _running = false;

            for (int i = 0; i < _drivers.Count; i++)
            {
                try { _drivers[i].StopReading(); } catch { }
            }

            // limpa buffer pro próximo Start
            try
            {
                lock (RfidReaderDriver.HidLock)
                {
                    try { SWHidApi.SWHid_ClearTagBuf(); } catch { }
                }
            }
            catch { }

            EmitLog("StopAll OK.\r\n");
        }

        public void CloseAll()
        {
            // para tudo antes de destruir
            try { StopAll(); } catch { }

            for (int i = 0; i < _drivers.Count; i++)
            {
                try { _drivers[i].Dispose(); } catch { }
            }
            _drivers.Clear();

            // dá uma “respirada” no HID pra não ficar estado zumbi
            Thread.Sleep(80);

            EmitLog("CloseAll OK.\r\n");
        }

        private void EmitLog(string m) => Log?.Invoke(m);

        public void Dispose()
        {
            try { CloseAll(); } catch { }
        }
    }
}
