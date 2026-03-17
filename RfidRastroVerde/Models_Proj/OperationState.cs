using System;

namespace RfidRastroVerde.Models_Proj
{
    public class OperationState
    {
        public string Cliente { get; set; } = "Khrin Viveiros";
        public string Zona { get; set; } = "Zona A";
        public string Setor { get; set; } = "Setor 3";

        public string Bandeja { get; set; } = "";
        public int Lidas { get; set; } = 0;
        public int Meta { get; set; } = 165;

        public string StatusLeitura { get; set; } = "Aguardando bandeja";
        public string StatusApi { get; set; } = "Idle";
        public string LeitorStatus { get; set; } = "Conectado";

        public int TempoSessao { get; set; } = 0;
        public int FilaEnvio { get; set; } = 0;

        public string CameraStatus { get; set; } = "Pronta";
        public bool FotoCapturada { get; set; } = false;
        public string NomeFoto { get; set; } = "";

        public bool EmSessao { get; set; } = false;

        public DateTime UltimaAtualizacaoUtc { get; set; } = DateTime.UtcNow;
    }
}