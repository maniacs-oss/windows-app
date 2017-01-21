﻿using System;
using System.Threading.Tasks;
using wallabag.Services;
using Windows.ApplicationModel.Background;

namespace wallabag.Common.Helpers
{
    public static class BackgroundTaskHelper
    {
        private const string _backgroundTaskName = "wallabagBackgroundSync";
        private static IBackgroundTaskRegistration _backgroundTask;

        public static async Task RegisterBackgroundTaskAsync()
        {
            if (IsSupportedOnDevice == false)
                return;

            await BackgroundExecutionManager.RequestAccessAsync();

            var builder = new BackgroundTaskBuilder()
            {
                Name = _backgroundTaskName,
                IsNetworkRequested = true
            };
            builder.SetTrigger(new TimeTrigger((uint)SettingsService.Instance.BackgroundTaskExecutionInterval, false));

            _backgroundTask = builder.Register();
        }
        public static void UnregisterBackgroundTask()
        {
            if (BackgroundTaskIsRegistered)
                _backgroundTask.Unregister(false);
        }

        public static bool BackgroundTaskIsRegistered
        {
            get
            {
                if (IsSupportedOnDevice == false)
                    return false;

                bool taskRegistered = false;
                foreach (var task in BackgroundTaskRegistration.AllTasks)
                {
                    if (task.Value.Name == _backgroundTaskName)
                    {
                        taskRegistered = true;
                        _backgroundTask = task.Value;
                        break;
                    }
                }
                return taskRegistered;
            }
        }
        public static bool IsSupportedOnDevice => Windows.Foundation.Metadata.ApiInformation.IsMethodPresent(typeof(Windows.UI.Xaml.Application).FullName, "OnBackgroundActivated");
    }
}
