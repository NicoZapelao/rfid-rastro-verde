using RfidRastroVerde.Models_Proj;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RfidRastroVerde.API
{
    public sealed class TraySnapshotDto
    {
        public string SnapshotId { get; set; } = Guid.NewGuid().ToString("N");

        public string DeviceId { get; set; }
        public string TrayEpc { get; set; }

        public string StartedAt { get; set; }
        public string LastTagAt { get; set; }
        public string EndedAt { get; set; }

        public string EndReason { get; set; }
        public bool Incomplete { get; set; }

        public int UniqueItemCount { get; set; }

        public List<TraySnapshotItemDto> Items { get; set; } = new List<TraySnapshotItemDto>();

        public static TraySnapshotDto FromSession(TraySession session, string deviceId)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            var dto = new TraySnapshotDto
            {
                DeviceId = deviceId ?? "",
                TrayEpc = session.TrayEpc ?? "",
                StartedAt = session.StartedAt.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                LastTagAt = session.LastTagAt.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                EndedAt = (session.EndedAt ?? DateTime.Now).ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                EndReason = session.EndReason?.ToString() ?? "",
                Incomplete = session.Incomplete,
                UniqueItemCount = session.UniqueTagsCount,
                Items = session.Tags.Values
                    .OrderByDescending(t => t.SeenCount)
                    .Select(t => new TraySnapshotItemDto
                    {
                        Epc = t.Epc,
                        SeenCount = t.SeenCount,
                        FirstSeen = t.FirstSeen.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                        LastSeen = t.LastSeen.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                        LastReaderIndex = t.LastReaderIndex,
                        LastReaderSn = t.LastReaderSn ?? "",
                        LastAntenna = t.LastAntenna,
                        LastRssiHex = t.LastRssiHex ?? ""
                    })
                    .ToList()
            };

            return dto;
        }
    }

    public sealed class TraySnapshotItemDto
    {
        public string Epc { get; set; }

        public int SeenCount { get; set; }

        public string FirstSeen { get; set; }
        public string LastSeen { get; set; }

        public int LastReaderIndex { get; set; }
        public string LastReaderSn { get; set; }

        public int LastAntenna { get; set; }
        public string LastRssiHex { get; set; }
    }
}
