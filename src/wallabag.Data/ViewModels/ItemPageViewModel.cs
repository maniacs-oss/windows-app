﻿using GalaSoft.MvvmLight.Command;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using wallabag.Api;
using wallabag.Data.Common;
using wallabag.Data.Common.Helpers;
using wallabag.Data.Interfaces;
using wallabag.Data.Services;

namespace wallabag.Data.ViewModels
{
    public class ItemPageViewModel : ViewModelBase
    {
        private readonly IOfflineTaskService _offlineTaskService;
        private readonly ILoggingService _loggingService;
        private readonly IPlatformSpecific _device;
        private readonly INavigationService _navigationService;
        private readonly IWallabagClient _client;
        private readonly SQLite.Net.SQLiteConnection _database;

        public ItemViewModel Item { get; set; }
        public string FormattedHtml { get; set; }

        public bool ErrorDuringInitialization { get; set; }
        public string ErrorDescription { get; set; }

        public ICommand ChangeReadStatusCommand { get; private set; }
        public ICommand ChangeFavoriteStatusCommand { get; private set; }
        public ICommand EditTagsCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }

        public int FontSize { get; set; } = Settings.Appereance.FontSize;
        public string FontFamily { get; set; } = Settings.Appereance.FontFamily;
        public string ColorScheme { get; set; } = Settings.Appereance.ColorScheme;
        public string TextAlignment { get; set; } = Settings.Appereance.TextAlignment;

        public Uri RightClickUri { get; set; }
        public ICommand SaveRightClickLinkCommand { get; private set; }
        public ICommand OpenRightClickLinkInBrowserCommand { get; private set; }
        public string ReadingProgressContainerName => $"ReadingProgressContainer-{Settings.Authentication.ClientId}";

        public ItemPageViewModel(
            IOfflineTaskService offlineTaskService,
            ILoggingService loggingService,
            IPlatformSpecific device,
            INavigationService navigationService,
            IWallabagClient client,
            SQLite.Net.SQLiteConnection database)
        {
            _offlineTaskService = offlineTaskService;
            _loggingService = loggingService;
            _device = device;
            _navigationService = navigationService;
            _client = client;
            _database = database;

            _loggingService.WriteLine($"Initializing new instance of {nameof(ItemPageViewModel)}.");

            ChangeReadStatusCommand = new RelayCommand(() => ChangeReadStatus());
            ChangeFavoriteStatusCommand = new RelayCommand(() => ChangeFavoriteStatus());
            EditTagsCommand = new RelayCommand(() => _navigationService.Navigate(Navigation.Pages.EditTagsPage, Item.Model.Id));
            DeleteCommand = new RelayCommand(() =>
            {
                _loggingService.WriteLine("Deleting the current item.");
                Item.DeleteCommand.Execute();
                _navigationService.GoBack();
            });

            SaveRightClickLinkCommand = new RelayCommand(() => _offlineTaskService.Add(RightClickUri.ToString(), new List<string>()));
            OpenRightClickLinkInBrowserCommand = new RelayCommand(() => _device.LaunchUri(RightClickUri));
        }

        private async Task GenerateFormattedHtmlAsync()
        {
            _loggingService.WriteLine("Generating the HTML.");
            string template = await _device.GetArticleTemplateAsync();

            _loggingService.WriteLineIf(string.IsNullOrEmpty(template), "The template is empty!", LoggingCategory.Critical);

            string accentColor = _device.AccentColorHexCode;
            var styleSheetBuilder = new StringBuilder();
            styleSheetBuilder.Append("<style>");
            styleSheetBuilder.Append("hr {border-color: " + accentColor + " !important}");
            styleSheetBuilder.Append("::selection,mark {background: " + accentColor + " !important}");
            styleSheetBuilder.Append("body {");
            styleSheetBuilder.Append($"font-size: {FontSize}px;");
            styleSheetBuilder.Append($"text-align: {TextAlignment};}}");
            styleSheetBuilder.Append("</style>");

            string imageHeader = string.Empty;

            if (Item?.Model?.Hostname?.Contains("youtube.com") == false &&
                Item?.Model?.Hostname?.Contains("vimeo.com") == false &&
                Item?.Model?.PreviewImageUri != null)
            {
                _loggingService.WriteLine($"Image header is set to: {Item.Model.PreviewImageUri.ToString()}");
                imageHeader = Item.Model.PreviewImageUri.ToString();
            }
            else
            {
                _loggingService.WriteLine("Image header is empty.");
            }

            _loggingService.WriteLine("Formatting the template with the item properties.");
            FormattedHtml = template.FormatWith(new
            {
                title = Item.Model.Title,
                content = await SetupArticleForHtmlViewerAsync(),
                articleUrl = Item.Model.Url,
                hostname = Item.Model.Hostname,
                color = ColorScheme,
                font = FontFamily,
                progress = Item.Model.ReadingProgress,
                publishDate = string.Format("{0:d}", Item.Model.CreationDate),
                stylesheet = styleSheetBuilder.ToString(),
                imageHeader = imageHeader
            });
        }

        private async Task<string> SetupArticleForHtmlViewerAsync()
        {
            _loggingService.WriteLine("Preparing HTML.");
            var document = new HtmlDocument();
            document.LoadHtml(Item.Model.Content);
            document.OptionCheckSyntax = false;

            // Implement lazy-loading for images
            _loggingService.WriteLine("Implementing lazy-loading for images...");
            foreach (var node in document.DocumentNode.Descendants("img"))
            {
                if (node.HasAttributes && node.Attributes["src"] != null)
                {
                    string oldSource = node.Attributes["src"].Value;
                    node.Attributes.RemoveAll();

                    _loggingService.WriteLine($"Source of the image: {oldSource}");

                    if (!oldSource.Equals(Item.Model.PreviewImageUri?.ToString()) &&
                        _device.InternetConnectionIsAvailable)
                    {
                        node.Attributes.Add("src", "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
                        node.Attributes.Add("data-src", oldSource);
                        node.Attributes.Add("class", "lazy");
                    }

                    node.InnerHtml = " "; // dirty hack to let HtmlAgilityPack close the <img> tag
                }
            }

            _loggingService.WriteLine("Add thumbnails for embedded videos...");
            string dataOpenMode = Settings.Reading.VideoOpenMode.ToString().ToLower();
            string containerString = "<div class='wallabag-video' style='background-image: url({0})' data-open-mode='" + dataOpenMode + "' data-provider='{1}' data-video-id='{2}'><span></span></div>";

            // Replace videos (YouTube & Vimeo) by static thumbnails                  
            var iframeNodes = document.DocumentNode.Descendants("iframe").ToList();
            var videoNodes = document.DocumentNode.Descendants("video").ToList();

            _loggingService.WriteLine($"Number of <iframe>'s: {iframeNodes.Count}");
            _loggingService.WriteLine($"Number of <video>'s: {videoNodes.Count}");

            _loggingService.WriteLine("Handling iframes...");
            foreach (var node in iframeNodes)
            {
                if (node.HasAttributes &&
                    node.Attributes.Contains("src"))
                {
                    var videoSourceUri = new Uri(node.Attributes["src"].Value);
                    string videoId = videoSourceUri.Segments.Last();
                    string videoProvider = videoSourceUri.Host;

                    if (videoProvider.Contains("youtube.com"))
                        videoProvider = "youtube";
                    else if (videoProvider.Contains("player.vimeo.com"))
                        videoProvider = "vimeo";

                    _loggingService.WriteLine($"Video source: {videoSourceUri}");
                    _loggingService.WriteLine($"Video ID: {videoId}");
                    _loggingService.WriteLine($"Video provider: {videoProvider}");
                    string newContainer = string.Format(containerString, await GetPreviewImageForVideoAsync(videoProvider, videoId), videoProvider, videoId);

                    node.ParentNode.InsertAfter(HtmlNode.CreateNode(newContainer), node);
                    node.ParentNode.RemoveChild(node);
                }
            }

            // This loop is for HTML5 videos using the <video> tag
            _loggingService.WriteLine("Handling video tags...");
            foreach (var node in videoNodes)
            {
                string videoSource = string.Empty;

                videoSource = node.GetAttributeValue("src", string.Empty);

                if (string.IsNullOrEmpty(videoSource) && node.HasChildNodes)
                    videoSource = node.ChildNodes
                          .Where(i => i.Name.Equals("source") && i.GetAttributeValue("type", string.Empty).Equals("video/mp4"))
                          .FirstOrDefault()
                          ?.GetAttributeValue("src", string.Empty);

                _loggingService.WriteLine($"Video source: {videoSource}");
                if (!string.IsNullOrEmpty(videoSource))
                {
                    string newContainer = string.Format(containerString, string.Empty, "html", videoSource);

                    node.ParentNode.InsertAfter(HtmlNode.CreateNode(newContainer), node);
                    node.ParentNode.RemoveChild(node);
                }
            }

            return document.DocumentNode.OuterHtml;
        }

        public async Task<string> GetPreviewImageForVideoAsync(string videoProvider, string videoId)
        {
            string result = string.Empty;

            _loggingService.WriteLine($"Getting preview image for video {videoId} from {videoProvider}");

            if (videoProvider == "youtube")
                result = $"http://img.youtube.com/vi/{videoId}/0.jpg";
            else
            {
                string link = $"http://vimeo.com/api/v2/video/{videoId}.json";
                _loggingService.WriteLine($"Contacting vimeo for preview image: {link}");

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(new Uri(link));
                    _loggingService.WriteObject(response);

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                        _loggingService.WriteLine($"Resulted JSON: {json}");
                        result = json[0].thumbnail_large.Value;
                    }
                }
            }

            _loggingService.WriteLine($"Final result: {result}");
            return result;
        }

        private void ChangeReadStatus()
        {
            _loggingService.WriteLine("Changing read status of item.");
            if (Item.Model.IsRead)
                Item.UnmarkAsReadCommand.Execute();
            else
            {
                Item.MarkAsReadCommand.Execute();

                if (Settings.Reading.NavigateBackAfterReadingAnArticle)
                {
                    _loggingService.WriteLine("Navigating back to main page.");
                    _navigationService.GoBack();
                }

                if (Settings.Reading.SyncReadingProgress)
                {
                    _loggingService.WriteLine($"Deleting reading progress.");
                    Settings.SettingsService.Remove(Item.Model.Id.ToString(), SettingStrategy.Roaming, ReadingProgressContainerName);
                }
            }
        }
        private void ChangeFavoriteStatus()
        {
            _loggingService.WriteLine("Changing favorite status of item.");
            if (Item.Model.IsStarred)
                Item.UnmarkAsStarredCommand.Execute();
            else
                Item.MarkAsStarredCommand.Execute();
        }

        public override async Task OnNavigatedToAsync(object parameter, IDictionary<string, object> state)
        {
            _loggingService.WriteLine($"Navigation parameter: {parameter}");
            Item = ItemViewModel.FromId(
                (int)parameter,
                _loggingService,
                _database,
                _offlineTaskService,
                _navigationService,
                _device);

            _loggingService.WriteLineIf(Item == null, "Item is null.", LoggingCategory.Warning);
            _loggingService.WriteLine($"Item title: {Item?.Model?.Title}");

            if ((Item == null || string.IsNullOrEmpty(Item?.Model?.Content)) && _device.InternetConnectionIsAvailable)
            {
                _loggingService.WriteLine("Fetching item from server.");
                var item = await _client.GetItemAsync(Item.Model.Id);
                if (item != null)
                {
                    Item = new ItemViewModel(item, _offlineTaskService, _navigationService, _loggingService, _device, _database);
                }

                _loggingService.WriteLine($"Success: {item != null}");
                _loggingService.WriteObject(item);
            }

            if (string.IsNullOrEmpty(Item?.Model?.Content))
            {
                _loggingService.WriteLine("No content available.", LoggingCategory.Warning);
                ErrorDuringInitialization = true;
                ErrorDescription = _device.GetLocalizedResource("NoContentAvailableErrorMessage");
            }
            else if (Item?.Model?.Content?.Contains("wallabag can't retrieve contents for this article.") == true)
            {
                _loggingService.WriteLine("wallabag can't retrieve content.", LoggingCategory.Warning);
                ErrorDuringInitialization = true;
                ErrorDescription = _device.GetLocalizedResource("CantRetrieveContentsErrorMessage");
            }

            if (ErrorDuringInitialization)
                return;

            if (Settings.Reading.SyncReadingProgress && Item.Model.ReadingProgress < 100)
            {
                _loggingService.WriteLine("Fetching reading progress from roaming settings.");

                if (Settings.SettingsService.Contains(Item.Model.Id.ToString(), SettingStrategy.Roaming, ReadingProgressContainerName))
                    Item.Model.ReadingProgress = Settings.SettingsService.GetValueOrDefault<double>(Item.Model.Id.ToString(), strategy: SettingStrategy.Roaming, containerName: ReadingProgressContainerName);

                _loggingService.WriteLine($"Reading progress: {Item.Model.ReadingProgress}");
            }

            await GenerateFormattedHtmlAsync();
        }
        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState)
        {
            Settings.Appereance.FontSize = FontSize;
            Settings.Appereance.FontFamily = FontFamily;
            Settings.Appereance.ColorScheme = ColorScheme;
            Settings.Appereance.TextAlignment = TextAlignment;

            _loggingService.WriteLine("Updating item in database.");
            _database.Update(Item.Model);

            if (Settings.Reading.SyncReadingProgress && Item.Model.ReadingProgress < 100)
            {
                _loggingService.WriteLine("Setting reading progress in RoamingSettings.");
                Settings.SettingsService.AddOrUpdateValue(Item.Model.Id.ToString(), Item.Model.ReadingProgress, SettingStrategy.Roaming, ReadingProgressContainerName);
            }

            return Task.FromResult(true);
        }
    }
}
