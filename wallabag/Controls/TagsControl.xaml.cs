﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using wallabag.Models;
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

        public TagsControl()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => UpdateNoTagsInfoTextBlockVisibility();
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
            sender.Text = string.Empty;
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
    }
}
