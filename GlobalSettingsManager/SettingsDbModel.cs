using System;

namespace GlobalSettingsManager
{
    public class SettingsStorageModel
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public DateTime UpdatedAt { get; set; }

        public override string ToString()
        {
            return String.Format("{0}.{1}: {2}", Category, Name, Value);
        }
    }
}