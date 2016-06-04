﻿using GalaSoft.MvvmLight.Messaging;
using PropertyChanged;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Template10.Mvvm;
using wallabag.Common;
using wallabag.Models;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace wallabag.ViewModels
{
    [ImplementPropertyChanged]
    public class MainViewModel : ViewModelBase
    {
        private List<Item> _items = new List<Item>();
        public ObservableCollection<ItemViewModel> Items { get; set; } = new ObservableCollection<ItemViewModel>();

        public DelegateCommand SyncCommand { get; private set; }
        public DelegateCommand AddCommand { get; private set; }
        public DelegateCommand NavigateToSettingsPageCommand { get; private set; }
        public DelegateCommand<ItemClickEventArgs> ItemClickCommand { get; private set; }

        public SearchProperties CurrentSearchProperties { get; private set; } = new SearchProperties();
        public ObservableCollection<Item> SearchQuerySuggestions { get; set; } = new ObservableCollection<Item>();
        public DelegateCommand<string> SetItemTypeFilterCommand { get; private set; }
        public DelegateCommand<string> SetEstimatedReadingTimeFilterCommand { get; private set; }
        public DelegateCommand<string> SetCreationDateFilterCommand { get; private set; }
        public DelegateCommand<AutoSuggestBoxTextChangedEventArgs> SearchQueryChangedCommand { get; private set; }
        public DelegateCommand<AutoSuggestBoxQuerySubmittedEventArgs> SearchQuerySubmittedCommand { get; private set; }

        public MainViewModel()
        {
            AddCommand = new DelegateCommand(async () => await Services.DialogService.ShowAsync(Services.DialogService.Dialog.AddItem));
            SyncCommand = new DelegateCommand(async () => await SyncAsync());
            NavigateToSettingsPageCommand = new DelegateCommand(() => NavigationService.Navigate(typeof(Views.SettingsPage), infoOverride: new DrillInNavigationTransitionInfo()));
            ItemClickCommand = new DelegateCommand<ItemClickEventArgs>(t => ItemClick(t));

            SetItemTypeFilterCommand = new DelegateCommand<string>(type => SetItemTypeFilter(type));
            SetEstimatedReadingTimeFilterCommand = new DelegateCommand<string>(order => SetEstimatedReadingTimeFilter(order));
            SetCreationDateFilterCommand = new DelegateCommand<string>(order => SetCreationDateFilter(order));
            SearchQueryChangedCommand = new DelegateCommand<AutoSuggestBoxTextChangedEventArgs>(args => SearchQueryChanged(args));
            SearchQuerySubmittedCommand = new DelegateCommand<AutoSuggestBoxQuerySubmittedEventArgs>(args => SearchQuerySubmitted(args));
            CurrentSearchProperties.SearchCanceled += p => FetchFromDatabase();
        }

        private async Task SyncAsync()
        {
            var items = await App.Client.GetItemsAsync();

            if (items != null)
            {
                foreach (var item in items)
                    if (!_items.Contains(item))
                        _items.Add(item);

                await Task.Factory.StartNew(() => App.Database.InsertOrReplaceAll(_items));
                FetchFromDatabase();
            }
        }
        private void FetchFromDatabase()
        {
            var databaseItems = App.Database.Table<Item>().Where(i => i.IsRead == false).ToList();
            UpdateItemCollection(databaseItems);
        }
        private void ItemClick(ItemClickEventArgs args)
        {
            var item = args.ClickedItem as ItemViewModel;
            NavigationService.Navigate(typeof(Views.ItemPage), item.Model);
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
            UpdateViewBySearchProperties();
        }
        private void SetEstimatedReadingTimeFilter(string order)
        {
            if (order.Equals("asc"))
                CurrentSearchProperties.ReadingTimeSortOrder = SearchProperties.SortOrder.Ascending;
            else
                CurrentSearchProperties.ReadingTimeSortOrder = SearchProperties.SortOrder.Descending;
            UpdateViewBySearchProperties();
        }
        private void SetCreationDateFilter(string order)
        {
            if (order.Equals("asc"))
                CurrentSearchProperties.CreationDateSortOrder = SearchProperties.SortOrder.Ascending;
            else
                CurrentSearchProperties.CreationDateSortOrder = SearchProperties.SortOrder.Descending;
            UpdateViewBySearchProperties();
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
                NavigationService.Navigate(typeof(Views.ItemPage), args.ChosenSuggestion as Item);
                return;
            }

            if (string.IsNullOrWhiteSpace(args.QueryText))
                return;

            var searchItems = App.Database.Table<Item>().Where(i => i.Title.ToLower().Contains(args.QueryText));

            if (CurrentSearchProperties.ReadingTimeSortOrder != null)
            {
                if (CurrentSearchProperties.ReadingTimeSortOrder == SearchProperties.SortOrder.Ascending)
                    searchItems = searchItems.OrderBy(i => i.EstimatedReadingTime);
                else
                    searchItems = searchItems.OrderByDescending(i => i.EstimatedReadingTime);
            }

            if (CurrentSearchProperties.CreationDateSortOrder != null)
            {
                if (CurrentSearchProperties.CreationDateSortOrder == SearchProperties.SortOrder.Ascending)
                    searchItems = searchItems.OrderBy(i => i.CreationDate);
                else
                    searchItems = searchItems.OrderByDescending(i => i.CreationDate);
            }

            UpdateItemCollection(searchItems.ToList());
        }

        private void UpdateItemCollection(List<Item> newItemList)
        {
            var idComparer = new ItemByIdEqualityComparer();
            var modificationDateComparer = new ItemByModificationDateEqualityComparer();

            var newItems = newItemList.Except(_items, idComparer);
            var changedItems = newItemList.Except(_items, modificationDateComparer).Except(newItems);
            var deletedItems = _items.Except(newItemList, idComparer);

            _items = newItemList;

            foreach (var item in newItems)
                Items.AddSorted(new ItemViewModel(item));

            foreach (var item in changedItems)
            {
                Items.Remove(Items.Where(i => i.Model.Id == item.Id).FirstOrDefault());
                Items.AddSorted(new ItemViewModel(item));
            }

            foreach (var item in deletedItems)
                Items.Remove(new ItemViewModel(item));
        }
        private void UpdateViewBySearchProperties()
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
            
            switch (CurrentSearchProperties.CreationDateSortOrder)
            {
                case SearchProperties.SortOrder.Ascending:
                    items = items.OrderBy(i => i.CreationDate); break;
                case SearchProperties.SortOrder.Descending:
                default:
                    items = items.OrderByDescending(i => i.CreationDate);
                    break;
            }

            switch (CurrentSearchProperties.ReadingTimeSortOrder)
            {
                case SearchProperties.SortOrder.Ascending:
                    items = items.OrderBy(i => i.EstimatedReadingTime); break;
                case SearchProperties.SortOrder.Descending:
                    items = items.OrderByDescending(i => i.EstimatedReadingTime); break;
                default:
                    break;
            }
            var list = items.ToList();
            UpdateItemCollection(list);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (mode != NavigationMode.Refresh)
                FetchFromDatabase();

            Messenger.Default.Register<NotificationMessage>(this, message =>
            {
                if (message.Notification.Equals("FetchFromDatabase"))
                    FetchFromDatabase();
            });
            return base.OnNavigatedToAsync(parameter, mode, state);
        }
    }
}