using System;
using System.Collections.Generic;

namespace GlobalSettingsManager
{
    public interface ISettingsRepository
    {
        bool WriteSetting(SettingsStorageModel setting);
        int WriteSettings(IEnumerable<SettingsStorageModel> settings);
        IEnumerable<SettingsStorageModel> ReadSettings(string category);
        IEnumerable<SettingsStorageModel> ReadSettings(IList<string> categories, DateTime? lastChangedMin = null);
    }
}