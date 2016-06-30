﻿using GalaSoft.MvvmLight.Messaging;
using Newtonsoft.Json;
using PropertyChanged;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Template10.Mvvm;
using wallabag.Common;
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
        public string PageHeader { get; set; } = Helpers.LocalizedResource("UnreadPageTitleTextBlock.Text");

        public DelegateCommand SyncCommand { get; private set; }
        public DelegateCommand AddCommand { get; private set; }
        public DelegateCommand NavigateToSettingsPageCommand { get; private set; }
        public DelegateCommand<ItemClickEventArgs> ItemClickCommand { get; private set; }

        public SearchProperties CurrentSearchProperties { get; private set; } = new SearchProperties();
        public ObservableCollection<Item> SearchQuerySuggestions { get; set; } = new ObservableCollection<Item>();
        public ObservableCollection<Language> LanguageSuggestions { get; set; } = new ObservableCollection<Language>();
        public ObservableCollection<Tag> TagSuggestions { get; set; } = new ObservableCollection<Tag>();
        public DelegateCommand<string> SetItemTypeFilterCommand { get; private set; }
        public DelegateCommand<string> SetEstimatedReadingTimeFilterCommand { get; private set; }
        public DelegateCommand<string> SetCreationDateFilterCommand { get; private set; }
        public DelegateCommand<AutoSuggestBoxTextChangedEventArgs> SearchQueryChangedCommand { get; private set; }
        public DelegateCommand<AutoSuggestBoxQuerySubmittedEventArgs> SearchQuerySubmittedCommand { get; private set; }
        public DelegateCommand<SelectionChangedEventArgs> LanguageCodeChangedCommand { get; private set; }
        public DelegateCommand<SelectionChangedEventArgs> TagChangedCommand { get; private set; }
        public DelegateCommand ResetFilterCommand { get; private set; }

        public MainViewModel()
        {
            AddCommand = new DelegateCommand(async () => await DialogService.ShowAsync(DialogService.Dialog.AddItem));
            SyncCommand = new DelegateCommand(async () => await SyncAsync());
            NavigateToSettingsPageCommand = new DelegateCommand(() => NavigationService.Navigate(typeof(Views.SettingsPage), infoOverride: new DrillInNavigationTransitionInfo()));
            ItemClickCommand = new DelegateCommand<ItemClickEventArgs>(t => ItemClick(t));

            SetItemTypeFilterCommand = new DelegateCommand<string>(type => SetItemTypeFilter(type));
            SetEstimatedReadingTimeFilterCommand = new DelegateCommand<string>(order => SetEstimatedReadingTimeFilter(order));
            SetCreationDateFilterCommand = new DelegateCommand<string>(order => SetCreationDateFilter(order));
            SearchQueryChangedCommand = new DelegateCommand<AutoSuggestBoxTextChangedEventArgs>(args => SearchQueryChanged(args));
            SearchQuerySubmittedCommand = new DelegateCommand<AutoSuggestBoxQuerySubmittedEventArgs>(args => SearchQuerySubmitted(args));
            LanguageCodeChangedCommand = new DelegateCommand<SelectionChangedEventArgs>(args => LanguageCodeChanged(args));
            TagChangedCommand = new DelegateCommand<SelectionChangedEventArgs>(args => TagChanged(args));
            ResetFilterCommand = new DelegateCommand(() => CurrentSearchProperties.Reset());

            CurrentSearchProperties.SearchStarted += p => StartSearch();
            CurrentSearchProperties.SearchCanceled += p => EndSearch(null, null);
            CurrentSearchProperties.SearchCanceled += p => UpdateView();
            CurrentSearchProperties.ItemTypeChanged += i => UpdateView();
            CurrentSearchProperties.SortOrderChanged += i => UpdateView();

            Items = new IncrementalObservableCollection<ItemViewModel>(async count => await LoadMoreItemsAsync(count));

            App.OfflineTasksChanged += async (s, e) =>
            {
                OfflineTaskCount = App.Database.Table<OfflineTask>().Count();
                await ExecuteOfflineTasksAsync();
            };
            Items.CollectionChanged += (s, e) => RaisePropertyChanged(nameof(ItemsCountIsZero));
        }

        private Task<List<ItemViewModel>> LoadMoreItemsAsync(uint count)
        {
            var result = new List<ItemViewModel>();

            var database = GetItemsForCurrentSearchProperties(Items.Count, (int)count);

            foreach (var item in database)
                result.Add(new ItemViewModel(item));

            GetMetadataForItems(result);

            return Task.FromResult(result);
        }

        private async Task ExecuteOfflineTasksAsync()
        {
            IsSyncing = true;

            foreach (var task in App.Database.Table<OfflineTask>())
                await task.ExecuteAsync();

            OfflineTaskCount = App.Database.Table<OfflineTask>().Count();

            UpdateView();

            IsSyncing = false;
        }
        private async Task SyncAsync()
        {
            IsSyncing = true;
            await ExecuteOfflineTasksAsync();
            int syncLimit = 30;

            var items = await App.Client.GetItemsAsync(
                DateOrder: Api.WallabagClient.WallabagDateOrder.ByLastModificationDate,
                SortOrder: Api.WallabagClient.WallabagSortOrder.Descending,
                ItemsPerPage: syncLimit);

            if (items != null)
            {
                var itemList = new List<Item>();

                foreach (var item in items)
                    itemList.Add(item);

                var databaseList = App.Database.Table<Item>()
                    .OrderByDescending(i => i.LastModificationDate)
                    .Take(syncLimit).ToList();
                var deletedItems = databaseList.Except(itemList);

                App.Database.RunInTransaction(() =>
                {
                    foreach (var item in deletedItems)
                        App.Database.Delete(item);

                    App.Database.InsertOrReplaceAll(itemList);
                });

                UpdateView();
            }
            IsSyncing = false;
        }

        private void ItemClick(ItemClickEventArgs args)
        {
            var item = args.ClickedItem as ItemViewModel;
            NavigationService.Navigate(typeof(Views.ItemPage), item.Model.Id);
        }

        private void SetItemTypeFilter(string type)
        {
            switch (type)
            {
                case "all":
                    CurrentSearchProperties.ItemType = SearchProperties.SearchPropertiesItemType.All;
                    break;
                case "unread":
                    CurrentSearchProperties.ItemType = SearchProperties.SearchPropertiesItemType.Unread;
                    break;
                case "starred":
                    CurrentSearchProperties.ItemType = SearchProperties.SearchPropertiesItemType.Favorites;
                    break;
                case "archived":
                    CurrentSearchProperties.ItemType = SearchProperties.SearchPropertiesItemType.Archived;
                    break;
            }
            UpdatePageHeader();
        }

        private void UpdatePageHeader()
        {
            if (IsSearchActive)
                PageHeader = $"\"{CurrentSearchProperties.Query.ToUpper()}\"";
            else
            {
                switch (CurrentSearchProperties.ItemType)
                {
                    case SearchProperties.SearchPropertiesItemType.All:
                        PageHeader = Helpers.LocalizedResource("AllPageTitleTextBlock.Text"); break;
                    case SearchProperties.SearchPropertiesItemType.Unread:
                    default:
                        PageHeader = Helpers.LocalizedResource("UnreadPageTitleTextBlock.Text"); break;
                    case SearchProperties.SearchPropertiesItemType.Favorites:
                        PageHeader = Helpers.LocalizedResource("StarredPageTitleTextBlock.Text"); break;
                    case SearchProperties.SearchPropertiesItemType.Archived:
                        PageHeader = Helpers.LocalizedResource("ArchivedPageTitleTextBlock.Text"); break;
                }
            }
        }

        private void SetEstimatedReadingTimeFilter(string order)
        {
            if (order.Equals("asc"))
                CurrentSearchProperties.SortOrder = SearchProperties.SearchPropertiesSortOrder.AscendingByReadingTime;
            else
                CurrentSearchProperties.SortOrder = SearchProperties.SearchPropertiesSortOrder.DescendingByReadingTime;
        }
        private void SetCreationDateFilter(string order)
        {
            if (order.Equals("asc"))
                CurrentSearchProperties.SortOrder = SearchProperties.SearchPropertiesSortOrder.AscendingByCreationDate;
            else
                CurrentSearchProperties.SortOrder = SearchProperties.SearchPropertiesSortOrder.DescendingByCreationDate;
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
        private void SearchQuerySubmitted(AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null)
            {
                NavigationService.Navigate(typeof(Views.ItemPage), (args.ChosenSuggestion as Item).Id);
                return;
            }

            if (string.IsNullOrWhiteSpace(args.QueryText))
                return;

            CurrentSearchProperties.ItemType = SearchProperties.SearchPropertiesItemType.All;
            UpdateView();
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

        private void StartSearch()
        {
            IsSearchActive = true;
            SystemNavigationManager.GetForCurrentView().BackRequested += (s, e) => EndSearch(s, e);
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
        }
        private void EndSearch(object sender, BackRequestedEventArgs e)
        {
            IsSearchActive = false;
            CurrentSearchProperties.ItemType = SearchProperties.SearchPropertiesItemType.Unread;

            SystemNavigationManager.GetForCurrentView().BackRequested -= (s, args) => EndSearch(s, args);
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;

            if (!string.IsNullOrWhiteSpace(CurrentSearchProperties.Query))
                CurrentSearchProperties.Query = string.Empty;

            if (e != null)
                e.Handled = true;

            UpdateView();
            UpdatePageHeader();
        }

        private void UpdateView()
        {
            var currentItems = new List<Item>();
            var databaseItems = GetItemsForCurrentSearchProperties(limit: 24);

            foreach (var item in Items)
                currentItems.Add(item.Model);


            var newItems = databaseItems.Except(currentItems).ToList();
            var changedItems = databaseItems.Except(currentItems, new ItemByModificationDateEqualityComparer()).Except(newItems).ToList();
            var deletedItems = currentItems.Except(databaseItems).ToList();


            foreach (var item in newItems)
                Items.AddSorted(new ItemViewModel(item));

            foreach (var item in changedItems)
            {
                Items.Remove(Items.Where(i => i.Model.Equals(item)).FirstOrDefault());
                Items.AddSorted(new ItemViewModel(item));
            }

            foreach (var item in deletedItems)
                Items.Remove(new ItemViewModel(item));

            GetMetadataForItems(Items);
        }
        private void GetMetadataForItems(IEnumerable<ItemViewModel> items)
        {
            foreach (var item in items)
            {
                if (item.Model.Language != null)
                {
                    var translatedLanguage = new Language(item.Model.Language);

                    if (!LanguageSuggestions.Contains(translatedLanguage))
                        LanguageSuggestions.Add(translatedLanguage);
                }
                else
                {
                    if (!LanguageSuggestions.Contains(Language.Unknown))
                        LanguageSuggestions.Add(Language.Unknown);
                }

                foreach (var tag in item.Model.Tags)
                    if (!TagSuggestions.Contains(tag))
                        TagSuggestions.Add(tag);
            }
        }
        private List<Item> GetItemsForCurrentSearchProperties(int? offset = null, int? limit = null)
        {
            var items = App.Database.Table<Item>();

            switch (CurrentSearchProperties.ItemType)
            {
                case SearchProperties.SearchPropertiesItemType.Unread:
                    items = items.Where(i => i.IsRead == false); break;
                case SearchProperties.SearchPropertiesItemType.Favorites:
                    items = items.Where(i => i.IsStarred == true); break;
                case SearchProperties.SearchPropertiesItemType.Archived:
                    items = items.Where(i => i.IsRead == true); break;
                case SearchProperties.SearchPropertiesItemType.All:
                default:
                    break;
            }

            if (!string.IsNullOrWhiteSpace(CurrentSearchProperties.Query))
                items = items.Where(i => i.Title.ToLower().Contains(CurrentSearchProperties.Query));

            if (CurrentSearchProperties.Language?.IsUnknown == false)
                items = items.Where(i => i.Language.Equals(CurrentSearchProperties.Language.wallabagLanguageCode));
            else if (CurrentSearchProperties.Language?.IsUnknown == true)
                items = items.Where(i => i.Language == null);

            switch (CurrentSearchProperties.SortOrder)
            {
                case SearchProperties.SearchPropertiesSortOrder.AscendingByReadingTime:
                    items = items.OrderBy(i => i.EstimatedReadingTime);
                    break;
                case SearchProperties.SearchPropertiesSortOrder.DescendingByReadingTime:
                    items = items.OrderByDescending(i => i.EstimatedReadingTime);
                    break;
                case SearchProperties.SearchPropertiesSortOrder.AscendingByCreationDate:
                    items = items.OrderBy(i => i.CreationDate);
                    break;
                case SearchProperties.SearchPropertiesSortOrder.DescendingByCreationDate:
                default:
                    items = items.OrderByDescending(i => i.CreationDate);
                    break;
            }

            Items.MaxItems = items.Count();

            if (offset != null)
                items = items.Skip((int)offset);

            if (limit != null)
                items = items.Take((int)limit);

            var list = items.ToList();

            if (CurrentSearchProperties.Tag != null)
                list = list.Where(i => i.Tags.Contains(CurrentSearchProperties.Tag)).ToList();

            return list;
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            UpdateView();

            if (SettingsService.Instance.SyncOnStartup)
                await SyncAsync();

            Messenger.Default.Register<NotificationMessage>(this, message =>
            {
                if (message.Notification.Equals("FetchFromDatabase"))
                    UpdateView();
            });

            if (state.ContainsKey(nameof(CurrentSearchProperties)))
            {
                var stateValue = state[nameof(CurrentSearchProperties)] as string;
                CurrentSearchProperties.Replace(await Task.Run(() => JsonConvert.DeserializeObject<SearchProperties>(stateValue)));
            }
        }
        public override async Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            var serializedSearchProperties = await Task.Run(() => JsonConvert.SerializeObject(CurrentSearchProperties));
            pageState[nameof(CurrentSearchProperties)] = serializedSearchProperties;
        }
    }
}