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

        // EPC único global (só dispara 1 vez por EPC no ciclo)
        public event Action<TagRead> TagUnique;
        public event Action<int> GlobalProgress;
        public event Action<int> GlobalFinished;

        private readonly object _lock = new object();
        private readonly HashSet<string> _globalSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private int _targetGlobal;
        private bool _running;

        // Gate para serializar Start/Stop/Close (evita corrida)
        private readonly object _gate = new object();

        // Geração do ciclo (ignora tags atrasadas)
        private int _cycleId = 0;

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
            lock (_gate)
            {
                CloseAll_NoGate(); // tudo limpo
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

                    // handler único por driver
                    d.TagReceived += (t) =>
                    {
                        // força índice estável
                        t.ReaderIndex = localIndex;

                        int myCycle;
                        bool accept;
                        lock (_lock)
                        {
                            myCycle = _cycleId;
                            accept = _running;
                        }

                        // se não estamos rodando, ignora
                        if (!accept) return;

                        // dedupe global por EPC
                        bool isNew = false;
                        int total = 0;
                        int target = 0;
                        bool finished = false;

                        lock (_lock)
                        {
                            // se mudou o ciclo enquanto a tag vinha, ignora
                            if (!_running || myCycle != _cycleId) return;

                            isNew = _globalSeen.Add(t.Epc);
                            if (!isNew) return;

                            total = _globalSeen.Count;
                            target = _targetGlobal;
                            finished = (target > 0 && total >= target);
                        }

                        // só dispara fora do lock
                        TagUnique?.Invoke(t);
                        GlobalProgress?.Invoke(total);

                        if (finished)
                        {
                            EmitLog("Meta global atingida: " + total + "\r\n");
                            FinishGlobal(total);
                        }
                    };

                    bool ok = d.Open(i);
                    if (ok) _drivers.Add(d);
                    else d.Dispose();
                }

                EmitLog("Readers abertos: " + _drivers.Count + "\r\n");
            }
        }

        public void StartGlobalFresh(int targetGlobalUnique)
        {
            lock (_gate)
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

                // Se estava rodando ou ficou resquício, para tudo antes
                StopAll_NoGate();

                // novo ciclo (qualquer tag atrasada vira “velha”)
                lock (_lock)
                {
                    _cycleId++;
                    _globalSeen.Clear();
                    _targetGlobal = targetGlobalUnique;
                    _running = true;
                }

                // limpeza segura (DLL pode manter estado global)
                try
                {
                    lock (RfidReaderDriver.HidLock)
                    {
                        try { SWHidApi.SWHid_StopRead(0xFF); } catch { }
                        try { SWHidApi.SWHid_ClearTagBuf(); } catch { }
                    }
                }
                catch { }

                // inicia leitura em todos os leitores abertos
                for (int i = 0; i < _drivers.Count; i++)
                {
                    bool ok = _drivers[i].StartReading(clearBufferBeforeStart: false);
                    if (!ok) EmitLog("[R" + i + "] StartReading falhou.\r\n");
                }

                EmitLog("StartGlobal OK. Meta global: " + targetGlobalUnique + "\r\n");
            }
        }

        private void FinishGlobal(int total)
        {
            lock (_gate)
            {
                // encerra uma vez só
                bool doFinish = false;
                lock (_lock)
                {
                    if (_running)
                    {
                        _running = false;
                        _cycleId++; // invalida qualquer tag atrasada
                        doFinish = true;
                    }
                }
                if (!doFinish) return;

                StopAll_NoGate();
                GlobalFinished?.Invoke(total);
            }
        }

        public void StopAll()
        {
            lock (_gate)
            {
                StopAll_NoGate();
            }
        }

        private void StopAll_NoGate()
        {
            // marca como parado e invalida tags atrasadas
            lock (_lock)
            {
                _running = false;
                _cycleId++;
            }

            // 1) manda parar timers/leitura
            for (int i = 0; i < _drivers.Count; i++)
            {
                try { _drivers[i].StopReading(); } catch { }
            }

            // 2) espera “tick em voo” terminar (evita corrida com GetTagBuf)
            for (int i = 0; i < _drivers.Count; i++)
            {
                try { _drivers[i].WaitIdle(250); } catch { } // <- precisa no Driver
            }

            // 3) limpa buffer HID depois que ninguém mais está lendo
            try
            {
                lock (RfidReaderDriver.HidLock)
                {
                    try { SWHidApi.SWHid_StopRead(0xFF); } catch { }
                    try { SWHidApi.SWHid_ClearTagBuf(); } catch { }
                }
            }
            catch { }

            EmitLog("StopAll OK.\r\n");
        }

        public void CloseAll()
        {
            lock (_gate)
            {
                CloseAll_NoGate();
            }
        }

        private void CloseAll_NoGate()
        {
            try { StopAll_NoGate(); } catch { }

            for (int i = 0; i < _drivers.Count; i++)
            {
                try { _drivers[i].Dispose(); } catch { }
            }
            _drivers.Clear();

            // respira um pouco pro HID/USB não ficar “zumbi”
            Thread.Sleep(120);

            EmitLog("CloseAll OK.\r\n");
        }

        private void EmitLog(string m) => Log?.Invoke(m);

        public void Dispose()
        {
            try { CloseAll(); } catch { }
        }
    }
}
