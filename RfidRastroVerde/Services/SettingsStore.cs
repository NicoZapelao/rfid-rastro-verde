using System;
using System.IO;
using Newtonsoft.Json;
using RfidRastroVerde.Models_Proj;

namespace RfidRastroVerde.Services
{
    public class SettingsStore
    {
        private readonly string _filePath;

        public SettingsStore()
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RfidRastroVerde");

            Directory.CreateDirectory(baseDir);
            _filePath = Path.Combine(baseDir, "local-settings.json");
        }

        public LocalSettings Load()
        {
            if (!File.Exists(_filePath))
            {
                var defaults = new LocalSettings();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<LocalSettings>(json) ?? new LocalSettings();
        }

        public void Save(LocalSettings settings)
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }
    }
}