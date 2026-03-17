namespace RfidRastroVerde.Models_Proj
{
    public class LocalSettings
    {
        public string Cliente { get; set; } = "Khrin Viveiros";
        public string Zona { get; set; } = "Zona A";
        public string Setor { get; set; } = "Setor 3";
        public int MetaPorBandeja { get; set; } = 165;
        public int IdleTimeoutSegundos { get; set; } = 20;

        public bool CapturaObrigatoria { get; set; } = true;
        public bool EnvioAutomatico { get; set; } = true;

        public string ApiBaseUrl { get; set; } = "https://api.rastroverde.com/";
        public bool CameraHabilitada { get; set; } = true;
        public bool LeitorHabilitado { get; set; } = true;
    }
}