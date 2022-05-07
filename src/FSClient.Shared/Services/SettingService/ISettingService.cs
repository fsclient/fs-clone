namespace FSClient.Shared.Services
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Settigns provider service.
    /// </summary>
    public interface ISettingService
    {
        /// <summary>
        /// Root container name.
        /// </summary>
        string RootContainer { get; }

        /// <summary>
        /// Gets all containers saved in settings including <see cref="RootContainer"/>.
        /// <see cref="SettingStrategy"/> is not available, if resulted with empty list.
        /// </summary>
        /// <param name="location"><see cref="SettingStrategy"/> where settings are stored.</param>
        /// <returns>List of settings keys.</returns>
        IReadOnlyList<string> GetAllContainers(
            SettingStrategy location = SettingStrategy.Local);

        /// <summary>
        /// Clears container.
        /// </summary>
        /// <param name="container">Container to clear from.</param>
        /// <param name="location"><see cref="SettingStrategy"/> where settings are stored.</param>
        void Clear(
            string container,
            SettingStrategy location = SettingStrategy.Local);

        /// <summary>
        /// Gets all settings from container without convertion.
        /// </summary>
        /// <param name="container">Container to read from.</param>
        /// <param name="location"><see cref="SettingStrategy"/> where settings are stored.</param>
        /// <returns>Key-value dictionary of settings.</returns>
        IReadOnlyDictionary<string, object> GetAllRawSettings(
            string container,
            SettingStrategy location = SettingStrategy.Local);

        /// <summary>
        /// Sets all settings to container.
        /// </summary>
        /// <param name="container">Container to read from.</param>
        /// <param name="settings">Settings to set.</param>
        /// <param name="location"><see cref="SettingStrategy"/> where settings are stored.</param>
        /// <returns>Count of successed operations.</returns>
        int SetRawSettings(
            string container,
            IReadOnlyDictionary<string, object> settings,
            SettingStrategy location = SettingStrategy.Local);

        /// <summary>
        /// Checks if setting exists in container.
        /// </summary>
        /// <param name="container">Container to read from.</param>
        /// <param name="key">Setting key.</param>
        /// <param name="location"><see cref="SettingStrategy"/> where settings are stored.</param>
        /// <returns>True, if setting exists.</returns>
        bool SettingExists(
            string container,
            string key,
            SettingStrategy location = SettingStrategy.Local);

        /// <summary>
        /// Gets setting from container. If setting is not set or value is null, returns otherwise value.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="container">Container to read from.</param>
        /// <param name="key">Setting key.</param>
        /// <param name="otherwise">Value to return, if setting is not setted or value is null.</param>
        /// <param name="location"><see cref="SettingStrategy"/> where settings are stored.</param>
        /// <returns>Readed value. If setting is not set or value is null, returns otherwise value.</returns>
        [return: NotNullIfNotNull("otherwise")]
        T? GetSetting<T>(
            string container,
            string key,
            T? otherwise = null,
            SettingStrategy location = SettingStrategy.Local)
            where T : unmanaged;

        /// <summary>
        /// Gets setting from container. If setting is not set or value is null, returns otherwise value.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="container">Container to read from.</param>
        /// <param name="key">Setting key.</param>
        /// <param name="otherwise">Value to return, if setting is not setted or value is null.</param>
        /// <param name="location"><see cref="SettingStrategy"/> where settings are stored.</param>
        /// <returns>Readed value. If setting is not set or value is null, returns otherwise value.</returns>
        T GetSetting<T>(
            string container,
            string key,
            T otherwise = default,
            SettingStrategy location = SettingStrategy.Local)
            where T : unmanaged;

        /// <summary>
        /// Gets setting from container. If setting is not set or value is null, returns otherwise value.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="container">Container to read from.</param>
        /// <param name="key">Setting key.</param>
        /// <param name="otherwise">Value to return, if setting is not setted or value is null.</param>
        /// <param name="location"><see cref="SettingStrategy"/> where settings are stored.</param>
        /// <returns>Readed value. If setting is not set or value is null, returns otherwise value.</returns>
        [return: NotNullIfNotNull("otherwise")]
        string? GetSetting(
            string container,
            string key,
            string? otherwise = null,
            SettingStrategy location = SettingStrategy.Local);

        /// <summary>
        /// Sets settings to container.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="container">Container to set setting value.</param>
        /// <param name="key">Setting key.</param>
        /// <param name="value">Setting value.</param>
        /// <param name="location"><see cref="SettingStrategy"/> where settings are stored.</param>
        /// <returns>True, if successful.</returns>
        bool SetSetting<T>(
            string container,
            string key,
            T value,
            SettingStrategy location = SettingStrategy.Local)
            where T : unmanaged;

        /// <summary>
        /// Sets settings to container.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="container">Container to set setting value.</param>
        /// <param name="key">Setting key.</param>
        /// <param name="value">Setting value.</param>
        /// <param name="location"><see cref="SettingStrategy"/> where settings are stored.</param>
        /// <returns>True, if successful.</returns>
        bool SetSetting<T>(
            string container,
            string key,
            T? value,
            SettingStrategy location = SettingStrategy.Local)
            where T : unmanaged;

        /// <summary>
        /// Sets settings to container.
        /// </summary>
        /// <param name="container">Container to set setting value.</param>
        /// <param name="key">Setting key.</param>
        /// <param name="value">Setting value.</param>
        /// <param name="location"><see cref="SettingStrategy"/> where settings are stored.</param>
        /// <returns>True, if successful.</returns>
        bool SetSetting(
            string container,
            string key,
            string? value,
            SettingStrategy location = SettingStrategy.Local);

        /// <summary>
        /// Deletes setting from container.
        /// </summary>
        /// <param name="container">Container to delete from.</param>
        /// <param name="key">Setting key.</param>
        /// <param name="location"><see cref="SettingStrategy"/> where settings are stored.</param>
        /// <returns>True, if successful.</returns>
        bool DeleteSetting(
            string container,
            string key,
            SettingStrategy location = SettingStrategy.Local);
    }
}
