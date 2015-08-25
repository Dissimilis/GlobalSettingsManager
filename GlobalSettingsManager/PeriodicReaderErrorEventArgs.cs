using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlobalSettingsManager
{
    public class PeriodicErrorEventManager
    {
        private static PeriodicErrorEventManager _instance;
        private static readonly object _padlock = new object();

        /// <summary>
        /// Indicates already caught exception lifespan
        /// </summary>
        public TimeSpan ExceptionStorageInterval { get; set; }

        /// <summary>
        /// A set of caught exception type names during defined period of time
        /// </summary>
        public HashSet<string> CapturedExceptionTypes { get; set; }

        private PeriodicErrorEventManager()
        {
            CapturedExceptionTypes = new HashSet<string>();
            ExceptionStorageInterval = TimeSpan.FromSeconds(90);
        }

        public static PeriodicErrorEventManager Instance
        {
            get
            {
                lock (_padlock)
                {
                    if (_instance == null)
                    {
                        _instance = new PeriodicErrorEventManager();
                    }
                    return _instance;
                }
            }
        }

        public void FlushExceptionTypesStorage()
        {
            CapturedExceptionTypes.Clear();
        }
    }
}
