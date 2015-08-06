using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace GlobalSettingsManager
{
    public abstract class SettingsBase : INotifyPropertyChanged
    {

        public abstract string Category { get; }

        /// <summary>
        /// SettingsManager associated with this settings instance
        /// </summary>
        //protected SettingsManager SettingsManager { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public object GetPropertyByName(string propertyName)
        {
            var prop = this.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return prop.GetValue(this, null);
        }


    }

    public abstract class SelfManagedSettings<T> : SettingsBase where T : SelfManagedSettings<T>, new()
    {
        protected ISettingsManager Manager { get; set; }

        /// <summary>
        /// Gets cached settings or creates new instance
        /// </summary>
        /// <param name="force">Forces to create new instance (read from repository)</param>
        /// <param name="customSettingsManager">Custom manager</param>
        /// <returns></returns>
        public static T Get(bool force = false, ISettingsManager customSettingsManager = null)
        {
            var manager = customSettingsManager ?? SimpleSettingsManager.DefaultSettingsManager;
            var settings = manager.Get<T>(force);
            if (settings == null)
                throw new TypeLoadException("Settings is of wrong type");
            if (customSettingsManager != null)
                settings.Manager = customSettingsManager;
            return settings;
        }

        public virtual int Save()
        {
            return GetManager().Save(this);
        }

        public virtual bool Save<T2>(Expression<Func<T2>> property)
        {
            return GetManager().Save(property, this as T);
        }

        public virtual int ChangeAndSave(Action<T> changeAction)
        {
            return GetManager().ChangeAndSave<T>(changeAction, this as T);
        }

        private ISettingsManager GetManager()
        {
            return (Manager ?? SimpleSettingsManager.DefaultSettingsManager);
        }


    }
}