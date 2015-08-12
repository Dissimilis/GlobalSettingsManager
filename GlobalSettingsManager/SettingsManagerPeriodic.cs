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

        /// <summary>
        /// Raised when exception occurs in periodic reader
        /// </summary>
        public event EventHandler<UnhandledExceptionEventArgs> PeriodicReaderError;

        public event EventHandler PeriodicReaderCanceled;

        public event EventHandler PeriodicReaderExecuting;

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
        public SettingsManagerPeriodic(ISettingsRepository repository) : base(repository)
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
                        token.ThrowIfCancellationRequested();
                        Delay(interval, token).Wait(token); //must be await Task.Delay() in .NET 4.5
                        lock (SyncRoot)
                        {
                            if (PeriodicReaderExecuting != null)
                                PeriodicReaderExecuting.Invoke(this, new EventArgs());

                            var settingsFromRepo = Repository.ReadSettings(GetCategoriesToRead(), lastRead).ToNonNullArray();
                            if (settingsFromRepo.Length == 0)
                                continue;
                            foreach (SettingsBase settings in AllSettings.Values)
                            {
                                var category = settings.Category;
                                var matchingSettings = settingsFromRepo.Where(s => s.Category == category);
                                SetProperties(settings, matchingSettings, ThrowPropertySetExceptionsOnPeriocRead);
                            }
                            foreach (var f in settingsFromRepo.Where(s=>s.Category == FlagsCategoryName))
                            {
                                if (SetFlag(f.Name, f.Value))
                                {
                                    if (FlagChanged != null)
                                        FlagChanged.Invoke(this, new PropertyChangedEventArgs(f.Name));
                                }
                            }
                            var now = Now();
                            lastRead = settingsFromRepo.Max(s=>s.UpdatedAt); //sets last read time to newest found setting
                            if (lastRead > now) //last read time must not be greated than current time
                                lastRead = now;
                        }
                    }
                    catch(OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        if (PeriodicReaderError != null)
                            PeriodicReaderError.Invoke(this, new UnhandledExceptionEventArgs(ex, false));
                    }
                }
            }, token);
            task.ContinueWith(t => 
            {
                if (PeriodicReaderCanceled != null)
                    PeriodicReaderCanceled.Invoke(this,new EventArgs());
            }, TaskContinuationOptions.OnlyOnCanceled);
            task.ContinueWith(t => { }, TaskContinuationOptions.NotOnRanToCompletion);
            return task;
        }


        private List<string> GetCategoriesToRead()
        {
            var result = new List<string>(AllSettings.Count+1);
            result.AddRange(from object setting in AllSettings select ((SettingsBase) setting).Category);
            result.Add(FlagsCategoryName);
            return result;
        }

        private Task Delay(TimeSpan interval, CancellationToken token)
        {
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            System.Threading.Timer timer = null;
            timer = new System.Threading.Timer(p =>
            {
                timer.Dispose(); //stop the timer
                tcs.TrySetResult(null); //timer expired, attempt to move task to the completed state.
            }, null, (int)interval.TotalMilliseconds, System.Threading.Timeout.Infinite);

            token.Register(() =>
            {
                timer.Dispose(); //stop the timer
                tcs.TrySetCanceled(); //attempt to mode task to canceled state
            });

            return tcs.Task;
        }

    }


}