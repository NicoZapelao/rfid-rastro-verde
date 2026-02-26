using System;
using System.Collections.Generic;

namespace RfidRastroVerde.Models_Proj
{
    public enum TrayEndReason
    {
        IdleTimeout,
        NewTrayScanned,
        ManualStop,
        AppClosing
    }

    public sealed class TrayTagRow
    {
        public string Epc { get; set; }

        public int LastReaderIndex { get; set; }
        public string LastReaderSn { get; set; }
        public int LastAntenna { get; set; }
        public string LastRssiHex { get; set; }

        public int SeenCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
    }

    public sealed class TraySession
    {
        public string TrayEpc { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime LastTagAt { get; set; }

        public DateTime? EndedAt { get; set; }
        public TrayEndReason? EndReason { get; set; }

        // true quando foi cortada por nova bandeja (ou encerrada incompleta)
        public bool Incomplete { get; set; }

        // EPC -> dados
        public Dictionary<string, TrayTagRow> Tags { get; } =
            new Dictionary<string, TrayTagRow>(StringComparer.OrdinalIgnoreCase);

        public int UniqueTagsCount => Tags.Count;
    }
}
