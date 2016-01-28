using System;
using System.Linq.Expressions;
using System.Reflection;

namespace GlobalSettingsManager
{
    public class PropertyChangeEventArgs : EventArgs
    {
        public string PropertyName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
        public SettingsBase Settings { get; set; }

        /// <summary>
        /// Checks if event args is of provided property
        /// </summary>
        /// <typeparam name="T">Settings</typeparam>
        /// <typeparam name="TP">Property type</typeparam>
        /// <param name="property">Property to check</param>
        /// <returns>True if this args object is for provided property</returns>
        public bool Is<T,TP>(Expression<Func<T, TP>> property) where T : SettingsBase
        {
            var propertyInfo = ((MemberExpression)property.Body).Member as PropertyInfo;
            if (propertyInfo == null)
            {
                throw new ArgumentException("Expression 'property' should define valid Property");
            }
            var settingsType = propertyInfo.DeclaringType;
            if (settingsType != typeof(T))
                throw new ArgumentException("Property not from generic T", "property");
            return propertyInfo.Name == PropertyName;
        }

        /// <summary>
        /// Invokes action if event args maches provided property
        /// </summary>
        /// <typeparam name="T">Settings</typeparam>
        /// <typeparam name="TP">Property type</typeparam>
        /// <param name="property">Property to check</param>
        /// <param name="action">Action where first param is old value and second param is new valu</param>
        public void When<T,TP>(Expression<Func<T, TP>> property, Action<TP, TP> action) where T : SettingsBase
        {
            if (Is(property))
            {
                if (action != null)
                {
                    action.Invoke((TP)OldValue, (TP)NewValue);
                }
            }
        }
        /// <summary>
        /// Invokes action if event args maches provided property
        /// </summary>
        /// <typeparam name="T">Settings</typeparam>
        /// <typeparam name="TP">Property type</typeparam>
        /// <param name="property">Property to check</param>
        /// <param name="action">Action where param is new (changed) valu</param>
        public void When<T, TP>(Expression<Func<T, TP>> property, Action<TP> action) where T : SettingsBase
        {
            if (Is(property))
            {
                if (action != null)
                {
                    action.Invoke((TP)NewValue);
                }
            }
        }

    }
}