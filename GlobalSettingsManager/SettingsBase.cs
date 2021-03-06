﻿using System.ComponentModel;
using System.Reflection;
using System.Security.Policy;

namespace GlobalSettingsManager
{
    /// <summary>
    /// Settings base class
    /// </summary>
    public abstract class SettingsBase : INotifyPropertyChanged
    {

        /// <summary>
        /// Indicates if this settings class should not be changed in repository
        /// </summary>
        public virtual bool ReadOnly { get; set; }


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
}