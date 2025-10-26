using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using RadioStream.Core.Models;

namespace RadioStream.Core.Services;

public class RadioStationManager
{
    private readonly string _dataFilePath;

    public ObservableCollection<RadioStation> Stations { get; } = new();
    public ObservableCollection<RadioStation> FavoriteStations { get; } = new();


    // Filter properties
    public string SearchText { get; set; } = string.Empty;
    public string SelectedGenre { get; set; } = "ALL";
    public string SelectedCountry { get; set; } = "ALL";

    // Filtered collections
    public ObservableCollection<RadioStation> FilteredStations { get; } = new();
    public ObservableCollection<RadioStation> FilteredFavorites { get; } = new();

    // Available filters
    public ObservableCollection<string> AvailableGenres { get; } = new();
    public ObservableCollection<string> AvailableCountries { get; } = new();



    public RadioStationManager()
    {
        // Postavi putanju do JSON fajla
        //_dataFilePath = Path.Combine(
        //    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        //    "RadioPlayer",
        //    "radiostations.json"
        //);

        _dataFilePath = Path.Combine(GetAppDirectory(), "radiostations.json");


        // Obezbedi da direktorijum postoji
        var directory = Path.GetDirectoryName(_dataFilePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        LoadStations();
        //UpdateAvailableFilters();
        //ApplyFilters();
    }

    private static string GetAppDirectory()
    {
        // Vraća folder gde se .exe nalazi
        return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
    }


    private void LoadStations()
    {
        try
        {
            if (File.Exists(_dataFilePath))
            {
                var json = File.ReadAllText(_dataFilePath);
                Debug.WriteLine($"📄 JSON file exists, size: {json.Length} chars");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                };

                var stations = JsonSerializer.Deserialize<List<RadioStation>>(json, options);

                if (stations != null)
                {
                    Stations.Clear();
                    FavoriteStations.Clear();

                    foreach (var station in stations)
                    {
                        // Provera validnosti stanice
                        if (IsValidStation(station))
                        {
                            Stations.Add(station);
                            if (station.IsFavorite)
                            {
                                FavoriteStations.Add(station);
                            }
                            Debug.WriteLine($"✅ Loaded: {station.Name}");
                        }
                        else
                        {
                            Debug.WriteLine($"❌ Invalid station: {station.Name}");
                        }
                    }

                    Debug.WriteLine($"🎯 Total loaded: {Stations.Count} stations");

                    // Sortiranje
                    var sortedStations = Stations.OrderBy(s => s.Name).ToList();
                    Stations.Clear();
                    foreach (var station in sortedStations)
                    {
                        Stations.Add(station);
                    }

                    // Obavezno ažuriraj UI
                    UpdateAvailableFilters();
                    ApplyFilters();
                }
                else
                {
                    Debug.WriteLine("❌ Deserialization returned null");
                    LoadDefaultStations();
                }
            }
            else
            {
                Debug.WriteLine("📁 JSON file not found, creating defaults");
                LoadDefaultStations();
                SaveStations();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"💥 Load error: {ex.Message}");
            LoadDefaultStations();
        }
    }

    private bool IsValidStation(RadioStation station)
    {
        return !string.IsNullOrWhiteSpace(station.Name) &&
               !string.IsNullOrWhiteSpace(station.StreamUrl) &&
               !string.IsNullOrWhiteSpace(station.Genre) &&
               !string.IsNullOrWhiteSpace(station.Country);
    }

    public void SaveStations()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            // Koristimo listu za serijalizaciju
            var stationsList = Stations.ToList();
            var json = JsonSerializer.Serialize(stationsList, options);
            File.WriteAllText(_dataFilePath, json);

            Debug.WriteLine($"💾 Saved {Stations.Count} stations to: {_dataFilePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Save error: {ex.Message}");
        }
    }


    private void LoadDefaultStations()
    {
        // 🇩🇪 Nemačke stanice
        Stations.Add(new RadioStation
        {
            Name = "Radio Netz - 70s",
            StreamUrl = "http://0n-70s.radionetz.de:8000/0n-70s.mp3",
            Genre = "70s Hits",
            Country = "Germany",
            Bitrate = 128
        });

        Stations.Add(new RadioStation
        {
            Name = "Radio Netz - 80s",
            StreamUrl = "http://0n-80s.radionetz.de:8000/0n-80s.mp3",
            Genre = "80s Hits",
            Country = "Germany",
            Bitrate = 128
        });

        // 🇺🇸 Američke stanice
        Stations.Add(new RadioStation
        {
            Name = "SomaFM - Groove Salad",
            StreamUrl = "http://ice1.somafm.com/groovesalad-128-mp3",
            Genre = "Ambient",
            Country = "USA",
            Bitrate = 128
        });

        Stations.Add(new RadioStation
        {
            Name = "SomaFM - Space Station",
            StreamUrl = "http://ice1.somafm.com/spacestation-128-mp3",
            Genre = "Electronic",
            Country = "USA",
            Bitrate = 128
        });

        // 🇷🇸 Srpske stanice
        Stations.Add(new RadioStation
        {
            Name = "Radio Novi Sad",
            StreamUrl = "http://shoutcast.rtv.vo.yellowcache.com:8040/;stream.mp3",
            Genre = "Various",
            Country = "Serbia",
            Bitrate = 128
        });

        Stations.Add(new RadioStation
        {
            Name = "Rock Radio",
            StreamUrl = "http://live.rockradio.rs:8000/rock.mp3",
            Genre = "Rock",
            Country = "Serbia",
            Bitrate = 128
        });

        // 🇬🇧 Britanske stanice
        Stations.Add(new RadioStation
        {
            Name = "BBC Radio 1",
            StreamUrl = "http://bbcmedia.ic.llnwd.net/stream/bbcmedia_radio1_mf_p",
            Genre = "Pop",
            Country = "UK",
            Bitrate = 128
        });

        // 🇨🇭 Švajcarske stanice
        Stations.Add(new RadioStation
        {
            Name = "Radio Swiss Jazz",
            StreamUrl = "http://stream.srg-ssr.ch/m/rsj/mp3_128",
            Genre = "Jazz",
            Country = "Switzerland",
            Bitrate = 128
        });

        // Sortiraj po imenu
        var sortedStations = Stations.OrderBy(s => s.Name).ToList();
        Stations.Clear();
        foreach (var station in sortedStations)
        {
            Stations.Add(station);
        }
    }



    public void AddStation(RadioStation station)
    {
        Stations.Add(station);
        SaveStations();
        UpdateAvailableFilters();
        ApplyFilters();
    }

    public void RemoveStation(RadioStation station)
    {
        Stations.Remove(station);
        FavoriteStations.Remove(station);
        SaveStations();
        UpdateAvailableFilters();
        ApplyFilters();
    }

    public void ToggleFavorite(RadioStation station)
    {
        station.IsFavorite = !station.IsFavorite;

        if (station.IsFavorite && !FavoriteStations.Contains(station))
        {
            FavoriteStations.Add(station);
            StationFavorited?.Invoke(this, new StationEventArgs(station));
        }
        else if (!station.IsFavorite)
        {
            FavoriteStations.Remove(station);
            StationUnfavorited?.Invoke(this, new StationEventArgs(station));
        }

        SaveStations();
    }


    // dodavanje favorita
    public event EventHandler<StationEventArgs>? StationFavorited;
    public event EventHandler<StationEventArgs>? StationUnfavorited;

    public class StationEventArgs : EventArgs
    {
        public RadioStation Station { get; }
        public StationEventArgs(RadioStation station) => Station = station;
    }




    // SEARCH/FILTER
    private void UpdateAvailableFilters()
    {
        // Popuni genre filter
        var genres = Stations.Select(s => s.Genre)
                           .Distinct()
                           .OrderBy(g => g)
                           .ToList();
        AvailableGenres.Clear();
        AvailableGenres.Add("ALL");
        foreach (var genre in genres)
        {
            AvailableGenres.Add(genre);
        }

        // Popuni country filter  
        var countries = Stations.Select(s => s.Country)
                              .Distinct()
                              .OrderBy(c => c)
                              .ToList();
        AvailableCountries.Clear();
        AvailableCountries.Add("ALL");
        foreach (var country in countries)
        {
            AvailableCountries.Add(country);
        }
    }

    public void ApplyFilters()
    {
        // Filter all stations
        var filtered = Stations.Where(station =>
            (SelectedGenre == "ALL" || station.Genre == SelectedGenre) &&
            (SelectedCountry == "ALL" || station.Country == SelectedCountry) &&
            (string.IsNullOrEmpty(SearchText) ||
             station.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
             station.Genre.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
        ).OrderBy(s => s.Name).ToList();

        FilteredStations.Clear();
        foreach (var station in filtered)
        {
            FilteredStations.Add(station);
        }

        // Filter favorites
        var filteredFavorites = FavoriteStations.Where(station =>
            (SelectedGenre == "ALL" || station.Genre == SelectedGenre) &&
            (SelectedCountry == "ALL" || station.Country == SelectedCountry) &&
            (string.IsNullOrEmpty(SearchText) ||
             station.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
        ).OrderBy(s => s.Name).ToList();

        FilteredFavorites.Clear();
        foreach (var station in filteredFavorites)
        {
            FilteredFavorites.Add(station);
        }

        Trace.WriteLine($"🔍 Filters applied: {filtered.Count}/{Stations.Count} stations");
    }




}

