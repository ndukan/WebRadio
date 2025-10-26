using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RadioPlayer.WebRadio
{
    public class AppSettings
    {
        public double WindowTop { get; set; }
        public double WindowLeft { get; set; }
        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public WindowState WindowState { get; set; }


        public double TraceWindowTop { get; set; }
        public double TraceWindowLeft { get; set; }
        public double TraceWindowWidth { get; set; }
        public double TraceWindowHeight { get; set; }
        public WindowState TraceWindowState { get; set; }


        public double Volume { get; set; }
        public string? LastStation { get; set; }


        public List<string> FavoriteStations { get; set; } = new List<string>();

    }


    public class FavoriteStation
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public DateTime AddedDate { get; set; } = DateTime.Now;
    }


}
