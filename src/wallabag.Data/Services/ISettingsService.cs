﻿using System.Collections.Generic;

namespace wallabag.Data.Services
{
    /// <summary>
    /// Main interface for settings
    /// Based on James Montemagno's version: https://github.com/jamesmontemagno/SettingsPlugin/blob/master/src/Plugin.Settings.Abstractions/ISettings.cs (MIT license)
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets the current value or the default that you specify.
        /// </summary>
        /// <typeparam name="T">Value of t (bool, int, float, long, string)</typeparam>
        /// <param name="key">Key for settings</param>
        /// <param name="defaultValue">default value if not set</param>
        /// <returns>Value or default</returns>
        T GetValueOrDefault<T>(string key, T defaultValue = default(T), SettingStrategy strategy = SettingStrategy.Local, string containerName = "");

        /// <summary>
        /// Adds or updates the value 
        /// </summary>
        /// <param name="key">Key for settting</param>
        /// <param name="value">Value to set</param>
        void AddOrUpdateValue<T>(string key, T value, SettingStrategy strategy = SettingStrategy.Local, string containerName = "");

        /// <summary>
        /// Removes a desired key from the settings
        /// </summary>
        /// <param name="key">Key for setting</param>
        void Remove(string key, SettingStrategy strategy = SettingStrategy.Local, string containerName = "");

        /// <summary>
        /// Clear all keys from settings
        /// </summary>
        void Clear(SettingStrategy strategy = SettingStrategy.Local, string containerName = "");

        /// <summary>
        /// Checks to see if the key has been added.
        /// </summary>
        /// <param name="key">Key to check</param>
        /// <returns>True if contains key, else false</returns>
        bool Contains(string key, SettingStrategy strategy = SettingStrategy.Local, string containerName = "");

        /// <summary>
        /// Removes all settings and all containers from the settings.
        /// </summary>
        void ClearAll();

        /// <summary>
        /// Returns a whole container dictionary.
        /// </summary>
        /// <param name="containerName">The name of the container.</param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}"/> containing all the values.</returns>
        IDictionary<string, object> GetContainer(string containerName, SettingStrategy strategy = SettingStrategy.Local);
    }

    public enum SettingStrategy
    {
        Local,
        Roaming
    }
}
