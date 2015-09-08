using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GlobalSettingsManager
{
    public class SettingsManagerPeriodic : SettingsManager
    {

        PeriodicErrorEventManager _periodicReaderErrors = new PeriodicErrorEventManager();
        /// <summary>
        /// Raised when exception occurs in periodic reader
        /// </summary>
        public event EventHandler<PeriodicReaderErrorEventArgs> PeriodicReaderError;

        public event EventHandler PeriodicReaderCanceled;

        public event EventHandler PeriodicReaderExecuting;

        public TimeSpan RepeatingErrorInterval
        {
            get { return _periodicReaderErrors.RepeatingErrorInterval; }
            set { _periodicReaderErrors.RepeatingErrorInterval = value; }
        }

        /// <summary>
        /// Default is false
        /// </summary>
        public bool ThrowPropertySetExceptionsOnPeriocRead { get; set; }

        /// <summary>
        /// This event is raised when flag changes in underlying repository
        /// </summary>
        public event PropertyChangedEventHandler FlagChanged;

        /// <summary>
        /// Initializes settings reader and starts periodic reading task
        /// </summary>
        /// <param name="repository"></param>
        public SettingsManagerPeriodic(ISettingsRepository repository)
            : base(repository)
        {
            ThrowPropertySetExceptionsOnPeriocRead = false;
        }

        public Task StartReadingTask(TimeSpan interval, CancellationToken token)
        {
            var task = Task.Factory.StartNew(() => //must be async in .net 4.5
            {
                DateTime lastRead = Now();

                while (true)
                {
                    try
                    {
                        token.WaitHandle.WaitOne(interval); //must be 'await Task.Delay()' in .NET 4.5
                        token.ThrowIfCancellationRequested();
                        lock (SyncRoot)
                        {
                            var now = Now();
                            if (PeriodicReaderExecuting != null)
                                PeriodicReaderExecuting.Invoke(this, new EventArgs());

                            var settingsFromRepo =
                                Repository.ReadSettings(GetCategoriesToRead(), lastRead).ToNonNullArray();
                            if (settingsFromRepo.Length == 0)
                                continue;
                            foreach (SettingsBase settings in AllSettings.Values)
                            {
                                var category = settings.Category;
                                var matchingSettings = settingsFromRepo.Where(s => s.Category == category);
                                SetProperties(settings, matchingSettings, ThrowPropertySetExceptionsOnPeriocRead);
                            }
                            foreach (var f in settingsFromRepo.Where(s => s.Category == FlagsCategoryName))
                            {
                                if (SetFlag(f.Name, f.Value))
                                {
                                    if (FlagChanged != null)
                                        FlagChanged.Invoke(this, new PropertyChangedEventArgs(f.Name));
                                }
                            }

                            lastRead = settingsFromRepo.Max(s => s.UpdatedAt);
                            //sets last read time to newest found setting
                            if (lastRead > now) //last read time must not be greated than current time
                                lastRead = now;
                        }
                    }
                    catch (Exception ex)
                    {
                        _periodicReaderErrors.FlushOld(Now());
                        if (PeriodicReaderError != null)
                        {
                            var args = new PeriodicReaderErrorEventArgs()
                            {
                                Exception = ex,
                                IsRepeating = _periodicReaderErrors.Contains(ex)
                            };
                            PeriodicReaderError.Invoke(this, args);
                        }
                        _periodicReaderErrors.Add(ex);
                    }
                }
            }, token);
            task.ContinueWith(t =>
            {
                if (PeriodicReaderCanceled != null)
                    PeriodicReaderCanceled.Invoke(this, new EventArgs());
            }, TaskContinuationOptions.OnlyOnCanceled);
            task.ContinueWith(t => { }, TaskContinuationOptions.NotOnRanToCompletion);
            return task;
        }

        private List<string> GetCategoriesToRead()
        {
            var result = new List<string>(AllSettings.Count + 1);
            result.AddRange(from object setting in AllSettings.Values select ((SettingsBase)setting).Category);
            result.Add(FlagsCategoryName);
            return result;
        }
    }


    public class PeriodicReaderErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
        public bool IsRepeating { get; set; }
    }
}