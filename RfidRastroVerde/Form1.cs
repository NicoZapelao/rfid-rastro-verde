using RfidRastroVerde.Api;
using RfidRastroVerde.API;
using RfidRastroVerde.Driver_Proj;
using RfidRastroVerde.Models_Proj;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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

        private int _lastTotalUniqueLogged = 0;

        // -----------------------------
        // API
        // -----------------------------
        private ApiQueue _apiQueue;
        private ApiConfig _apiCfg;

        public Form1()
        {
            InitializeComponent();

            InitLogSystem();
            InitGrid();

            // eventos do manager
            _mgr.Log += DriverLog;
            _mgr.TagUnique += DriverTag;
            _mgr.GlobalFinished += OnGlobalFinished;
            _mgr.GlobalProgress += OnGlobalProgress;

            // estado inicial
            btnStart.Enabled = false;
            btnStop.Enabled = false;
            txtReadCount.Text = "10";

            // UX: textbox log
            txtLog.WordWrap = false;
            txtLog.ScrollBars = ScrollBars.Vertical;

            // grid
            gridTags.CellDoubleClick += gridTags_CellDoubleClick;

            // API
            InitApi();
        }

        private void OnGlobalProgress(int total)
        {
            //log econômico de progresso
            if (total != _lastTotalUniqueLogged && (total % 10 == 0))
            {
                _lastTotalUniqueLogged = total;
                Log("Progresso global: " + total + "/" + _mgr.TargetGlobal + "\r\n");
            }
        }

        private void OnGlobalFinished(int total)
        {
            Log("Ciclo global finalizado: " + total + " tags únicas.\r\n");
            if (IsHandleCreated)
                BeginInvoke(new Action(() => btnStart.Enabled = (_mgr.Drivers.Count > 0)));
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
            string s;

            while (_logQueue.TryDequeue(out s))
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

            gridTags.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReaderIndex",
                HeaderText = "Leitor",
                Width = 70
            });

            gridTags.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReaderSn",
                HeaderText = "Nº Série do Leitor",
                Width = 160
            });

            gridTags.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "EPC",
                HeaderText = "Código da Tag (EPC)",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            gridTags.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Ant",
                HeaderText = "Antena",
                Width = 75
            });

            gridTags.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "RSSI",
                HeaderText = "Sinal (RSSI)",
                Width = 105
            });

            gridTags.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "SeenCount",
                HeaderText = "Leituras",
                Width = 80
            });

            gridTags.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "FirstSeen",
                HeaderText = "Primeira Leitura",
                Width = 140,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "HH:mm:ss.fff" }
            });

            gridTags.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "LastSeen",
                HeaderText = "Última Leitura",
                Width = 140,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "HH:mm:ss.fff" }
            });

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

            TagRow row;
            if (_gridIndex.TryGetValue(key, out row))
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
            _mgr.OpenAllDetected(pollIntervalMs: 80);

            if (_mgr.Drivers.Count > 0)
            {
                btnOpen.Enabled = false;
                btnStart.Enabled = true;
                btnStop.Enabled = true;
                Log("Open OK (" + _mgr.Drivers.Count + " leitores).\r\n");
            }
            else
            {
                Log("Open FAIL (0 leitores).\r\n");
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            _gridIndex.Clear();
            _gridList.Clear();
            _lastTotalUniqueLogged = 0;

            int n;
            if (!int.TryParse(txtReadCount.Text, out n) || n <= 0)
            {
                Log("Quantidade inválida.\r\n");
                return;
            }

            _mgr.StartGlobalFresh(n);

            btnStart.Enabled = false;
            Log("Ciclo Global Iniciado: meta global = " + n + " tags únicas.\r\n");
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _mgr.StopAll();
            btnStart.Enabled = _mgr.Drivers.Count > 0;
            Log("StopAll OK.\r\n");
        }

        private void gridTags_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = _gridList[e.RowIndex];
            Clipboard.SetText(row.EPC);
            Log("EPC copiado: " + row.EPC + "\r\n");
        }

        // =========================================================
        // DRIVER EVENTS
        // =========================================================
        private void DriverLog(string msg) => Log(msg);

        private void DriverTag(TagRead tag)
        {
            Log("[R" + tag.ReaderIndex + "] " + tag.ToString() + " SN=" + (tag.ReaderSn ?? "") + "\r\n");

            if (IsHandleCreated)
                BeginInvoke(new Action(() => UpsertGridRow(tag)));

            if (_apiQueue != null && _apiCfg != null)
            {
                // IMPORTANTÍSSIMO: propriedades em PascalCase
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
        // EXPORTAR XML
        // =========================================================
        private void ExportXml(bool includeLog)
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

                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    new XElement("RfidReadReport",
                        new XElement("Meta",
                            new XElement("GeneratedAt", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff")),
                            new XElement("TotalRows", _gridList.Count),
                            new XElement("TotalReaders", readers.Count)
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
                        ),
                        includeLog ? new XElement("Log", new XCData(GetFullLog() ?? "")) : null
                    )
                );

                doc.Save(sfd.FileName);
                Log("XML exportado: " + sfd.FileName + "\r\n");
            }
        }

        private void btnExportXml_Click(object sender, EventArgs e)
        {
            ExportXml(includeLog: true);
        }

        // =========================================================
        // CLEANUP
        // =========================================================
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { _logFlushTimer.Stop(); } catch { }
            try { _mgr.Dispose(); } catch { }
            try { _apiQueue?.Dispose(); } catch { }
            base.OnFormClosing(e);
        }
    }
}
