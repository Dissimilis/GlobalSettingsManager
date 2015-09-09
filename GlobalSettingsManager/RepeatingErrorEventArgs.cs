using System;

namespace GlobalSettingsManager
{
    public class RepeatingErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
        public bool IsRepeating { get; set; }
    }
}
