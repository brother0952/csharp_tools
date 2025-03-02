using System;
using System.IO;
using Newtonsoft.Json;

namespace KimiChat
{
    public class ConfigManager
    {
        private const string CONFIG_FILE = "config.json";
        private Config _config;

        public ConfigManager()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            if (File.Exists(CONFIG_FILE))
            {
                string json = File.ReadAllText(CONFIG_FILE);
                _config = JsonConvert.DeserializeObject<Config>(json);
            }
            else
            {
                _config = new Config();
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(CONFIG_FILE, json);
        }

        public string GetApiKey()
        {
            return _config?.ApiKey;
        }

        public void SetApiKey(string apiKey)
        {
            _config.ApiKey = apiKey;
            SaveConfig();
        }
    }

    public class Config
    {
        public string ApiKey { get; set; }
    }
} 