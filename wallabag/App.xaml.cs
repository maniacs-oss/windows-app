﻿using Newtonsoft.Json;
using SQLite.Net;
using SQLite.Net.Platform.WinRT;
using System;
using System.Threading.Tasks;
using Template10.Common;
using wallabag.Models;
using wallabag.Services;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel;

namespace wallabag
{
    public sealed partial class App : BootStrapper
    {
        public static Api.WallabagClient Client { get; private set; }
        public static SQLiteConnection Database { get; private set; }
        public static SettingsService Settings { get; private set; }

        public App() { InitializeComponent(); }

        public override async Task OnStartAsync(StartKind startKind, IActivatedEventArgs args)
        {
            var path = (await Windows.Storage.ApplicationData.Current.LocalCacheFolder.CreateFileAsync("wallabag.db", Windows.Storage.CreationCollisionOption.OpenIfExists)).Path;
            await Task.Factory.StartNew(() =>
            {
                Database = new SQLiteConnection(new SQLitePlatformWinRT(), path, serializer: new CustomBlobSerializer());
                Database.CreateTable<Item>();
                Database.CreateTable<Tag>();
            });

            Settings = SettingsService.Instance;
            if (string.IsNullOrEmpty(Settings.ClientId) || string.IsNullOrEmpty(Settings.ClientSecret))
            {
                Client = new Api.WallabagClient(null,string.Empty,string.Empty);
                NavigationService.Navigate(typeof(Views.LoginPage));
            }
            else
            {
                Client = new Api.WallabagClient(Settings.WallabagUrl, Settings.ClientId, Settings.ClientSecret);
                Client.AccessToken = Settings.AccessToken;
                Client.RefreshToken = Settings.RefreshToken;
                Client.LastTokenRefreshDateTime = Settings.LastTokenRefreshDateTime;

                NavigationService.Navigate(typeof(Views.MainPage));
            }
        }

        public override async Task OnSuspendingAsync(object s, SuspendingEventArgs e, bool prelaunchActivated)
        {
            e.SuspendingOperation.GetDeferral();

            Settings.AccessToken = Client.AccessToken;
            Settings.RefreshToken = Client.RefreshToken;
            Settings.LastTokenRefreshDateTime = Client.LastTokenRefreshDateTime;

            await NavigationService.SaveNavigationAsync();
            Database.Close();
        }

        public override void OnResuming(object s, object e, AppExecutionState previousExecutionState)
        {
            Client.AccessToken = Settings.AccessToken;
            Client.RefreshToken = Settings.RefreshToken;
            Client.LastTokenRefreshDateTime = Settings.LastTokenRefreshDateTime;

            if (previousExecutionState == AppExecutionState.Suspended)
                NavigationService.RestoreSavedNavigationAsync();
        }

        public class CustomBlobSerializer : IBlobSerializer
        {
            private JsonSerializerSettings _serializerSettings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.All,
                TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Full
            };

            public bool CanDeserialize(Type type) => true;

            public object Deserialize(byte[] data, Type type)
            {
                var str = System.Text.Encoding.UTF8.GetString(data);

                if (type == typeof(Uri))
                    return new Uri(str.Replace("\"", string.Empty));
                else
                    return JsonConvert.DeserializeObject(str, _serializerSettings);
            }

            public byte[] Serialize<T>(T obj)
            {
                if (typeof(T) == typeof(Uri))
                    return System.Text.Encoding.UTF8.GetBytes(obj.ToString());
                else
                    return System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj, _serializerSettings));
            }
        }
    }
}
