using System;

namespace RfidRastroVerde.Models_Proj
{
    public sealed class TagRead
    {
        public string Epc { get; set; }
        public byte Antenna { get; set; }
        public string RssiHex { get; set; }
        public DateTime Timestamp { get; set; }
        public int ReaderIndex { get; set; }
        public string ReaderSn { get; set; }

        public override string ToString()
        {
            return "EPC=" + Epc + " ANT=" + Antenna.ToString("X2") + " RSSI=" + RssiHex;
        }
    }
}
