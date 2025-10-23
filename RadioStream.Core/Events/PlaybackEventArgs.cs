using System;

namespace RadioStream.Core.Events;

public class PlaybackEventArgs : EventArgs
{
    public string Message { get; }
    public PlaybackEventArgs(string message) => Message = message;
}

public class BufferEventArgs : EventArgs
{
    public double BufferLevel { get; }
    public BufferEventArgs(double bufferLevel) => BufferLevel = bufferLevel;
}

public class ErrorEventArgs : EventArgs
{
    public Exception Exception { get; }
    public string Context { get; }

    public ErrorEventArgs(Exception exception, string context = "")
    {
        Exception = exception;
        Context = context;
    }
}