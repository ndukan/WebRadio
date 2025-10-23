using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadioStream.Core.Models
{
    public enum PlaybackState
    {
        Stopped,
        Playing,
        Paused,
        Buffering,
        Error
    }
}
