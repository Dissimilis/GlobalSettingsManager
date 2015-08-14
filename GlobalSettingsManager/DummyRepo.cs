using System;
using System.Collections.Generic;
using System.Linq;

namespace GlobalSettingsManager
{
    public class InMemoryRepository : ISettingsRepository
    {

        public List<SettingsStorageModel> Content = new List<SettingsStorageModel>(); 

        public bool WriteSetting(SettingsStorageModel s)
        {
            var existing = Content.Single(c => c.Category == s.Category && c.Name == s.Name);
            if (existing == null)
            {
                s.UpdatedAt = DateTime.UtcNow;
                Content.Add(s);
                return true;
            }
            else
            {
                existing.Value = s.Value;
                existing.UpdatedAt = DateTime.UtcNow;
                return false;
            }
        }

        public int WriteSettings(IEnumerable<SettingsStorageModel> settings)
        {
            int cnt = 0;
            foreach (var s in settings)
            {
                var existing = Content.SingleOrDefault(c => c.Category == s.Category && c.Name == s.Name);
                if (existing == null)
                {
                    cnt++;
                    s.UpdatedAt = DateTime.UtcNow;
                    Content.Add(s);
                }
                else
                {
                    existing.Value = s.Value;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }
            return cnt;
        }

        public IEnumerable<SettingsStorageModel> ReadSettings(string category)
        {
            return Content.Where(c => c.Category == category);
        }

        public IEnumerable<SettingsStorageModel> ReadSettings(IList<string> categories, DateTime? lastChangedMin = null)
        {
            return Content.Where(c => categories.Contains(c.Category) && (c.UpdatedAt <= lastChangedMin || !lastChangedMin.HasValue));
        }
    }
}