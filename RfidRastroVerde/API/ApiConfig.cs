using System;
using System.Configuration;

namespace RfidRastroVerde.API
{
    public sealed class ApiConfig
    {
        public string BaseUrl { get; }
        public TimeSpan Timeout { get; }
        public string ApiKey { get; }
        public string DeviceId { get; }
        public bool Enabled { get; set; } = false;

        public ApiConfig()
        {
            BaseUrl = (ConfigurationManager.AppSettings["Api.BaseUrl"] ?? "").Trim();
            ApiKey = (ConfigurationManager.AppSettings["Api.ApiKey"] ?? "").Trim();
            DeviceId = (ConfigurationManager.AppSettings["Api.DeviceId"] ?? "").Trim();

            int sec;
            if (!int.TryParse(ConfigurationManager.AppSettings["Api.TimeoutSeconds"], out sec)) sec = 10;
            if (sec < 2) sec = 2;

            Timeout = TimeSpan.FromSeconds(sec);
        }
    }
}
