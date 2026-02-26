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

        // endpoints/flags
        public string TagsEndpoint { get; set; } = "rfid/tags";
        public string SnapshotsEndpoint { get; set; } = "rfid/trays";
        public bool SendTagEvents { get; set; } = false; // default: só snapshot por bandeja

        public ApiConfig()
        {
            var cfgUrl = (ConfigurationManager.AppSettings["Api.BaseUrl"] ?? "").Trim();
            BaseUrl = string.IsNullOrWhiteSpace(cfgUrl) ? "https://api.rastroverde.com/" : cfgUrl;

            ApiKey = (ConfigurationManager.AppSettings["Api.ApiKey"] ?? "").Trim();
            DeviceId = (ConfigurationManager.AppSettings["Api.DeviceId"] ?? "").Trim();


TagsEndpoint = (ConfigurationManager.AppSettings["Api.TagsEndpoint"] ?? "rfid/tags").Trim().TrimStart('/');
SnapshotsEndpoint = (ConfigurationManager.AppSettings["Api.SnapshotsEndpoint"] ?? "rfid/trays").Trim().TrimStart('/');

var sendTagsStr = (ConfigurationManager.AppSettings["Api.SendTagEvents"] ?? "").Trim();
if (!string.IsNullOrEmpty(sendTagsStr))
{
    bool b;
    if (bool.TryParse(sendTagsStr, out b)) SendTagEvents = b;
}

            int sec;
            if (!int.TryParse(ConfigurationManager.AppSettings["Api.TimeoutSeconds"], out sec)) sec = 10;
            if (sec < 2) sec = 2;
            Timeout = TimeSpan.FromSeconds(sec);

            // liga automático quando tiver URL válida
            Enabled = !string.IsNullOrWhiteSpace(BaseUrl);
        }
    }
}
