namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    using Windows.Security.Credentials;
    using Windows.Storage;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;
    using System.Diagnostics.CodeAnalysis;

    /// <inheritdoc/>
    public class SettingService : ISettingService
    {
        private readonly PasswordVault? passwordVault;

        public SettingService()
        {
            try
            {
                passwordVault = new PasswordVault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public string RootContainer => "__GlobalSettings";

        public bool DeleteSetting(string container, string key, SettingStrategy location = SettingStrategy.Local)
        {
            try
            {
                switch (location)
                {
                    case SettingStrategy.Local when container == RootContainer:
                        return ApplicationData.Current.LocalSettings.Values.Remove(key);
                    case SettingStrategy.Local:
                        if (ApplicationData.Current.LocalSettings.Values[container] is ApplicationDataCompositeValue
                            localComposite && localComposite.ContainsKey(key))
                        {
                            var result = localComposite.Remove(key);
                            if (localComposite.Count == 0)
                            {
                                result &= ApplicationData.Current.LocalSettings.Values.Remove(container);
                            }
                            else
                            {
                                ApplicationData.Current.LocalSettings.Values[container] = localComposite;
                            }

                            return result;
                        }

                        break;
                    case SettingStrategy.Roaming when container == RootContainer:
                        return ApplicationData.Current.RoamingSettings.Values.Remove(key);
                    case SettingStrategy.Roaming:
                        if (ApplicationData.Current.RoamingSettings.Values[container] is ApplicationDataCompositeValue
                            roamingComposite && roamingComposite.ContainsKey(key))
                        {
                            var result = roamingComposite.Remove(key);
                            if (roamingComposite.Count == 0)
                            {
                                result &= ApplicationData.Current.RoamingSettings.Values.Remove(container);
                            }
                            else
                            {
                                ApplicationData.Current.RoamingSettings.Values[container] = roamingComposite;
                            }

                            return result;
                        }

                        break;
                    case SettingStrategy.Secure:
                        if (passwordVault == null)
                        {
                            return false;
                        }

                        var value = passwordVault
                            .RetrieveAll()
                            .FirstOrDefault(c => c.Resource == container && c.UserName == key);
                        if (value != null)
                        {
                            passwordVault.Remove(value);
                            return true;
                        }

                        break;
                    default:
                        throw new NotSupportedException($"DeleteSetting {location} not supported");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);
            }

            return false;
        }

        public IReadOnlyDictionary<string, object> GetAllRawSettings(string container,
            SettingStrategy location = SettingStrategy.Local)
        {
            try
            {
                switch (location)
                {
                    case SettingStrategy.Local when container == RootContainer:
                        return ApplicationData.Current.LocalSettings.Values
                            .Where(v => !(v.Value is ApplicationDataCompositeValue))
                            .ToDictionary(p => p.Key, p => p.Value);
                    case SettingStrategy.Local:
                        if (ApplicationData.Current.LocalSettings.Values[container] is ApplicationDataCompositeValue
                            localComposite)
                        {
                            return localComposite.ToDictionary(v => v.Key, v => v.Value);
                        }

                        break;
                    case SettingStrategy.Roaming when container == RootContainer:
                        return ApplicationData.Current.RoamingSettings.Values
                            .Where(v => !(v.Value is ApplicationDataCompositeValue))
                            .ToDictionary(p => p.Key, p => p.Value);
                    case SettingStrategy.Roaming:
                        if (ApplicationData.Current.RoamingSettings.Values[container] is ApplicationDataCompositeValue
                            roamingComposite)
                        {
                            return roamingComposite.ToDictionary(v => v.Key, v => v.Value);
                        }

                        break;
                    case SettingStrategy.Secure:
                        return passwordVault?
                                   .RetrieveAll()
                                   .Where(c => c.Resource == container)
                                   .ToDictionary(
                                       v => v.UserName,
                                       v =>
                                       {
                                           v.RetrievePassword();
                                           return (object)v.Password;
                                       })
                               ?? new Dictionary<string, object>();
                    default:
                        throw new NotSupportedException($"GetAllSettings {location} not supported");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);
            }

            return new Dictionary<string, object>();
        }

        public bool SettingExists(string container, string key, SettingStrategy location = SettingStrategy.Local)
        {
            try
            {
                switch (location)
                {
                    case SettingStrategy.Local when container == RootContainer:
                        return ApplicationData.Current.LocalSettings.Values.ContainsKey(key);
                    case SettingStrategy.Local:
                        if (ApplicationData.Current.LocalSettings.Values[container] is ApplicationDataCompositeValue
                            localComposite)
                        {
                            return localComposite.ContainsKey(key);
                        }

                        break;
                    case SettingStrategy.Roaming when container == RootContainer:
                        return ApplicationData.Current.RoamingSettings.Values.ContainsKey(key);
                    case SettingStrategy.Roaming:
                        if (ApplicationData.Current.RoamingSettings.Values[container] is ApplicationDataCompositeValue
                            roamingComposite)
                        {
                            return roamingComposite.ContainsKey(key);
                        }

                        break;
                    case SettingStrategy.Secure:
                        return passwordVault?.RetrieveAll().Any(v => v.Resource == container && v.UserName == key) ??
                               false;
                    default:
                        throw new NotSupportedException($"SettingExists {location} not supported");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);
            }

            return false;
        }

        public void Clear(string container, SettingStrategy location = SettingStrategy.Local)
        {
            try
            {
                switch (location)
                {
                    case SettingStrategy.Local when container == RootContainer:
                        foreach (var item in ApplicationData.Current.LocalSettings.Values)
                        {
                            if (!(item.Value is ApplicationDataContainer))
                            {
                                _ = ApplicationData.Current.LocalSettings.Values.Remove(item.Key);
                            }
                        }

                        break;
                    case SettingStrategy.Local:
                        if (ApplicationData.Current.LocalSettings.Values[container] is ApplicationDataCompositeValue
                            localComposite)
                        {
                            localComposite.Clear();
                            ApplicationData.Current.LocalSettings.Values[container] = localComposite;
                        }

                        break;
                    case SettingStrategy.Roaming when container == RootContainer:
                        foreach (var item in ApplicationData.Current.RoamingSettings.Values)
                        {
                            if (!(item.Value is ApplicationDataContainer))
                            {
                                _ = ApplicationData.Current.RoamingSettings.Values.Remove(item.Key);
                            }
                        }

                        break;
                    case SettingStrategy.Roaming:
                        if (ApplicationData.Current.RoamingSettings.Values[container] is ApplicationDataCompositeValue
                            roamingConmosite)
                        {
                            roamingConmosite.Clear();
                            ApplicationData.Current.RoamingSettings.Values[container] = roamingConmosite;
                        }

                        break;
                    case SettingStrategy.Secure:
                        if (passwordVault == null)
                        {
                            return;
                        }

                        foreach (var credentian in passwordVault.RetrieveAll().Where(c => c.Resource == container))
                        {
                            passwordVault.Remove(credentian);
                        }

                        break;
                    default:
                        throw new NotSupportedException($"Clear {location} not supported");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);
            }
        }

        public IReadOnlyList<string> GetAllContainers(SettingStrategy location = SettingStrategy.Local)
        {
            try
            {
                var rootContainer = new[] {RootContainer};
                return location switch
                {
                    SettingStrategy.Local => ApplicationData.Current.LocalSettings.Values
                        .Where(v => v.Value is ApplicationDataCompositeValue).Select(v => v.Key).Concat(rootContainer)
                        .ToList(),
                    SettingStrategy.Roaming => ApplicationData.Current.RoamingSettings.Values
                        .Where(v => v.Value is ApplicationDataCompositeValue).Select(v => v.Key).Concat(rootContainer)
                        .ToList(),
                    SettingStrategy.Secure when passwordVault is null => new List<string>(),
                    SettingStrategy.Secure => passwordVault.RetrieveAll().Select(c => c.Resource)
                        .Distinct().Concat(rootContainer).ToList(),

                    _ => throw new NotSupportedException($"GetAllContainers {location} not supported"),
                };
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);
            }

            return new List<string>();
        }

        public T GetSetting<T>(string container, string key, T otherwise = default,
            SettingStrategy location = SettingStrategy.Local) where T : unmanaged
        {
            return GetSettingInternal(container, key, otherwise, location);
        }

        public T? GetSetting<T>(string container, string key, T? otherwise = null,
            SettingStrategy location = SettingStrategy.Local) where T : unmanaged
        {
            return GetSettingInternal(container, key, otherwise, location);
        }

        public string? GetSetting(string container, string key, string? otherwise = null,
            SettingStrategy location = SettingStrategy.Local)
        {
            return GetSettingInternal(container, key, otherwise, location);
        }

        public bool SetSetting<T>(string container, string key, T value,
            SettingStrategy location = SettingStrategy.Local) where T : unmanaged
        {
            return SetSettingInternal(container, key, value, location);
        }

        public bool SetSetting<T>(string container, string key, T? value,
            SettingStrategy location = SettingStrategy.Local) where T : unmanaged
        {
            return SetSettingInternal(container, key, value, location);
        }

        public bool SetSetting(string container, string key, string? value,
            SettingStrategy location = SettingStrategy.Local)
        {
            return SetSettingInternal(container, key, value, location);
        }

        public int SetRawSettings(string container, IReadOnlyDictionary<string, object> settings,
            SettingStrategy location = SettingStrategy.Local)
        {
            var successCount = 0;
            foreach (var setting in settings)
            {
                if (SetSettingInternal(container, setting.Key, setting.Value, location))
                {
                    successCount++;
                }
            }

            return successCount;
        }

        [return: NotNullIfNotNull("otherwise")]
        private T? GetSettingInternal<T>(string container, string key, T? otherwise = default,
            SettingStrategy location = SettingStrategy.Local)
        {
            if (!SettingExists(container, key, location))
            {
                return otherwise;
            }

            try
            {
                switch (location)
                {
                    case SettingStrategy.Local when container == RootContainer:
                        return ObjectHelper.SafeCast(ApplicationData.Current.LocalSettings.Values[key], otherwise);
                    case SettingStrategy.Local:
                        if (ApplicationData.Current.LocalSettings.Values[container] is ApplicationDataCompositeValue
                            localComposite)
                        {
                            return ObjectHelper.SafeCast(localComposite[key], otherwise);
                        }

                        break;
                    case SettingStrategy.Roaming when container == RootContainer:
                        return ObjectHelper.SafeCast(ApplicationData.Current.RoamingSettings.Values[key], otherwise);
                    case SettingStrategy.Roaming:
                        if (ApplicationData.Current.RoamingSettings.Values[container] is ApplicationDataCompositeValue
                            roamingComposite)
                        {
                            return ObjectHelper.SafeCast(roamingComposite[key], otherwise);
                        }

                        break;
                    case SettingStrategy.Secure:
                        var value = passwordVault?
                            .RetrieveAll()
                            .FirstOrDefault(c => c.Resource == container && c.UserName == key);
                        if (value == null)
                        {
                            return otherwise;
                        }

                        value.RetrievePassword();
                        return ObjectHelper.SafeCast(value.Password, otherwise);
                    default:
                        throw new NotSupportedException($"GetSetting {location} not supported");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);
            }

            return otherwise;
        }

        private bool SetSettingInternal<T>(string container, string key, T value,
            SettingStrategy location = SettingStrategy.Local)
        {
            try
            {
                switch (location)
                {
                    case SettingStrategy.Local when container == RootContainer:
                        ApplicationData.Current.LocalSettings.Values[key] = value;
                        return true;
                    case SettingStrategy.Local:
                        if (ApplicationData.Current.LocalSettings.Values[container] is not ApplicationDataCompositeValue
                            localComposite)
                        {
                            if (ApplicationData.Current.LocalSettings.Values.ContainsKey(container))
                            {
                                return false;
                            }

                            localComposite = new ApplicationDataCompositeValue();
                        }

                        localComposite[key] = value;
                        ApplicationData.Current.LocalSettings.Values[container] = localComposite;
                        return true;
                    case SettingStrategy.Roaming when container == RootContainer:
                        ApplicationData.Current.RoamingSettings.Values[key] = value;
                        return true;
                    case SettingStrategy.Roaming:
                        if (ApplicationData.Current.RoamingSettings.Values[container] is not ApplicationDataCompositeValue
                            roamingComposite)
                        {
                            if (ApplicationData.Current.RoamingSettings.Values.ContainsKey(container))
                            {
                                return false;
                            }

                            roamingComposite = new ApplicationDataCompositeValue();
                        }

                        roamingComposite[key] = value;
                        ApplicationData.Current.RoamingSettings.Values[container] = roamingComposite;
                        return true;
                    case SettingStrategy.Secure:
                        if (passwordVault == null)
                        {
                            return false;
                        }

                        if (SettingExists(container, key, location))
                        {
                            _ = DeleteSetting(container, key, location);
                        }

                        if (value != null)
                        {
                            passwordVault.Add(new PasswordCredential(container, key, value.ToString()));
                        }

                        return true;
                    default:
                        throw new NotSupportedException($"SetSetting {location} not supported");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);
            }

            return false;
        }
    }
}
