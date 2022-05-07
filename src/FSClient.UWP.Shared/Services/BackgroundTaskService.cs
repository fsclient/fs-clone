namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using Windows.ApplicationModel.Background;

    using FSClient.Shared;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    using Microsoft.Extensions.Logging;

    public class BackgroundTaskService
    {
        public const string AutomatedBackupTaskName = "AutomatedBackupTask";
        public const uint AutomatedBackupMinutesInterval = 60 * 24; // once a day
        public const string AutomatedBackupTaskEntryPoint = "FSClient.UWP.Background.Tasks.AutomatedBackupTask";

        public const string MirrorUpdaterTaskName = "MirrorUpdaterTask";
        public const uint MirrorUpdaterMinutesInterval = 60 * 24; // once a day
        public const string MirrorUpdaterTaskEntryPoint = "FSClient.UWP.Background.Tasks.MirrorUpdaterTask";

        private readonly ILogger logger;

        public BackgroundTaskService(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<bool> UpdateTaskRegistration()
        {
            try
            {
                return await DispatcherHelper
                    .GetForCurrentOrMainView()
                    .CheckBeginInvokeOnUI(async () =>
                    {
                        BackgroundExecutionManager.RemoveAccess();
                        var access = await BackgroundExecutionManager.RequestAccessAsync();
                        if (access != BackgroundAccessStatus.AlwaysAllowed
                            && access != BackgroundAccessStatus.AllowedSubjectToSystemPolicy)
                        {
                            return false;
                        }

                        return EnsureAutomatedBackupTaskRegistration()
                               & EnsureMirrorUpdaterTaskRegistration();
                    });
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex);
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }

            return false;
        }

        private static bool EnsureAutomatedBackupTaskRegistration()
        {
            var registration =
                BackgroundTaskRegistration.AllTasks.Values.FirstOrDefault(r => r.Name == AutomatedBackupTaskName);

            if (Settings.Instance.AutomatedBackupTaskEnabled)
            {
                if (registration != null)
                {
                    return true;
                }

                var builder = new BackgroundTaskBuilder();
                builder.Name = AutomatedBackupTaskName;
                builder.TaskEntryPoint = AutomatedBackupTaskEntryPoint;
                builder.SetTrigger(new TimeTrigger(AutomatedBackupMinutesInterval, false));
                registration = builder.Register();
                return registration != null;
            }
            else
            {
                if (registration != null)
                {
                    registration.Unregister(false);
                }

                return true;
            }
        }

        private static bool EnsureMirrorUpdaterTaskRegistration()
        {
            var registration =
                BackgroundTaskRegistration.AllTasks.Values.FirstOrDefault(r => r.Name == MirrorUpdaterTaskName);

            if (registration != null)
            {
                return true;
            }

            var builder = new BackgroundTaskBuilder();
            builder.Name = MirrorUpdaterTaskName;
            builder.TaskEntryPoint = MirrorUpdaterTaskEntryPoint;
            builder.SetTrigger(new TimeTrigger(MirrorUpdaterMinutesInterval, false));
            registration = builder.Register();
            return registration != null;
        }
    }
}
