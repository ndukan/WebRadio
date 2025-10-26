using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadioStream.Core.Services
{
    // Volume Level Event Args
    public class VolumeLevelEventArgs : EventArgs
    {
        public double LeftChannelLevel { get; }
        public double RightChannelLevel { get; }

        public VolumeLevelEventArgs(double leftLevel, double rightLevel)
        {
            LeftChannelLevel = leftLevel;
            RightChannelLevel = rightLevel;
        }

        public double AverageLevel => (LeftChannelLevel + RightChannelLevel) / 2.0;
    }
}
