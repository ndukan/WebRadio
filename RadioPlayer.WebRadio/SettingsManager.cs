using System.Text.Json;
using System.IO;
using System.Reflection;

namespace RadioPlayer.WebRadio
{
    public static class SettingsManager
    {
        private static readonly string SettingsPath =
            Path.Combine(GetAppDirectory(), "appsettings.json");

        private static string GetAppDirectory()
        {
            // Vraća folder gde se .exe nalazi
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
        }

        public static AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }

            return new AppSettings();
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsPath, json);

                Console.WriteLine($"Settings saved to: {SettingsPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }



        // POMOĆNE METODE ZA FAVORITE
        public static void AddFavoriteStation(AppSettings settings, string stationName)
        {
            if (!settings.FavoriteStations.Contains(stationName))
            {
                settings.FavoriteStations.Add(stationName);
                SaveSettings(settings);
            }
        }

        public static void RemoveFavoriteStation(AppSettings settings, string stationName)
        {
            settings.FavoriteStations.Remove(stationName);
            SaveSettings(settings);
        }

        public static bool IsFavoriteStation(AppSettings settings, string stationName)
        {
            return settings.FavoriteStations.Contains(stationName);
        }


    }
}