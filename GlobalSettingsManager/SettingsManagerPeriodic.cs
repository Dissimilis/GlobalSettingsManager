using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GlobalSettingsManager
{
    public class SettingsManagerPeriodic : SimpleSettingsManager
    {

        /// <summary>
        /// Raised when exception occurs in periodic reader
        /// </summary>
        public event EventHandler<UnhandledExceptionEventArgs> PeriodicReaderError;

        public event EventHandler PeriodicReaderCanceled;

        public event EventHandler PeriodicReaderExecuting;

        /// <summary>
        /// Initializes settings reader and starts periodic reading task
        /// </summary>
        /// <param name="repository"></param>
        public SettingsManagerPeriodic(ISettingsRepository repository) : base(repository)
        {

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
                            var settingsFromDb = Repository.ReadSettings(SettingsNames, lastRead).ToArray();
                            if (settingsFromDb.Length == 0)
                                continue;
                            foreach (SettingsBase settings in AllSettings.Values)
                            {
                                var category = settings.Category;
                                var matchingSettings = settingsFromDb.Where(s => s.Category == category);
                                SetProperties(settings, matchingSettings);
                            }
                            lastRead = Now();
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