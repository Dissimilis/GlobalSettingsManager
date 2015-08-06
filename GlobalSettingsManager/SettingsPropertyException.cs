using System;

namespace GlobalSettingsManager
{
    public class SettingsPropertyException : Exception
    {
        public string PropertyName { get; private set; }
        public string SettingsName { get; private set; }

        public SettingsPropertyException(string message, string propertyName, string settingsName, Exception innerException)
            : base(message, innerException)
        {
            PropertyName = propertyName;
            SettingsName = settingsName;
        }
    }
}