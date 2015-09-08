﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlobalSettingsManager
{
    internal class PeriodicErrorEventManager
    {
        /// <summary>
        /// Indicates when CapturedExceptionTypes was last flushed
        /// </summary>
        private DateTime _lastFlushTime;

        public TimeSpan RepeatingErrorInterval { get; set; }

        /// <summary>
        /// A set of caught exception type names during defined period of time
        /// </summary>
        private HashSet<Type> _capturedExceptionTypes;

        public void Add(Exception ex)
        {
            _capturedExceptionTypes.Add(ex.GetType());
        }

        public bool FlushOld(DateTime now)
        {
            if (_lastFlushTime < now - RepeatingErrorInterval)
            {
                _capturedExceptionTypes.Clear();
                _lastFlushTime = now;
                return true;
            }
            return false;
        }

        public bool Contains(Exception ex)
        {
            return _capturedExceptionTypes.Contains(ex.GetType());
        }


        public PeriodicErrorEventManager()
        {
            RepeatingErrorInterval = TimeSpan.FromSeconds(90);
            _capturedExceptionTypes = new HashSet<Type>();
        }


    }
}
