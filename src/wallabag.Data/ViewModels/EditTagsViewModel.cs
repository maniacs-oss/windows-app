﻿using GalaSoft.MvvmLight.Command;
using PropertyChanged;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using wallabag.Data.Common.Helpers;
using wallabag.Data.Models;
using wallabag.Data.Services;

namespace wallabag.Data.ViewModels
{
    [ImplementPropertyChanged]
    public class EditTagsViewModel : ViewModelBase
    {
        private IEnumerable<Tag> _previousTags;
        public IList<Item> Items { get; set; } = new List<Item>();
        public ObservableCollection<Tag> Tags { get; set; } = new ObservableCollection<Tag>();

        public ICommand FinishCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public EditTagsViewModel()
        {
            FinishCommand = new RelayCommand(() => Finish());
            CancelCommand = new RelayCommand(() => Services.DialogService.HideCurrentDialog());
        }
        public EditTagsViewModel(Item Item)
        {
            FinishCommand = new RelayCommand(() => Finish());
            CancelCommand = new RelayCommand(() => Services.DialogService.HideCurrentDialog());

            Items.Add(Item);
            _previousTags = Item.Tags;
            Tags = new ObservableCollection<Tag>(Item.Tags);
        }

        private void Finish()
        {
            if (_previousTags == null)
            {
                foreach (var item in Items)
                {
                    OfflineTaskService.Add(item.Id, OfflineTask.OfflineTaskAction.EditTags, Tags.ToList());

                    foreach (var tag in Tags)
                        item.Tags.Add(tag);
                }
            }
            else
            {
                var newTags = Tags.Except(_previousTags).ToList();
                var deletedTags = _previousTags.Except(Tags).ToList();

                Items.First().Tags.Replace(Tags);

                OfflineTaskService.Add(Items.First().Id, OfflineTask.OfflineTaskAction.EditTags, newTags, deletedTags);
            }
        }
    }
}
