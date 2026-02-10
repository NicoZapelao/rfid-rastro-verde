using System;
using System.Configuration;

namespace RfidRastroVerde.API
{
    public sealed class ApiConfig
    {
        public string BaseUrl { get; set; } = "https://api.rastroverde.com/";
        public TimeSpan Timeout { get; }
        public string ApiKey { get; }
        public string DeviceId { get; }
        public bool Enabled { get; set; }

        public ApiConfig()
        {
            var cfgUrl = (ConfigurationManager.AppSettings["Api.BaseUrl"] ?? "").Trim();
            BaseUrl = string.IsNullOrWhiteSpace(cfgUrl) ? "https://api.rastroverde.com/" : cfgUrl;

            ApiKey = (ConfigurationManager.AppSettings["Api.ApiKey"] ?? "").Trim();
            DeviceId = (ConfigurationManager.AppSettings["Api.DeviceId"] ?? "").Trim();

            int sec;
            if (!int.TryParse(ConfigurationManager.AppSettings["Api.TimeoutSeconds"], out sec)) sec = 10;
            if (sec < 2) sec = 2;
            Timeout = TimeSpan.FromSeconds(sec);

            // liga automático quando tiver URL válida
            Enabled = !string.IsNullOrWhiteSpace(BaseUrl);
        }
    }
}
