﻿using GalaSoft.MvvmLight.Messaging;
using Newtonsoft.Json;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Template10.Mvvm;
using wallabag.Common;
using wallabag.Common.Messages;
using wallabag.Models;
using wallabag.Services;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace wallabag.ViewModels
{
    [ImplementPropertyChanged]
    public class MainViewModel : ViewModelBase
    {
        public IncrementalObservableCollection<ItemViewModel> Items { get; set; }

        [DependsOn(nameof(OfflineTaskCount))]
        public Visibility OfflineTaskVisibility { get { return OfflineTaskCount > 0 ? Visibility.Visible : Visibility.Collapsed; } }
        public int OfflineTaskCount { get; set; }
        public bool IsSyncing { get; set; }

        public bool ItemsCountIsZero { get { return Items.Count == 0; } }
        public bool IsSearchActive { get; set; } = false;
        public string PageHeader { get; set; } = Helpers.LocalizedResource("SearchBox.PlaceholderText").ToUpper();

        public bool? SortByCreationDate
        {
            get { return CurrentSearchProperties.SortType == SearchProperties.SearchPropertiesSortType.ByCreationDate; }
            set { CurrentSearchProperties.SortType = SearchProperties.SearchPropertiesSortType.ByCreationDate; }
        }
        public bool? SortByReadingTime
        {
            get { return CurrentSearchProperties.SortType == SearchProperties.SearchPropertiesSortType.ByReadingTime; }
            set { CurrentSearchProperties.SortType = SearchProperties.SearchPropertiesSortType.ByReadingTime; }
        }
        public DelegateCommand<string> SetSortTypeFilterCommand { get; private set; }
        public DelegateCommand<string> SetSortOrderCommand { get; private set; }

        public DelegateCommand SyncCommand { get; private set; }
        public DelegateCommand AddCommand { get; private set; }
        public DelegateCommand NavigateToSettingsPageCommand { get; private set; }
        public DelegateCommand<SelectionChangedEventArgs> PivotSelectionChangedCommand { get; private set; }

        public SearchProperties CurrentSearchProperties { get; private set; } = new SearchProperties();
        public ObservableCollection<Item> SearchQuerySuggestions { get; set; } = new ObservableCollection<Item>();
        public ObservableCollection<Language> LanguageSuggestions { get; set; } = new ObservableCollection<Language>();
        public ObservableCollection<Tag> TagSuggestions { get; set; } = new ObservableCollection<Tag>();
        public DelegateCommand<AutoSuggestBoxTextChangedEventArgs> SearchQueryChangedCommand { get; private set; }
        public DelegateCommand<AutoSuggestBoxQuerySubmittedEventArgs> SearchQuerySubmittedCommand { get; private set; }
        public DelegateCommand CloseSearchCommand { get; private set; }
        public DelegateCommand<SelectionChangedEventArgs> LanguageCodeChangedCommand { get; private set; }
        public DelegateCommand<SelectionChangedEventArgs> TagChangedCommand { get; private set; }
        public DelegateCommand ResetFilterLanguageCommand { get; private set; }
        public DelegateCommand ResetFilterTagCommand { get; private set; }
        public DelegateCommand ResetFilterCommand { get; private set; }

        public MainViewModel()
        {
            AddCommand = new DelegateCommand(async () => await DialogService.ShowAsync(DialogService.Dialog.AddItem));
            SyncCommand = new DelegateCommand(async () => await SyncAsync());
            NavigateToSettingsPageCommand = new DelegateCommand(() => NavigationService.Navigate(typeof(Views.SettingsPage), infoOverride: new DrillInNavigationTransitionInfo()));

            SetSortTypeFilterCommand = new DelegateCommand<string>(filter => SetSortTypeFilter(filter));
            SetSortOrderCommand = new DelegateCommand<string>(order => SetSortOrder(order));
            SearchQueryChangedCommand = new DelegateCommand<AutoSuggestBoxTextChangedEventArgs>(args => SearchQueryChanged(args));
            SearchQuerySubmittedCommand = new DelegateCommand<AutoSuggestBoxQuerySubmittedEventArgs>(async args => await SearchQuerySubmittedAsync(args));
            CloseSearchCommand = new DelegateCommand(() => EndSearchAsync(this, null));
            LanguageCodeChangedCommand = new DelegateCommand<SelectionChangedEventArgs>(args => LanguageCodeChanged(args));
            TagChangedCommand = new DelegateCommand<SelectionChangedEventArgs>(args => TagChanged(args));
            ResetFilterLanguageCommand = new DelegateCommand(() => CurrentSearchProperties.Language = null);
            ResetFilterTagCommand = new DelegateCommand(() => CurrentSearchProperties.Tag = null);
            ResetFilterCommand = new DelegateCommand(() => CurrentSearchProperties.Reset());

            CurrentSearchProperties.SearchStarted += p => StartSearch();
            CurrentSearchProperties.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName != nameof(CurrentSearchProperties.Query))
                    await ReloadViewAsync();

                RaisePropertyChanged(nameof(SortByCreationDate));
                RaisePropertyChanged(nameof(SortByReadingTime));
            };

            Items = new IncrementalObservableCollection<ItemViewModel>(async count => await LoadMoreItemsAsync(count));

            App.OfflineTaskAdded += App_OfflineTaskAdded;
            App.OfflineTaskRemoved += (s, e) => OfflineTaskCount -= 1;
            Items.CollectionChanged += (s, e) => RaisePropertyChanged(nameof(ItemsCountIsZero));
        }

        private async void App_OfflineTaskAdded(object sender, OfflineTask e)
        {
            if (_offlineTaskAreBlocked)
                return;

            ItemViewModel item = default(ItemViewModel);
            var orderAscending = CurrentSearchProperties.OrderAscending ?? false;

            if (e.Action != OfflineTask.OfflineTaskAction.Delete)
                item = new ItemViewModel(Item.FromId(e.ItemId));

            await CoreWindow.GetForCurrentThread().Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                switch (e.Action)
                {
                    case OfflineTask.OfflineTaskAction.MarkAsRead:
                        if (CurrentSearchProperties.ItemTypeIndex == 2)
                            Items.AddSorted(item, sortAscending: orderAscending);
                        else
                            Items.Remove(item);
                        break;
                    case OfflineTask.OfflineTaskAction.UnmarkAsRead:
                        if (CurrentSearchProperties.ItemTypeIndex == 2)
                            Items.Remove(item);
                        else
                            Items.AddSorted(item, sortAscending: orderAscending);
                        break;
                    case OfflineTask.OfflineTaskAction.MarkAsStarred: break;
                    case OfflineTask.OfflineTaskAction.UnmarkAsStarred:
                        if (CurrentSearchProperties.ItemTypeIndex == 1)
                            Items.Remove(item);
                        break;
                    case OfflineTask.OfflineTaskAction.EditTags: break;
                    case OfflineTask.OfflineTaskAction.AddItem:
                        if (CurrentSearchProperties.ItemTypeIndex == 0)
                            Items.AddSorted(item, sortAscending: orderAscending);
                        break;
                    case OfflineTask.OfflineTaskAction.Delete:
                        Items.Remove(Items.Where(i => i.Model.Id.Equals(e.ItemId)).First());
                        break;
                }
            });
            await e.ExecuteAsync();
        }

        private async Task<List<ItemViewModel>> LoadMoreItemsAsync(uint count)
        {
            var result = new List<ItemViewModel>();

            var database = await GetItemsForCurrentSearchPropertiesAsync(Items.Count, (int)count);

            foreach (var item in database)
                result.Add(new ItemViewModel(item));

            GetMetadataForItems(result);

            return result;
        }

        private async Task ExecuteOfflineTasksAsync()
        {
            OfflineTaskCount = App.Database.ExecuteScalar<int>("SELECT COUNT(*) FROM OfflineTask");

            if (OfflineTaskCount > 0)
            {
                foreach (var task in App.Database.Table<OfflineTask>())
                    await task.ExecuteAsync();
            }
        }
        private async Task SyncAsync()
        {
            if (Helpers.InternetConnectionIsAvailable == false)
                return;

            IsSyncing = true;
            await ExecuteOfflineTasksAsync();
            int syncLimit = 24;

            var items = await App.Client.GetItemsAsync(
                dateOrder: Api.WallabagClient.WallabagDateOrder.ByLastModificationDate,
                sortOrder: Api.WallabagClient.WallabagSortOrder.Descending,
                itemsPerPage: syncLimit);

            if (items != null)
            {
                var itemList = new List<Item>();

                foreach (var item in items)
                    itemList.Add(item);

                var databaseList = App.Database.Query<Item>($"SELECT Id FROM Item ORDER BY LastModificationDate DESC LIMIT 0,{syncLimit}", Array.Empty<object>());
                var deletedItems = databaseList.Except(itemList);

                App.Database.RunInTransaction(() =>
                {
                    foreach (var item in deletedItems)
                        App.Database.Delete(item);

                    App.Database.InsertOrReplaceAll(itemList);
                });

                if (databaseList[0].Equals(Items[0].Model) == false)
                    await ReloadViewAsync();
            }
            IsSyncing = false;
        }

        internal void ItemClick(object sender, ItemClickEventArgs args)
        {
            var item = args.ClickedItem as ItemViewModel;
            NavigationService.Navigate(typeof(Views.ItemPage), item.Model.Id);
        }

        private void UpdatePageHeader()
        {
            if (IsSearchActive)
                PageHeader = string.Format(Helpers.LocalizedResource("SearchPivotItem.Header").ToUpper(), "\"" + CurrentSearchProperties.Query + "\"");
            else
                PageHeader = Helpers.LocalizedResource("SearchBox.PlaceholderText").ToUpper();
        }

        private void SearchQueryChanged(AutoSuggestBoxTextChangedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(CurrentSearchProperties.Query))
                return;

            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var suggestions = App.Database.Table<Item>().Where(i => i.Title.ToLower().Contains(CurrentSearchProperties.Query)).Take(5);
                SearchQuerySuggestions.Replace(suggestions.ToList());
            }
        }
        private async Task SearchQuerySubmittedAsync(AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null)
            {
                NavigationService.Navigate(typeof(Views.ItemPage), (args.ChosenSuggestion as Item).Id);
                return;
            }

            if (string.IsNullOrWhiteSpace(args.QueryText))
            {
                CurrentSearchProperties.InvokeSearchCanceledEvent();
                return;
            }

            UpdatePageHeader();
            await ReloadViewAsync();
        }
        private void LanguageCodeChanged(SelectionChangedEventArgs args)
        {
            var selectedLanguage = args.AddedItems.FirstOrDefault() as Language;

            CurrentSearchProperties.Language = selectedLanguage as Language;
        }
        private void TagChanged(SelectionChangedEventArgs args)
        {
            var selectedTag = args.AddedItems.FirstOrDefault() as Tag;

            CurrentSearchProperties.Tag = selectedTag as Tag;
        }
        private void SetSortTypeFilter(string filter)
        {
            if (filter == "date")
                CurrentSearchProperties.SortType = SearchProperties.SearchPropertiesSortType.ByCreationDate;
            else
                CurrentSearchProperties.SortType = SearchProperties.SearchPropertiesSortType.ByReadingTime;
        }
        private void SetSortOrder(string order) => CurrentSearchProperties.OrderAscending = order == "asc";

        private int _previousItemTypeIndex;
        private bool _offlineTaskAreBlocked;

        private void StartSearch()
        {
            IsSearchActive = true;
            _previousItemTypeIndex = CurrentSearchProperties.ItemTypeIndex;
            SystemNavigationManager.GetForCurrentView().BackRequested += (s, e) => EndSearchAsync(s, e);
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
        }
        private async void EndSearchAsync(object sender, BackRequestedEventArgs e)
        {
            IsSearchActive = false;
            CurrentSearchProperties.ItemTypeIndex = _previousItemTypeIndex;

            SystemNavigationManager.GetForCurrentView().BackRequested -= (s, args) => EndSearchAsync(s, args);
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;

            if (e != null)
                e.Handled = true;

            CurrentSearchProperties.InvokeSearchCanceledEvent();

            if (!string.IsNullOrWhiteSpace(CurrentSearchProperties.Query))
            {
                CurrentSearchProperties.Query = string.Empty;
                await ReloadViewAsync();
            }

            UpdatePageHeader();
        }

        private async Task ReloadViewAsync()
        {
            var databaseItems = await GetItemsForCurrentSearchPropertiesAsync();
            await CoreWindow.GetForCurrentThread().Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
             {
                 Items.Clear();

                 foreach (var item in databaseItems)
                     Items.Add(new ItemViewModel(item));

                 GetMetadataForItems(Items);
             });
        }
        private void GetMetadataForItems(IEnumerable<ItemViewModel> items)
        {
            foreach (var item in items)
            {
                if (item.Model.Language != null)
                {
                    var translatedLanguage = new Language(item.Model.Language);

                    if (!LanguageSuggestions.Contains(translatedLanguage))
                        LanguageSuggestions.AddSorted(translatedLanguage, sortAscending: true);
                }
                else
                {
                    if (!LanguageSuggestions.Contains(Language.Unknown))
                        LanguageSuggestions.AddSorted(Language.Unknown, sortAscending: true);
                }

                foreach (var tag in item.Model.Tags)
                    if (!TagSuggestions.Contains(tag))
                        TagSuggestions.AddSorted(tag, sortAscending: true);
            }

            if (LanguageSuggestions.Contains(Language.Unknown))
                LanguageSuggestions.Move(LanguageSuggestions.IndexOf(Language.Unknown), 0);
        }
        private Task<List<Item>> GetItemsForCurrentSearchPropertiesAsync(int offset = 0, int limit = 24)
        {
            return Task.Factory.StartNew(() =>
            {
                var queryStart = "SELECT Id,Title,PreviewImageUri,Hostname,EstimatedReadingTime,Tags FROM Item";
                var queryParts = new List<string>();
                var queryParameters = new List<object>();

                if (string.IsNullOrWhiteSpace(CurrentSearchProperties.Query))
                {
                    if (CurrentSearchProperties.ItemTypeIndex == 0)
                    {
                        queryParts.Add("IsRead=?");
                        queryParameters.Add(0);
                    }
                    else if (CurrentSearchProperties.ItemTypeIndex == 1)
                    {
                        queryParts.Add("IsStarred=?");
                        queryParameters.Add(0);
                    }
                    else if (CurrentSearchProperties.ItemTypeIndex == 2)
                    {
                        queryParts.Add("IsRead=?");
                        queryParameters.Add(1);
                    }
                }

                if (!string.IsNullOrWhiteSpace(CurrentSearchProperties.Query))
                    queryParts.Add($"Title LIKE '%{CurrentSearchProperties.Query}%'");

                if (CurrentSearchProperties.Language?.IsUnknown == false)
                {
                    queryParts.Add("Language=?");
                    queryParameters.Add(CurrentSearchProperties.Language.wallabagLanguageCode);
                }
                else if (CurrentSearchProperties.Language?.IsUnknown == true)
                    queryParts.Add("Language IS NULL");

                if (CurrentSearchProperties.Tag != null)
                    queryParts.Add($"Tags LIKE '%{CurrentSearchProperties.Tag.Label}%'");

                var query = BuildSQLQuery(queryStart, queryParts);

                if (CurrentSearchProperties.SortType == SearchProperties.SearchPropertiesSortType.ByReadingTime)
                {
                    if (CurrentSearchProperties.OrderAscending == true)
                        query += " ORDER BY EstimatedReadingTime ASC";
                    else
                        query += " ORDER BY EstimatedReadingTime DESC";
                }
                else
                {
                    if (CurrentSearchProperties.OrderAscending == true)
                        query += " ORDER BY CreationDate ASC";
                    else
                        query += " ORDER BY CreationDate DESC";
                }

                query += " LIMIT ?,?";
                queryParameters.Add(offset);
                queryParameters.Add(limit);

                Items.MaxItems = App.Database.ExecuteScalar<int>(query.Replace(queryStart, "SELECT count(*) FROM Item"), queryParameters.ToArray());
                return App.Database.Query<Item>(query, queryParameters.ToArray());
            });
        }

        private string BuildSQLQuery(string start, List<string> queries)
        {
            string result = start;
            if (start.EndsWith(" ") == false)
                result += " ";

            foreach (var item in queries)
            {
                var queryIndex = queries.IndexOf(item);
                if (queryIndex == 0)
                    result += "WHERE " + item;
                else
                    result += "AND " + item;
                result += " ";
            }

            return result;
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await TitleBarExtensions.ResetAsync();

            if (mode != NavigationMode.Back && mode != NavigationMode.Forward)
            {
                if (state.ContainsKey(nameof(CurrentSearchProperties)))
                {
                    var stateValue = state[nameof(CurrentSearchProperties)] as string;
                    CurrentSearchProperties.Replace(await Task.Run(() => JsonConvert.DeserializeObject<SearchProperties>(stateValue)));
                }

                await ReloadViewAsync();

                if (SettingsService.Instance.SyncOnStartup)
                    await SyncAsync();
            }

            Messenger.Default.Register<BlockOfflineTaskExecutionMessage>(this, message =>
            {
                _offlineTaskAreBlocked = message.IsBlocked;
                if (_offlineTaskAreBlocked == false)
                {
                    var tasks = App.Database.Table<OfflineTask>().ToList();
                    foreach (var task in tasks)
                    {
                        App_OfflineTaskAdded(this, task);
                    }
                }
            });
        }
        public override async Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            var serializedSearchProperties = await Task.Run(() => JsonConvert.SerializeObject(CurrentSearchProperties));
            pageState[nameof(CurrentSearchProperties)] = serializedSearchProperties;

            Messenger.Default.Unregister(this);
        }
    }
}