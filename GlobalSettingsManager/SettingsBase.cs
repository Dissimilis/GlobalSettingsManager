using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace GlobalSettingsManager
{
    /// <summary>
    /// Settings base class
    /// </summary>
    public abstract class SettingsBase : INotifyPropertyChanged
    {

        /// <summary>
        /// Category which will be looked for in repository
        /// </summary>
        public abstract string Category { get; }

        /// <summary>
        /// SettingsManager associated with this settings instance
        /// </summary>
        //protected SettingsManager SettingsManager { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Called when property value changes
        /// </summary>
        /// <param name="propertyName"></param>
        public virtual void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Returns object property by string name using reflection
        /// </summary>
        /// <param name="propertyName">Property name to look for</param>
        /// <returns>Property value</returns>
        public object GetPropertyByName(string propertyName)
        {
            var prop = this.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return prop.GetValue(this, null);
        }

    }

    /// <summary>
    /// Settings base class with Save/Get methods
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class SelfManagedSettings<T> : SettingsBase where T : SelfManagedSettings<T>, new()
    {

        /// <summary>
        /// Manager which was used to load this object
        /// </summary>
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

        /// <summary>
        /// Saves all properties to repository
        /// </summary>
        /// <returns>How many properties was saved</returns>
        public virtual int Save()
        {
            return GetManager().Save(this);
        }

        /// <summary>
        /// Saves single property to repository
        /// </summary>
        /// <typeparam name="T2">Property type</typeparam>
        /// <param name="property">Property to save</param>
        /// <returns></returns>
        public virtual bool Save<T2>(Expression<Func<T2>> property)
        {
            return GetManager().Save(property, this as T);
        }

        /// <summary>
        /// Changes and saves property to repository in single transaction (under lock)
        /// </summary>
        /// <param name="changeAction">Change function</param>
        /// <returns>How many properties was saved</returns>
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