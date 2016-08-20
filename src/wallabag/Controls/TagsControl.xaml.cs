﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using wallabag.Common;
using wallabag.Models;
using wallabag.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace wallabag.Controls
{
    public sealed partial class TagsControl : UserControl
    {
        #region Dependency Properties

        public IEnumerable<Tag> ItemsSource
        {
            get { return (IEnumerable<Tag>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemsSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(IEnumerable<Tag>), typeof(TagsControl), new PropertyMetadata(new ObservableCollection<Tag>()));

        #endregion

        public ObservableCollection<Tag> Suggestions { get; set; } = new ObservableCollection<Tag>();

        public TagsControl()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => UpdateNoTagsInfoTextBlockVisibility();

            if (SettingsService.Instance.EnableAutomaticAddingOfTags)
                autoSuggestBox.KeyDown += AutoSuggestBox_KeyDown;
            else
                autoSuggestBox.SuggestionChosen += AutoSuggestBox_SuggestionChosen;
        }

        private bool _loadOldQuery;
        private string _oldQuery;
        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem != null)
            {
                _loadOldQuery = true;
                _oldQuery = sender.Text;
            }
        }

        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var tags = args.QueryText.Split(","[0]).ToList();
            var itemsSource = ItemsSource as ICollection<Tag>;

            foreach (var item in tags)
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    var newTag = new Tag() { Label = item, Id = itemsSource.Count + 1 };
                    if (itemsSource.Contains(newTag) == false)
                        itemsSource.Add(newTag);
                }
            }

            UpdateNoTagsInfoTextBlockVisibility();

            if (_loadOldQuery)
            {
                _loadOldQuery = false;

                var queryWithoutLastElement = _oldQuery.Split(","[0]).ToList();
                queryWithoutLastElement.Remove(queryWithoutLastElement.Last());

                sender.Text = string.Join(",", queryWithoutLastElement);
            }
            else
                sender.Text = string.Empty;
        }
        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)

                if (SettingsService.Instance.EnableAutomaticAddingOfTags)
                    Suggestions.Replace(App.Database.Table<Tag>().Where(t => t.Label.ToLower().StartsWith(sender.Text.ToLower())).Take(3).ToList());
                else
                {
                    var suggestionString = sender.Text.ToLower().Split(","[0]).Last();
                    Suggestions.Replace(App.Database.Table<Tag>().Where(t => t.Label.ToLower().StartsWith(suggestionString)).Take(3).ToList());
                }
        }

        private void UpdateNoTagsInfoTextBlockVisibility()
        {
            if (ItemsSource.Count() == 0)
                noTagsInfoTextBlock.Visibility = Visibility.Visible;
            else
                noTagsInfoTextBlock.Visibility = Visibility.Collapsed;
        }

        private void tagsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            (ItemsSource as IList<Tag>).Remove(e.ClickedItem as Tag);
            UpdateNoTagsInfoTextBlockVisibility();
        }

        private void AutoSuggestBox_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // Checks if the comma key was pressed (code 188)
            if ((int)e.Key == 188)
            {
                e.Handled = true;
                var textBox = e.OriginalSource as TextBox;

                (ItemsSource as ObservableCollection<Tag>).Add(new Tag() { Label = textBox.Text.Replace(",", string.Empty) });
                textBox.Text = string.Empty;
            }
        }
    }
}
