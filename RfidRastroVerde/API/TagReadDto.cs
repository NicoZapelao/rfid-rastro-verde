using System;

namespace RfidRastroVerde.Api
{
    public sealed class TagReadDto
    {
        public string Epc { get; set; }
        public int ReaderIndex { get; set; }
        public string ReaderSn { get; set; }
        public int Antenna { get; set; }
        public string RssiHex { get; set; }
        public string DeviceId { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
