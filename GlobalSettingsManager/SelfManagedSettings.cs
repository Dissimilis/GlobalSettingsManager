using System;
using System.Linq.Expressions;

namespace GlobalSettingsManager
{
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
            var manager = customSettingsManager ?? SettingsManager.DefaultManagerInstance;
            if (manager == null)
                throw new ArgumentNullException("customSettingsManager", "Settings manager not provided and default settings manager is not set");
            var settings = manager.Get<T>(force);
            if (settings == null)
                throw new SettingsGetException("Settings is of wrong type");
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
            return (Manager ?? SettingsManager.DefaultManagerInstance);
        }


    }

    public class SettingsGetException : Exception
    {
        public SettingsGetException(string messgae) : base(messgae)
        {

        }
    }
}