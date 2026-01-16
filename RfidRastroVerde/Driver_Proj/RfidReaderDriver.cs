using System;
using System.Text;
using System.Threading;
using RFID;
using RfidRastroVerde.Models_Proj;

namespace RfidRastroVerde.Driver_Proj
{
    public sealed class RfidReaderDriver : IDisposable
    {
        private const byte DEV = 0xFF;

        // 🔒 LOCK GLOBAL: protege TODAS as chamadas à DLL (muito provável ela não ser thread-safe)
        internal static readonly object HidLock = new object();

        private bool _isOpen;
        private bool _isReading;

        private readonly int _pollIntervalMs;
        private Timer _pollTimer; // ✅ timer de background (não depende da UI)
        private readonly byte[] _tagBuf = new byte[64 * 1024];

        public bool IsOpen => _isOpen;
        public bool IsReading => _isReading;

        public int ReaderIndex { get; private set; }
        public string ReaderSn { get; private set; } = "";

        public event Action<string> Log;
        public event Action<TagRead> TagReceived;

        public RfidReaderDriver(int pollIntervalMs)
        {
            _pollIntervalMs = Math.Max(20, pollIntervalMs);
        }

        public int GetUsbCount()
        {
            try
            {
                lock (HidLock)
                    return SWHidApi.SWHid_GetUsbCount();
            }
            catch (Exception ex)
            {
                EmitLog("GetUsbCount erro: " + ex.Message + "\r\n");
                return 0;
            }
        }

        public bool Open(int index)
        {
            if (_isOpen) return true;

            int count = GetUsbCount();
            if (count <= 0)
            {
                EmitLog("Nenhum leitor HID encontrado.\r\n");
                return false;
            }

            if (index < 0 || index > ushort.MaxValue)
            {
                EmitLog("Index inválido.\r\n");
                return false;
            }

            bool okOpen;
            lock (HidLock)
                okOpen = SWHidApi.SWHid_OpenDevice((ushort)index);

            if (!okOpen)
            {
                EmitLog("Falha ao abrir HID index " + index + ".\r\n");
                return false;
            }

            _isOpen = true;
            ReaderIndex = index;

            EmitLog("HID aberto (index " + index + ").\r\n");

            // system info
            try
            {
                var sys = new byte[16];
                bool okInfo;
                lock (HidLock)
                    okInfo = SWHidApi.SWHid_GetDeviceSystemInfo(DEV, sys);

                if (okInfo)
                {
                    EmitLog("SoftVer: " + (sys[0] >> 4) + "." + (sys[0] & 0x0F) + "\r\n");
                    EmitLog("HardVer: " + (sys[1] >> 4) + "." + (sys[1] & 0x0F) + "\r\n");

                    var snSb = new StringBuilder();
                    for (int i = 0; i < 7; i++) snSb.Append(sys[2 + i].ToString("X2"));
                    ReaderSn = snSb.ToString();

                    EmitLog("SN:" + ReaderSn + "\r\n");
                }
            }
            catch { }

            return true;
        }

        public bool StartReading(bool clearBufferBeforeStart)
        {
            if (!_isOpen)
            {
                EmitLog("Abra o leitor antes.\r\n");
                return false;
            }

            if (_isReading) return true;

            // opcional (mas CUIDADO: pode ser global na DLL)
            if (clearBufferBeforeStart)
            {
                try
                {
                    lock (HidLock)
                        SWHidApi.SWHid_ClearTagBuf();
                }
                catch { }
            }

            bool ok;
            lock (HidLock)
                ok = SWHidApi.SWHid_StartRead(DEV);

            if (!ok)
            {
                EmitLog("Falha ao iniciar leitura.\r\n");
                return false;
            }

            _isReading = true;
            EmitLog("StartRead OK.\r\n");

            // ✅ timer em background
            _pollTimer = new Timer(_ => PollOnce(), null, 0, _pollIntervalMs);

            return true;
        }

        public void StopReading()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;

            if (!_isReading) return;

            try
            {
                lock (HidLock)
                    SWHidApi.SWHid_StopRead(DEV);
            }
            catch { }

            _isReading = false;
            EmitLog("StopRead OK.\r\n");
        }

        public void Close()
        {
            StopReading();
            if (!_isOpen) return;

            try
            {
                lock (HidLock)
                    SWHidApi.SWHid_CloseDevice(); // pode ser global na DLL, mas aqui já paramos leitura antes
            }
            catch { }

            _isOpen = false;
            EmitLog("HID fechado.\r\n");
        }

        public void ResetHard()
        {
            // ordem importa
            try { StopReading(); } catch { }
            try { Close(); } catch { }

            ReaderSn = "";
            ReaderIndex = -1;
        }

        private void PollOnce()
        {
            if (!_isOpen || !_isReading) return;

            int len, tagCount;

            try
            {
                lock (HidLock)
                    SWHidApi.SWHid_GetTagBuf(_tagBuf, out len, out tagCount);
            }
            catch (Exception ex)
            {
                EmitLog("Erro GetTagBuf: " + ex.Message + "\r\n");
                return;
            }

            if (tagCount > 0 && len > 0)
                ParseTags(_tagBuf, len, tagCount);
        }

        private void ParseTags(byte[] buf, int totalLen, int tagCount)
        {
            int offset = 0;

            for (int t = 0; t < tagCount; t++)
            {
                if (offset >= totalLen) break;

                int packLen = buf[offset];
                if (packLen <= 0) break;

                int bytesInPack = packLen + 1;
                if (offset + bytesInPack > totalLen) break;

                byte type = buf[1 + offset + 0];
                byte ant = buf[1 + offset + 1];

                int idLen = ((type & 0x80) == 0x80) ? (packLen - 7) : (packLen - 1);
                if (idLen < 3) { offset += bytesInPack; continue; }

                var epcSb = new StringBuilder(64);
                for (int i = 2; i < idLen; i++)
                    epcSb.Append(buf[1 + offset + i].ToString("X2"));

                string epc = epcSb.ToString();
                if (epc.Length > 24) epc = epc.Substring(0, 24);
                if (string.IsNullOrEmpty(epc)) { offset += bytesInPack; continue; }

                byte rssi = 0x00;
                int rssiIdx = 1 + offset + idLen;
                if (rssiIdx >= 0 && rssiIdx < totalLen) rssi = buf[rssiIdx];

                if (rssi == 0x00)
                {
                    byte last = buf[offset + packLen];
                    if (last != 0x00) rssi = last;
                }

                TagReceived?.Invoke(new TagRead
                {
                    ReaderIndex = this.ReaderIndex,
                    ReaderSn = this.ReaderSn,
                    Epc = epc,
                    Antenna = ant,
                    RssiHex = rssi.ToString("X2"),
                    Timestamp = DateTime.Now
                });

                offset += bytesInPack;
            }
        }

        private void EmitLog(string msg) => Log?.Invoke(msg);

        public void Dispose()
        {
            try { ResetHard(); } catch { }
            _pollTimer?.Dispose();
            _pollTimer = null;
        }
    }
}
