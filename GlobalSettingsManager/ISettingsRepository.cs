using System;
using System.Collections.Generic;

namespace GlobalSettingsManager
{
    public interface ISettingsRepository
    {
        bool WriteSetting(SettingsDbModel setting);
        int WriteSettings(IEnumerable<SettingsDbModel> settings);
        IEnumerable<SettingsDbModel> ReadSettings(string category);
        IEnumerable<SettingsDbModel> ReadSettings(IList<string> categories, DateTime? lastChangedMin = null);
    }
}