using Newtonsoft.Json;
using RfidRastroVerde.Api;
using RfidRastroVerde.API;
using RfidRastroVerde.Driver_Proj;
using RfidRastroVerde.Models_Proj;
using RfidRastroVerde.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace RfidRastroVerde
{
    public partial class Form1 : Form
    {
        // -----------------------------
        // Manager (multi-leitor)
        // -----------------------------
        private readonly RfidReaderManager _mgr = new RfidReaderManager();
        private ContextMenuStrip _exportMenu;

        // -----------------------------
        // LOG: fila + flush em timer (evita travar UI)
        // -----------------------------
        private readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private readonly System.Windows.Forms.Timer _logFlushTimer = new System.Windows.Forms.Timer();

        private readonly List<string> _logHistory = new List<string>(50000);
        private readonly object _historyLock = new object();

        private const int UiMaxChars = 250_000;
        private const int HistoryMaxLines = 200_000;

        // -----------------------------
        // GRID: lista + índice por chave (ReaderIndex|EPC)
        // -----------------------------
        private readonly BindingList<TagRow> _gridList = new BindingList<TagRow>();
        private readonly Dictionary<string, TagRow> _gridIndex =
            new Dictionary<string, TagRow>(StringComparer.OrdinalIgnoreCase);

        // -----------------------------
        // UI BATCH: evita travar WinForms com update por tag
        // -----------------------------
        private readonly ConcurrentQueue<TagRead> _uiTagQueue = new ConcurrentQueue<TagRead>();
        private readonly System.Windows.Forms.Timer _uiFlushTimer = new System.Windows.Forms.Timer();
        private const int UiFlushIntervalMs = 250;      // 4x por segundo
        private const int UiMaxTagsPerFlush = 800;      // limite por flush
        private const int ProgressLogEveryTags = 25;    // log de progresso a cada N tags únicas
        private volatile int _lastProgressLogged = 0;
        private const bool VerbosePerTagLog = false;    // ligar só para depuração pesada
        private int _lastTotalUniqueLogged = 0;

        // -----------------------------
        // API
        // -----------------------------
        private ApiQueue _apiQueue;
        private ApiConfig _apiCfg;

        // -----------------------------
        // SCANNER BANDEJA (keyboard wedge)
        // -----------------------------
        private readonly StringBuilder _trayBuffer = new StringBuilder(128);

        private readonly object _trayLock = new object();
        private TraySession _currentSession = null;

        private DateTime _lastTrayScanAt = DateTime.MinValue;
        private static readonly TimeSpan TrayScanDebounce = TimeSpan.FromMilliseconds(350);

        // Idle: encerra após 20s sem TAG
        private System.Threading.Timer _idleTimer;
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(20); // TODO: tornar configurável por cliente/zona
        private volatile int _lastTagTick = 0; // Environment.TickCount

        // scan novo invalida sessões antigas
        private int _traySessionId = 0;
        private TraySession _lastFinishedSession = null;
        
        // API front
        private LocalApiServer _localApi;
        private SettingsStore _settingsStore;
        private LocalSettings _settings;
        private readonly OperationState _operationState = new OperationState();
        private DateTime? _sessionStartedUtc;

        public Form1()
        {
            InitializeComponent();

            InitLogSystem();
            InitGrid();
            InitExportMenu();

            // UI flush (batch)
            _uiFlushTimer.Interval = UiFlushIntervalMs;
            _uiFlushTimer.Tick += (s, e) => FlushUiTagsToGrid();
            _uiFlushTimer.Start();

            _mgr.Log += DriverLog;
            _mgr.TagUnique += DriverTag;

            // eventos bip bandeja
            txtTrayScan.KeyPress += TxtTrayScan_KeyPress;
            txtTrayScan.KeyDown += TxtTrayScan_KeyDown;
            txtTrayScan.Focus();

            // estado inicial
            btnStop.Enabled = false;
            btnExport.Enabled = false;

            // botão clear sempre ativo
            btnClear.Enabled = true;

            // primeira atualização visual
            RefreshUiState();

            txtLog.WordWrap = false;
            txtLog.ScrollBars = ScrollBars.Vertical;

            gridTags.CellDoubleClick += gridTags_CellDoubleClick;

            InitApi();
            InitializeLocalServices();

            // timer de idle (check a cada 250ms)
            _idleTimer = new System.Threading.Timer(_ => IdleTimerTick(), null, 250, 250);
        }

        // =========================================================
        // LOCAL API (para dashboard local em Node.js ou outro cliente)
        // =========================================================
        private void InitializeLocalServices()
        {
            _settingsStore = new SettingsStore();
            _settings = _settingsStore.Load();

            ApplySettingsToOperationState();
                _localApi = new LocalApiServer(
                getStatus: () => BuildStatus(),
                getCurrentSession: () => BuildCurrentSession(),
                getSettings: () => _settings,
                updateSettings: payload => UpdateSettingsFromApi(payload),
                startSession: bandeja => StartSessionFromApi(bandeja),
                resetSession: () => ResetSessionFromApi(),
                capturePhoto: () => CapturePhotoFromApi()
            );

            _localApi.Start("http://localhost:8085/");  
        }

        private void ApplySettingsToOperationState()
        {
            _operationState.Cliente = _settings.Cliente;
            _operationState.Zona = _settings.Zona;
            _operationState.Setor = _settings.Setor;
            _operationState.Meta = _settings.MetaPorBandeja;
            _operationState.UltimaAtualizacaoUtc = DateTime.UtcNow;
        }

        private object BuildStatus()
        {
            _operationState.UltimaAtualizacaoUtc = DateTime.UtcNow;

            return new
            {
                appStatus = "running",
                readerStatus = _operationState.LeitorStatus,
                cameraStatus = _operationState.CameraStatus,
                apiStatus = _operationState.StatusApi,
                filaEnvio = _operationState.FilaEnvio,
                emSessao = _operationState.EmSessao,
                ultimaAtualizacao = _operationState.UltimaAtualizacaoUtc
            };
        }

        private object BuildCurrentSession()
        {
            if (_sessionStartedUtc.HasValue)
                _operationState.TempoSessao = (int)(DateTime.UtcNow - _sessionStartedUtc.Value).TotalSeconds;
            else 
                _operationState.TempoSessao = 0;

            _operationState.UltimaAtualizacaoUtc = DateTime.UtcNow;
            return _operationState;
        }

        private void UpdateSettingsFromApi(dynamic payload)
        {
            _settings.Cliente = payload?.cliente != null ? (string)payload.cliente : _settings.Cliente;
            _settings.Zona = payload?.zona != null ? (string)payload.zona : _settings.Zona;
            _settings.Setor = payload?.setor != null ? (string)payload.setor : _settings.Setor;
            _settings.MetaPorBandeja = payload?.meta != null ? (int)payload.meta : _settings.MetaPorBandeja;
            _settings.ApiBaseUrl = payload?.apiBaseUrl != null ? (string)payload.apiBaseUrl : _settings.ApiBaseUrl;

            _settingsStore.Save(_settings);
            ApplySettingsToOperationState();
        }

        private void StartSessionFromApi(string bandeja)
        {
            if (!string.IsNullOrWhiteSpace(bandeja))
                _operationState.Bandeja = bandeja;

            _operationState.Meta = _settings.MetaPorBandeja;
            _operationState.Lidas = 0;
            _operationState.StatusLeitura = "Em andamento";
            _operationState.StatusApi = "Aguardando envio";
            _operationState.EmSessao = true;
            _operationState.FotoCapturada = false;
            _operationState.NomeFoto = "";
            _operationState.CameraStatus = "Pronta";
            _operationState.FilaEnvio = 0;
            _sessionStartedUtc = DateTime.UtcNow;

            _mgr.StartGlobalFresh(_settings.MetaPorBandeja);
        }

        private void ResetSessionFromApi()
        {
            _operationState.Lidas = 0;
            _operationState.StatusLeitura = "Aguardando bandeja";
            _operationState.StatusApi = "Idle";
            _operationState.EmSessao = false;
            _operationState.FotoCapturada = false;
            _operationState.NomeFoto = "";
            _operationState.CameraStatus = "Pronta";
            _operationState.FilaEnvio = 0;
            _sessionStartedUtc = null;
        }

        private string CapturePhotoFromApi()
        {
            var fileName = $"{(_operationState.Bandeja ?? "sem_bandeja")}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";

            _operationState.FotoCapturada = true;
            _operationState.NomeFoto = fileName;
            _operationState.CameraStatus = "Foto capturada";
            _operationState.UltimaAtualizacaoUtc = DateTime.UtcNow;

            return fileName;
        }

        private void InitApi()
        {
            try
            {
                _apiCfg = new ApiConfig();

                if (_apiCfg.Enabled && !string.IsNullOrWhiteSpace(_apiCfg.BaseUrl))
                {
                    _apiQueue = new ApiQueue(new ApiClient(_apiCfg));
                    _apiQueue.Log += (msg) => Log("[API] " + msg + "\r\n");
                    Log("API OK: " + _apiCfg.BaseUrl + "\r\n");
                }
                else
                {
                    _apiQueue = null;
                    Log("API desativada.\r\n");
                }
            }
            catch (Exception ex)
            {
                Log("Falha InitApi: " + ex.Message + "\r\n");
            }
        }

        // =========================================================
        // LOG SYSTEM
        // =========================================================
        private void InitLogSystem()
        {
            _logFlushTimer.Interval = 100;
            _logFlushTimer.Tick += (s, e) => FlushLogToUi();
            _logFlushTimer.Start();
        }

        private void ResetUiForNewCycle(string header = null)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ResetUiForNewCycle(header)));
                return;
            }

            _gridIndex.Clear();
            _gridList.Clear();

            _lastTotalUniqueLogged = 0;
            Log("\r\n=============== " + (header ?? "NOVO CICLO") + " ===============\r\n");
        }

        private void Log(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            lock (_historyLock)
            {
                _logHistory.Add(text);
                if (_logHistory.Count > HistoryMaxLines)
                    _logHistory.RemoveRange(0, 5000);
            }

            _logQueue.Enqueue(text);
        }

        private void FlushLogToUi()
        {
            if (_logQueue.IsEmpty) return;

            var sb = new StringBuilder(8192);
            while (_logQueue.TryDequeue(out var s))
                sb.Append(s);

            if (sb.Length == 0) return;

            if (txtLog.TextLength > UiMaxChars)
            {
                txtLog.Text = txtLog.Text.Substring(txtLog.TextLength - (UiMaxChars / 2));
                txtLog.SelectionStart = txtLog.TextLength;
                txtLog.ScrollToCaret();
            }

            txtLog.AppendText(sb.ToString());
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        private string GetFullLog()
        {
            lock (_historyLock)
            {
                var sb = new StringBuilder(Math.Min(_logHistory.Count * 32, 5_000_000));
                for (int i = 0; i < _logHistory.Count; i++)
                    sb.Append(_logHistory[i]);
                return sb.ToString();
            }
        }

        // =========================================================
        // GRID
        // =========================================================
        public sealed class TagRow
        {
            public int ReaderIndex { get; set; }
            public string ReaderSn { get; set; }

            public string EPC { get; set; }
            public int Ant { get; set; }
            public string RSSI { get; set; }
            public int SeenCount { get; set; }
            public DateTime FirstSeen { get; set; }
            public DateTime LastSeen { get; set; }
        }

        private void InitGrid()
        {
            gridTags.AutoGenerateColumns = false;
            gridTags.DataSource = _gridList;

            gridTags.Columns.Clear();

            gridTags.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReaderIndex", HeaderText = "Leitor", Width = 70 });
            gridTags.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReaderSn", HeaderText = "Nº Série do Leitor", Width = 160 });
            gridTags.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EPC", HeaderText = "Código da Tag (EPC)", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            gridTags.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Ant", HeaderText = "Antena", Width = 75 });
            gridTags.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RSSI", HeaderText = "Sinal (RSSI)", Width = 105 });
            gridTags.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SeenCount", HeaderText = "Leituras", Width = 80 });
            gridTags.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FirstSeen", HeaderText = "Primeira Leitura", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Format = "HH:mm:ss.fff" } });
            gridTags.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastSeen", HeaderText = "Última Leitura", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Format = "HH:mm:ss.fff" } });

            gridTags.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridTags.MultiSelect = false;
            gridTags.ReadOnly = true;
            gridTags.AllowUserToAddRows = false;
            gridTags.AllowUserToDeleteRows = false;
            gridTags.RowHeadersVisible = false;
        }

        private void UpsertGridRow(TagRead tag)
        {
            string key = tag.ReaderIndex.ToString() + "|" + tag.Epc;

            if (_gridIndex.TryGetValue(key, out var row))
            {
                row.LastSeen = DateTime.Now;
                row.SeenCount += 1;
                row.Ant = tag.Antenna;

                int rssiDec = 0;
                try { rssiDec = Convert.ToInt32(tag.RssiHex, 16); } catch { }
                row.RSSI = rssiDec + " (0x" + tag.RssiHex + ")";

                int idx = _gridList.IndexOf(row);
                if (idx >= 0) _gridList.ResetItem(idx);
            }
            else
            {
                int rssiDec = 0;
                try { rssiDec = Convert.ToInt32(tag.RssiHex, 16); } catch { }

                row = new TagRow
                {
                    ReaderIndex = tag.ReaderIndex,
                    ReaderSn = tag.ReaderSn ?? "",
                    EPC = tag.Epc,
                    Ant = tag.Antenna,
                    RSSI = rssiDec + " (0x" + tag.RssiHex + ")",
                    SeenCount = 1,
                    FirstSeen = DateTime.Now,
                    LastSeen = DateTime.Now
                };

                _gridIndex[key] = row;
                _gridList.Add(row);
            }
        }

        // =========================================================
        // UI EVENTS
        // =========================================================
        private void btnOpen_Click(object sender, EventArgs e)
        {
            _mgr.OpenAllDetected(pollIntervalMs: 20);

            if (_mgr.Drivers.Count > 0)
            {
                Log("Open OK (" + _mgr.Drivers.Count + " leitores).\r\n");
            }
            else
            {
                Log("Open FAIL (0 leitores).\r\n");
            }

            RefreshUiState();
            txtTrayScan.Focus();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            EndCurrentTraySession(TrayEndReason.ManualStop);
            Log("[UI] Sessão encerrada (ManualStop).\r\n");
            txtTrayScan.Focus();
            RefreshUiState();
        }

        private void gridTags_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = _gridList[e.RowIndex];
            Clipboard.SetText(row.EPC);
            Log("EPC copiado: " + row.EPC + "\r\n");
        }

        // =========================================================
        // SCANNER BANDEJA
        // =========================================================
        private void TxtTrayScan_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar))
                _trayBuffer.Append(e.KeyChar);
        }

        private void TxtTrayScan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Tab) return;

            string trayCode = _trayBuffer.ToString().Trim();
            _trayBuffer.Clear();

            if (!string.IsNullOrWhiteSpace(trayCode))
                _ = OnTrayScannedAsync(trayCode);

            e.Handled = true;
            e.SuppressKeyPress = true;
            txtTrayScan.Focus();
        }

        private async Task OnTrayScannedAsync(string trayEpc)
        {
            var now = DateTime.UtcNow;
            if (now - _lastTrayScanAt < TrayScanDebounce)
                return;
            _lastTrayScanAt = now;

            if (_mgr.Drivers.Count == 0)
            {
                Log("[TRAY] Nenhum leitor aberto. Clique em Open.\r\n");
                return;
            }

            // encerra anterior como incompleta
            EndCurrentTraySession(TrayEndReason.NewTrayScanned);

            int myId = Interlocked.Increment(ref _traySessionId);

            var session = new TraySession
            {
                TrayEpc = trayEpc,
                StartedAt = DateTime.Now,
                LastTagAt = DateTime.Now,
                EndedAt = null,
                EndReason = null,
                Incomplete = false
            };

            lock (_trayLock)
                _currentSession = session;

            _lastTagTick = Environment.TickCount;

            ResetUiForNewCycle("BANDEJA " + trayEpc);
            Log($"[TRAY] Bandeja escaneada: {trayEpc}\r\n");
            Log("[TRAY] Iniciando leitura... (finaliza por 20s sem tag ou nova bandeja)\r\n");

            _mgr.ResetGlobal();
            _mgr.StartGlobalFresh(_settings.MetaPorBandeja);

            _operationState.Bandeja = trayEpc;
            _operationState.Meta = _settings.MetaPorBandeja;
            _operationState.Lidas = 0;
            _operationState.StatusLeitura = "Em andamento";
            _operationState.StatusApi = "Aguardando envio";
            _operationState.EmSessao = true;
            _operationState.FilaEnvio = 0;
            _sessionStartedUtc = DateTime.UtcNow;

            RefreshUiState();

            await Task.Delay(1);
            if (myId != _traySessionId) return;

            txtTrayScan.Focus();
        }

        private void IdleTimerTick()
        {
            if (IsDisposed || !IsHandleCreated) return;

            TraySession session;
            lock (_trayLock) session = _currentSession;

            if (session == null) return;
            if (!_mgr.IsRunning) return;

            int sinceLast = Environment.TickCount - _lastTagTick;
            if (sinceLast < 0) sinceLast = int.MaxValue;

            if (sinceLast >= (int)IdleTimeout.TotalMilliseconds)
            {
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        EndCurrentTraySession(TrayEndReason.IdleTimeout);
                    }));
                }
                catch { /* ignore: closing */ }
            }
        }

        private void EndCurrentTraySession(TrayEndReason reason)
        {
            TraySession snap = null;

            lock (_trayLock)
            {
                if (_currentSession == null) return;
                if (_currentSession.EndedAt.HasValue) return;

                _currentSession.EndedAt = DateTime.Now;
                _currentSession.EndReason = reason;
                _currentSession.Incomplete = (reason == TrayEndReason.NewTrayScanned);

                // snapshot (pode ser só referência, mas daqui pra frente não vamos mais editar)
                snap = _currentSession;

                // zera sessão atual para não receber mais tags nessa estrutura
                _currentSession = null;

                // guarda última finalizada para export
                _lastFinishedSession = snap;
            }

            // parar leitura fora do lock
            _mgr.StopAll();

            Log($"[TRAY] Sessão encerrada: {snap.TrayEpc} | Motivo: {reason} | Únicas: {snap.UniqueTagsCount}\r\n");

            _ = ProcessTrayResultAsync(snap);

            RefreshUiState();
        }

        private async Task ProcessTrayResultAsync(TraySession session)
        {
            try
            {
                // Monta snapshot completo (1 request por bandeja)
                var snapshot = TraySnapshotDto.FromSession(session, _apiCfg?.DeviceId);
                _operationState.StatusLeitura = "Finalizando";
                _operationState.StatusApi = "Enviando snapshot";
                _operationState.FilaEnvio = 1;

                // log enxuto (JSON completo pode ser grande e matar desempenho)
                Log($"[TRAY] Snapshot pronto: Tray={snapshot.TrayEpc} | Itens={snapshot.UniqueItemCount} | Incomplete={snapshot.Incomplete} | Reason={snapshot.EndReason}\r\n");

                // Salva JSON em disco (útil para auditoria e reenvio)
                try
                {
                    var jsonCompact = JsonConvert.SerializeObject(snapshot);
                    SaveSnapshotLocal(snapshot.SnapshotId, jsonCompact);
                }
                catch { /* não falhar por causa de log/arquivo */ }

                // API: envia snapshot na fila (assíncrono)
                if (_apiQueue != null && _apiCfg != null && _apiCfg.Enabled)
                {
                    _apiQueue.EnqueueSnapshot(snapshot);
                    _operationState.StatusApi = "Snapshot enfileirado";
                    _operationState.FilaEnvio = 1;
                }
                else
                {
                    _operationState.StatusApi = "API desativada";
                    _operationState.FilaEnvio = 0;
                }
                
                _operationState.StatusLeitura = "Concluída";
                _operationState.EmSessao = false;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _operationState.StatusApi = "Falha no envio";
                _operationState.StatusLeitura = "Concluída com pendência";
                _operationState.FilaEnvio = 1;
                Log("[TRAY] Erro ProcessTrayResultAsync: " + ex.Message + "\r\n");
            }
        }

        private void SaveSnapshotLocal(string snapshotId, string json)
        {
            // grava em %AppData%\RfidRastroVerde\snapshots\
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RfidRastroVerde", "snapshots");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd_HHmmss}_{snapshotId}.json");
            File.WriteAllText(file, json, Encoding.UTF8);
        }

        // =========================================================
        // DRIVER EVENTS (TagUnique do Manager)
        // =========================================================
        private void DriverLog(string msg) => Log(msg);

        private void DriverTag(TagRead tag)
        {
            _lastTagTick = Environment.TickCount;

            bool isTrayEpc = false;
            bool accepted = false;

            lock (_trayLock)
            {
                if (_currentSession == null) return;

                if (!string.IsNullOrEmpty(_currentSession.TrayEpc) &&
                    string.Equals(tag.Epc, _currentSession.TrayEpc, StringComparison.OrdinalIgnoreCase))
                {
                    isTrayEpc = true;
                }
                else
                {
                    _currentSession.LastTagAt = DateTime.Now;

                    if (!_currentSession.Tags.TryGetValue(tag.Epc, out var row))
                    {
                        row = new TrayTagRow
                        {
                            Epc = tag.Epc,
                            SeenCount = 0,
                            FirstSeen = DateTime.Now,
                            LastSeen = DateTime.Now
                        };
                        _currentSession.Tags[tag.Epc] = row;
                    }

                    row.SeenCount += 1;
                    row.LastSeen = DateTime.Now;
                    row.LastReaderIndex = tag.ReaderIndex;
                    row.LastReaderSn = tag.ReaderSn ?? "";
                    row.LastAntenna = tag.Antenna;
                    row.LastRssiHex = tag.RssiHex ?? "";

                    accepted = true;
                }
            }

            if (isTrayEpc)
            {
                Log($"[TRAY] EPC da bandeja visto no RFID: {tag.Epc}\r\n");
                return;
            }
            if (!accepted) return;

            
            // UI: não atualiza grid/log a cada tag (isso derruba performance). Faz batch no timer.
            _uiTagQueue.Enqueue(tag);

            // log detalhado por tag só em modo debug pesado
            if (VerbosePerTagLog)
            Log("[R" + tag.ReaderIndex + "] " + tag.ToString() + " SN=" + (tag.ReaderSn ?? "") + "\r\n");

            // log de progresso (a cada N tags únicas na sessão)
            int uniqueNow;
            lock (_trayLock)
            {
                uniqueNow = _currentSession != null ? _currentSession.UniqueTagsCount : 0;
            }
            _operationState.Lidas = uniqueNow;
            _operationState.LeitorStatus = "Conectado";
            _operationState.UltimaAtualizacaoUtc = DateTime.UtcNow;

            if (uniqueNow > 0 && uniqueNow - _lastProgressLogged >= ProgressLogEveryTags)
            {
                _lastProgressLogged = uniqueNow;
                Log($"[TRAY] Progresso: {uniqueNow} tags únicas\r\n");
            }

            // API tag-a-tag (opcional; pode atrapalhar desempenho em rede)
            if (_apiQueue != null && _apiCfg != null && _apiCfg.SendTagEvents)
            {
                _apiQueue.Enqueue(new TagReadDto
                {
                    Epc = tag.Epc,
                    ReaderIndex = tag.ReaderIndex,
                    ReaderSn = tag.ReaderSn ?? "",
                    Antenna = tag.Antenna,
                    RssiHex = tag.RssiHex,
                    DeviceId = _apiCfg.DeviceId,
                    Timestamp = tag.Timestamp
                });
            }
        }  

        // =========================================================
        // UI BATCH FLUSH
        // =========================================================
        private void FlushUiTagsToGrid()
        {
            if (_uiTagQueue.IsEmpty) return;
            if (!IsHandleCreated) return;

            // Faz 1 BeginInvoke por flush (em vez de 1 por tag)
            BeginInvoke(new Action(() =>
            {
                int processed = 0;
                while (processed < UiMaxTagsPerFlush && _uiTagQueue.TryDequeue(out var tag))
                {
                    UpsertGridRow(tag);
                    processed++;
                }
            }));
        }

        // =========================================================
        // EXPORT (mantém pelo GRID como estava)
        // =========================================================
        private void ExportXml()
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "XML (*.xml)|*.xml";
                sfd.Title = "Salvar relatório RFID (XML)";
                sfd.FileName = "rfid_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xml";

                if (sfd.ShowDialog() != DialogResult.OK) return;

                var readers = _gridList
                    .GroupBy(r => new { r.ReaderIndex, r.ReaderSn })
                    .OrderBy(g => g.Key.ReaderIndex)
                    .ToList();

                TraySession session;
                lock (_trayLock) session = _lastFinishedSession;

                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    new XElement("RfidReadReport",
                        new XElement("Meta",
                            new XElement("GeneratedAt", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff")),
                            new XElement("TotalRows", _gridList.Count),
                            new XElement("TotalReaders", readers.Count),
                            session != null ? new XElement("TrayEpc", session.TrayEpc ?? "") : null,
                            session != null ? new XElement("TrayUniqueTags", session.UniqueTagsCount) : null,
                            session != null && session.EndReason.HasValue ? new XElement("TrayEndReason", session.EndReason.Value.ToString()) : null,
                            session != null ? new XElement("TrayIncomplete", session.Incomplete) : null
                        ),
                        new XElement("Readers",
                            readers.Select(g =>
                                new XElement("Reader",
                                    new XAttribute("index", g.Key.ReaderIndex),
                                    new XAttribute("sn", g.Key.ReaderSn ?? ""),
                                    new XAttribute("uniqueTags", g.Count()),
                                    new XElement("Tags",
                                        g.Select(row =>
                                            new XElement("Tag",
                                                new XElement("Epc", row.EPC),
                                                new XElement("Antenna", row.Ant),
                                                new XElement("Rssi", row.RSSI ?? ""),
                                                new XElement("SeenCount", row.SeenCount),
                                                new XElement("FirstSeen", row.FirstSeen.ToString("yyyy-MM-ddTHH:mm:ss.fff")),
                                                new XElement("LastSeen", row.LastSeen.ToString("yyyy-MM-ddTHH:mm:ss.fff"))
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                );

                doc.Save(sfd.FileName);
                Log("XML exportado: " + sfd.FileName + "\r\n");
            }
        }

        private void ExportJson()
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "JSON (*.json)|*.json";
                sfd.Title = "Salvar relatório RFID (JSON)";
                sfd.FileName = "rfid_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";

                if (sfd.ShowDialog() != DialogResult.OK) return;

                TraySession session;
                lock (_trayLock) session = _lastFinishedSession;

                var readers = _gridList
                    .GroupBy(r => new { r.ReaderIndex, r.ReaderSn })
                    .OrderBy(g => g.Key.ReaderIndex)
                    .Select(g => new
                    {
                        index = g.Key.ReaderIndex,
                        sn = g.Key.ReaderSn ?? "",
                        uniqueTags = g.Count(),
                        tags = g.Select(row => new
                        {
                            epc = row.EPC,
                            antenna = row.Ant,
                            rssiText = row.RSSI,
                            seenCount = row.SeenCount,
                            firstSeen = row.FirstSeen.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                            lastSeen = row.LastSeen.ToString("yyyy-MM-ddTHH:mm:ss.fff")
                        }).ToList()
                    })
                    .ToList();

                var report = new
                {
                    meta = new
                    {
                        generatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                        totalRows = _gridList.Count,
                        totalReaders = readers.Count,
                        trayEpc = session?.TrayEpc,
                        trayUniqueTags = session?.UniqueTagsCount ?? 0,
                        trayEndReason = session?.EndReason?.ToString(),
                        trayIncomplete = session?.Incomplete ?? false
                    },
                    readers
                };

                var json = JsonConvert.SerializeObject(report, Formatting.Indented);
                File.WriteAllText(sfd.FileName, json, Encoding.UTF8);
                Log("JSON exportado: " + sfd.FileName + "\r\n");
            }
        }

        private void InitExportMenu()
        {
            _exportMenu = new ContextMenuStrip();

            var itemXml = new ToolStripMenuItem("Exportar XML");
            itemXml.Click += (s, e) => ExportXml();

            var itemJson = new ToolStripMenuItem("Exportar JSON");
            itemJson.Click += (s, e) => ExportJson();

            var itemBoth = new ToolStripMenuItem("Exportar Ambos (XML + JSON)");
            itemBoth.Click += (s, e) =>
            {
                ExportXml();
                ExportJson();
            };

            _exportMenu.Items.Add(itemXml);
            _exportMenu.Items.Add(itemJson);
            _exportMenu.Items.Add(new ToolStripSeparator());
            _exportMenu.Items.Add(itemBoth);
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            if (_lastFinishedSession == null)
            {
                Log("[EXPORT] Nenhuma sessão finalizada para exportar.\r\n");
                return;
            }

            // abre o menu abaixo do botão
            _exportMenu.Show(btnExport, new System.Drawing.Point(0, btnExport.Height));
        }

        private void RefreshUiState()
        {
            if (!IsHandleCreated) return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshUiState));
                return;
            }

            btnStop.Enabled = _mgr.IsRunning;
            btnExport.Enabled = (_lastFinishedSession != null);
            btnOpen.Enabled = (_mgr.Drivers.Count == 0);
            btnClear.Enabled = true;
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            if (_mgr.IsRunning)
                EndCurrentTraySession(TrayEndReason.ManualStop);

            lock (_trayLock)
            {
                _currentSession = null;
                _lastFinishedSession = null;
            }

            _gridIndex.Clear();
            _gridList.Clear();

            // limpa log visível e histórico
            lock (_historyLock) _logHistory.Clear();
            while (_logQueue.TryDequeue(out _)) { }
            txtLog.Clear();

            RefreshUiState();
            txtTrayScan.Focus();

            Log("[UI] Limpeza concluída.\r\n");
        }

        // =========================================================
        // CLEANUP
        // =========================================================
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { EndCurrentTraySession(TrayEndReason.AppClosing); } catch { }
            try { _idleTimer?.Dispose(); } catch { }
            try { _logFlushTimer.Stop(); } catch { }
            try { _mgr.Dispose(); } catch { }
            try { _apiQueue?.Dispose(); } catch { }
            try { _localApi?.Stop(); } catch { }

            base.OnFormClosing(e);
        }
    }
}
