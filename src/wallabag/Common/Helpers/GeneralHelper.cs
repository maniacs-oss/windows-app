﻿using System;
using Windows.ApplicationModel.Resources;
using Windows.System.Profile;
using WindowsStateTriggers;

namespace wallabag.Common.Helpers
{
    class GeneralHelper
    {
        public static bool IsPhone { get { return DeviceFamilyOfCurrentDevice == DeviceFamily.Mobile; } }

        public static string LocalizedResource(string resourceName)
        {
            return ResourceLoader.GetForCurrentView().GetString(resourceName.Replace(".", "/"));
        }

        public static bool InternetConnectionIsAvailable
        {
            get
            {
                var profile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
                return profile?.GetNetworkConnectivityLevel() == Windows.Networking.Connectivity.NetworkConnectivityLevel.InternetAccess;
            }
        }

        public static DeviceFamily DeviceFamilyOfCurrentDevice
        {
            get
            {
                return (DeviceFamily)Enum.Parse(typeof(DeviceFamily), AnalyticsInfo.VersionInfo.DeviceFamily.Replace("Windows.", string.Empty));
            }
        }

        public static int LastItemId => App.Database.ExecuteScalar<int>("select Max(ID) from 'Item'", new object[0]);
    }
}
