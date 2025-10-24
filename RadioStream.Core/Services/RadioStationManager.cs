using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using RadioStream.Core.Models;

namespace RadioStream.Core.Services;

public class RadioStationManager
{
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
        LoadDefaultStations();

        UpdateAvailableFilters();
        ApplyFilters();
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
    }

    public void RemoveStation(RadioStation station)
    {
        Stations.Remove(station);
        FavoriteStations.Remove(station);
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

