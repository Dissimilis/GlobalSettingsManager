using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace GlobalSettingsManager
{
    public class SettingsManager : ISettingsManager
    {
        public static ISettingsManager DefaultManagerInstance { get; set; }

        public const string FlagsCategoryName = "Flags";

        protected const int PropertyErrorsThreshold = 12;
        protected DateTime FirstPropertyError = DateTime.UtcNow;
        protected int PropertyErrorsCount = 0;

        public static readonly object SyncRoot = new object();
        protected ListDictionary AllSettings = new ListDictionary();
        //protected List<string> SettingsNames = new List<string>(10);

        protected Dictionary<string, bool> Flags = new Dictionary<string, bool>(); 

        /// <summary>
        /// Raised on when deserializing property value fails
        /// </summary>
        public event EventHandler<UnhandledExceptionEventArgs> PropertyError;

        protected readonly ISettingsRepository Repository;

        /// <summary>
        /// Default is true; If false - raises PropertyError event instead of throwing
        /// </summary>
        public bool ThrowPropertySetException { get; set; }

        /// <summary>
        /// Default is true; Tries not to spam if many deserializing exceptions occurs in small period
        /// </summary>
        public bool ThrottlePropertyExceptions { get; set; }

        /// <summary>
        /// Default is true; Writes settings to DB if no previous setting exists
        /// </summary>
        public bool AutoPersistOnCreate { get; set; }


        /// <summary>
        /// Default is DateTime.UtcNow
        /// </summary>
        public Func<DateTime> Now { get; set; }

        /// <summary>
        /// Class responsible for converting properties to string and back
        /// </summary>
        public ValueConverter Converter { get; set; }

        static SettingsManager()
        {

        }

        public SettingsManager(ISettingsRepository repository)
        {
            Repository = repository;
            Now = () => DateTime.UtcNow;
            Converter = new ValueConverter();

            AutoPersistOnCreate = true;
            ThrottlePropertyExceptions = true;
            ThrottlePropertyExceptions = true;
        }

        /// <summary>
        /// Checks if flag is set to true
        /// </summary>
        /// <param name="flagName"></param>
        /// <returns>True if flag is set</returns>
        public bool IsFlagSet(string flagName)
        {
            if (Flags == null)
            {
                lock (SyncRoot)
                {
                    Flags = new Dictionary<string, bool>();
                    var flagsFromDb = Repository.ReadSettings(FlagsCategoryName).ToNonNullArray();
                    foreach (var f in flagsFromDb)
                    {
                        SetFlag(f.Name, f.Value);
                    }
                }
            }
            bool flag = false;
            Flags.TryGetValue(flagName, out flag);
            return flag;
        }

        public T Get<T>(bool force = false) where T : SettingsBase, new()
        {
            var loadedSettings = AllSettings[typeof(T)];
            lock (SyncRoot)
            {
                if (loadedSettings == null || force)
                {
                    var settingsInstance = new T();
                    var settingsFromDb = Repository.ReadSettings(settingsInstance.Category).ToNonNullArray();
                    if (settingsFromDb.Length == 0)
                    {
                        if (AutoPersistOnCreate)
                        {
                            var model = ToDbModel(settingsInstance);
                            Repository.WriteSettings(model); //todo: consider moving out of lock
                        }
                    }
                    SetProperties(settingsInstance, settingsFromDb);
                    if (loadedSettings == null)
                    {
                        //SettingsNames.Add(settingsInstance.Category);
                        AllSettings.Add(typeof (T), settingsInstance);
                    }
                    return settingsInstance;

                }
                return loadedSettings as T;
            }
        }
        
        public virtual bool Save<T>(Expression<Func<T>> property, SettingsBase settings)
        {
            var propertyInfo = ((MemberExpression)property.Body).Member as PropertyInfo;
            if (propertyInfo == null)
            {
                throw new ArgumentException("The lambda expression 'property' should return valid Property");
            }
            var settingsType = propertyInfo.DeclaringType;
            if (settingsType == null)
                throw new ArgumentException("Provided 'property' dont have declaring type", "property");
            if (settings.ReadOnly)
                return false;
            var model = ToDbModel(propertyInfo, settings);
            lock (SyncRoot)
            {
                return Repository.WriteSetting(model);
            }
        }
        public virtual int Save(SettingsBase settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (settings.ReadOnly)
                return 0;
            var model = ToDbModel(settings);
            lock (SyncRoot)
            {
                return Repository.WriteSettings(model);
            }
        }

        /// <summary>
        /// Allows to modify settings and save them in single transaction (under lock)
        /// </summary>
        /// <typeparam name="T">Settings type</typeparam>
        /// <param name="changeAction">Action for manipulating settings class</param>
        /// <param name="settings">Settings object to modify</param>
        /// <returns></returns>
        public virtual int ChangeAndSave<T>(Action<T> changeAction, T settings) where T : SettingsBase
        {
            if (changeAction == null)
                throw new ArgumentNullException("changeAction");

            lock (SyncRoot)
            {
                changeAction(settings);
                var model = ToDbModel(settings);
                if (settings.ReadOnly)
                    return 0;
                return Repository.WriteSettings(model);
            }
        }

        protected virtual IEnumerable<SettingsDbModel> ToDbModel(SettingsBase settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            var props = settings.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            return props.Select<PropertyInfo, SettingsDbModel>(prop => ToDbModel(prop, settings)).Where(model => model != null);
        }

        protected virtual SettingsDbModel ToDbModel(PropertyInfo property, SettingsBase settings)
        {
            if (!property.CanRead || !property.CanWrite)
                return null;
            if (property.Name == "ReadOnly") //system property
                return null;
            var value = property.GetValue(settings, null);
            var model = new SettingsDbModel()
            {
                Category = settings.Category,
                UpdatedAt = DateTime.UtcNow,
                Name = property.Name
            };
            model.Value = Converter.ConvertToString(value);
            return model;
        }

        /// <summary>
        /// Updates flag with provided value
        /// </summary>
        /// <returns>True if flag was changed</returns>
        protected bool SetFlag(string name, string value)
        {
            var boolValue = IsTrueString(value);
            bool flag;
            if (!Flags.TryGetValue(name, out flag) || flag != boolValue)
            {
                Flags[name] = boolValue;
                return true;
            }
            return false;
        }

        protected virtual void SetProperties(SettingsBase settings, IEnumerable<SettingsDbModel> dbSettings, bool? throwSetException = null)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (dbSettings == null)
                throw new ArgumentNullException("dbSettings");
            throwSetException = throwSetException ?? ThrowPropertySetException;
            foreach (var property in dbSettings)
            {
                try
                {
                    var prop = settings.GetType().GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null || !prop.CanWrite)
                        continue;

                    var existing = prop.GetValue(settings, null);

                    var converted = Converter.ConvertFromString(property.Value, prop.PropertyType);
                    if (!Equals(existing, converted))//custom objects must implement Equals() for this to work
                        settings.OnPropertyChanged(property.Name);
                    prop.SetValue(settings, converted,  null);

                }
                catch (Exception ex)
                {
                    if (!throwSetException.Value)
                    {
                        if (PropertyErrorsCount > PropertyErrorsThreshold && DateTime.UtcNow - FirstPropertyError > TimeSpan.FromMinutes(5)) //reset error counter after 5minutes
                        {
                            PropertyErrorsCount = 0;
                            FirstPropertyError = DateTime.UtcNow;
                        }
                        if (PropertyError != null && (PropertyErrorsCount < PropertyErrorsThreshold || !ThrottlePropertyExceptions)) //only raise event when not throttling
                        {
                            PropertyErrorsCount++;
                            var propertyExeption = new SettingsPropertyException(String.Format("Error setting property {0}.{1}", settings.Category, property.Name), property.Name,settings.Category, ex);
                            PropertyError.Invoke(settings, new UnhandledExceptionEventArgs(propertyExeption, false));
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        private bool IsTrueString(string value)
        {
            bool boolValue;
            if (bool.TryParse(value, out boolValue))
            {
                return boolValue;
            }
            if (value == "1")
                return true;
            return false;

        }
    }
}