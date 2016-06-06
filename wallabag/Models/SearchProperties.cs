﻿using System.ComponentModel;

namespace wallabag.Models
{
    public class SearchProperties : INotifyPropertyChanged
    {
        public event SearchChangedHandler SearchCanceled;
        public event SearchChangedHandler SearchStarted;
        public event PropertyChangedEventHandler PropertyChanged;

        public delegate void SearchChangedHandler(SearchProperties p);

        private string _query;
        public string Query
        {
            get { return _query; }
            set
            {
                var oldValue = _query;
                if (_query != value)
                {
                    _query = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Query)));
                }

                if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(oldValue))
                    SearchCanceled?.Invoke(this);

                if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(oldValue))
                    SearchStarted?.Invoke(this);
            }
        }

        public SearchPropertiesItemType? ItemType { get; set; }
        public SortOrder? ReadingTimeSortOrder { get; set; }
        public SortOrder? CreationDateSortOrder { get; set; }
        public Language Language { get; set; }

        public enum SearchPropertiesItemType
        {
            All = 0,
            Unread = 1,
            Favorites = 2,
            Archived = 3
        }
        public enum SortOrder
        {
            Ascending = 0,
            Descending = 1
        }

        public SearchProperties()
        {
            Query = string.Empty;
            ItemType = SearchPropertiesItemType.Unread;
            ReadingTimeSortOrder = null;
            CreationDateSortOrder = null;
        }
    }
}
