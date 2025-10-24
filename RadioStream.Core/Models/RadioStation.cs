using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RadioStream.Core.Models;

public class RadioStation : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _streamUrl = string.Empty;
    private string _genre = "Various";
    private string _country = "International";
    private string _logoUrl = string.Empty;
    private bool _isFavorite;
    private int _bitrate = 128;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string StreamUrl
    {
        get => _streamUrl;
        set { _streamUrl = value; OnPropertyChanged(); }
    }

    public string Genre
    {
        get => _genre;
        set { _genre = value; OnPropertyChanged(); }
    }

    public string Country
    {
        get => _country;
        set { _country = value; OnPropertyChanged(); }
    }

    public string LogoUrl
    {
        get => _logoUrl;
        set { _logoUrl = value; OnPropertyChanged(); }
    }


    public int Bitrate
    {
        get => _bitrate;
        set { _bitrate = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => Name;


    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                OnPropertyChanged();

                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string DisplayName => $"{(IsFavorite ? "⭐ " : "")}{Name}";


}