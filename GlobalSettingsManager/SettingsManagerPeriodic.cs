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
        public event EventHandler<RepeatingErrorEventArgs> PeriodicReaderError;

        /// <summary>
        /// Raised when periodic reading task receives cancelation request
        /// </summary>
        public event EventHandler PeriodicReaderCanceled;

        /// <summary>
        /// Raised when reading starts executing (by UpdateChangedSettings or periodic task)
        /// </summary>
        public event EventHandler ReaderExecuting;

        /// <summary>
        /// Gets or sets interval at which reading errors are treated as repeating
        /// </summary>
        public TimeSpan RepeatingErrorInterval
        {
            get { return _periodicReaderErrors.RepeatingErrorInterval; }
            set { _periodicReaderErrors.RepeatingErrorInterval = value; }
        }

        /// <summary>
        /// Default is false
        /// </summary>
        public bool ThrowPropertySetExceptionsOnPeriodicRead { get; set; }

        /// <summary>
        /// Raised when flag changes in underlying repository
        /// </summary>
        public event PropertyChangedEventHandler FlagChanged;

        private DateTime? _lastRead;

        /// <summary>
        /// Initializes settings reader and starts periodic reading task
        /// </summary>
        /// <param name="repository"></param>
        public SettingsManagerPeriodic(ISettingsRepository repository)
            : base(repository)
        {
            ThrowPropertySetExceptionsOnPeriodicRead = false;
        }

        /// <summary>
        /// Looks for changes in underlying repository and updates all SettingsBase (registered with this SettingsManager) objects with changed values
        /// Respects all error handling settings; 
        /// </summary>
        public void UpdateChangedSettings()
        {
            try
            {
                lock (SyncRoot)
                {

                    if (ReaderExecuting != null)
                        ReaderExecuting.Invoke(this, new EventArgs());
                    var now = Now();
                    var settingsFromRepo = Repository.ReadSettings(GetCategoriesToRead(), _lastRead).ToNonNullArray();
                    if (settingsFromRepo.Length == 0)
                        return;
                    foreach (SettingsBase settings in AllSettings.Values)
                    {
                        var category = settings.Category;
                        var matchingSettings = settingsFromRepo.Where(s => s.Category == category);
                        SetProperties(settings, matchingSettings, ThrowPropertySetExceptionsOnPeriodicRead);
                    }
                    foreach (var f in settingsFromRepo.Where(s => s.Category == FlagsCategoryName))
                    {
                        if (SetFlag(f.Name, f.Value))
                        {
                            if (FlagChanged != null)
                                FlagChanged.Invoke(this, new PropertyChangedEventArgs(f.Name));
                        }
                    }

                    _lastRead = settingsFromRepo.Max(s => s.UpdatedAt);
                    //sets last read time to newest found setting
                    if (_lastRead > now) //last read time must not be greated than current time
                        _lastRead = now;
                }
            }
            catch (Exception ex)
            {
                _periodicReaderErrors.FlushOld(Now());
                if (PeriodicReaderError != null)
                {
                    var args = new RepeatingErrorEventArgs()
                    {
                        Exception = ex,
                        IsRepeating = _periodicReaderErrors.Contains(ex.GetType().Name)
                    };
                    PeriodicReaderError.Invoke(this, args);
                }
                _periodicReaderErrors.Add(ex.GetType().Name);
            }
        }

        /// <summary>
        /// Starts periodic reading task which calls <see cref="UpdateChangedSettings"/> at provided interval)
        /// </summary>
        /// <param name="interval">Interval for calling underlying repository</param>
        /// <param name="token"></param>
        /// <returns>Neverending task</returns>
        public Task StartReadingTask(TimeSpan interval, CancellationToken token)
        {
            var task = Task.Factory.StartNew(() => //must be async in .net 4.5
            {
                while (true)
                {
                    token.WaitHandle.WaitOne(interval); //must be 'await Task.Delay()' in .NET 4.5
                    token.ThrowIfCancellationRequested();
                    UpdateChangedSettings();

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



}