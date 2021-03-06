﻿using System;
using System.ComponentModel;
using System.Linq.Expressions;

namespace GlobalSettingsManager
{
    public interface ISettingsManager
    {
        /// <summary>
        /// Gets cached settings or creates new instance       
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="force">Forces to create new instance (read from repository)</param>
        /// <returns></returns>
        T Get<T>(bool force = false) where T : SettingsBase, new();

        /// <summary>
        /// Checks if flag is set to true
        /// </summary>
        /// <param name="flagName"></param>
        /// <returns>True if flag is set</returns>
        bool IsFlagSet(string flagName);

        bool Save<T>(Expression<Func<T>> property, SettingsBase settings);
        int Save(SettingsBase settings);
        
        /// <summary>
        /// Allows to modify settings and save them in single transaction (under lock)
        /// </summary>
        /// <typeparam name="T">Settings type</typeparam>
        /// <param name="changeAction">Action for manipulating settings class</param>
        /// <param name="settings"></param>
        /// <returns></returns>
        int ChangeAndSave<T>(Action<T> changeAction, T settings) where T : SettingsBase;

    }
}