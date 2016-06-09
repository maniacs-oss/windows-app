﻿using SQLite.Net.Attributes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using wallabag.Api.Models;
using wallabag.Common;

namespace wallabag.Models
{
    public class OfflineTask
    {
        [AutoIncrement, PrimaryKey]
        public int Id { get; set; }

        public int ItemId { get; set; }
        public OfflineTaskAction Action { get; set; }
        public List<Tag> addTagsList { get; set; }
        public List<Tag> removeTagsList { get; set; }

        public async Task ExecuteAsync()
        {
            bool executionIsSuccessful = false;
            switch (Action)
            {
                case OfflineTaskAction.MarkAsRead:
                    executionIsSuccessful = await App.Client.ArchiveAsync(ItemId);
                    break;
                case OfflineTaskAction.UnmarkAsRead:
                    executionIsSuccessful = await App.Client.UnarchiveAsync(ItemId);
                    break;
                case OfflineTaskAction.MarkAsStarred:
                    executionIsSuccessful = await App.Client.FavoriteAsync(ItemId);
                    break;
                case OfflineTaskAction.UnmarkAsStarred:
                    executionIsSuccessful = await App.Client.UnfavoriteAsync(ItemId);
                    break;
                case OfflineTaskAction.EditTags:
                    var newTags = await App.Client.AddTagsAsync(ItemId, string.Join(",", addTagsList).Split(","[0]));

                    var item = App.Database.Get<Item>(i => i.Id == ItemId);
                    var tags = item.Tags as List<Tag>;

                    if (newTags != null)
                        tags.Replace(newTags.Convert<WallabagTag, Tag>().ToList());

                    executionIsSuccessful = await App.Client.RemoveTagsAsync(ItemId, removeTagsList.Convert<Tag, WallabagTag>());
                    break;
                case OfflineTaskAction.Delete:
                    executionIsSuccessful = await App.Client.DeleteAsync(ItemId);
                    break;
                default:
                    break;
            }

            if (executionIsSuccessful)
                App.Database.Delete(this);
        }

        public OfflineTask() { }
        public OfflineTask(int itemId, OfflineTaskAction action, List<Tag> addTagsList = null, List<Tag> removeTagsList = null)
        {
            ItemId = itemId;
            Action = action;
            this.addTagsList = addTagsList;
            this.removeTagsList = removeTagsList;
        }

        public enum OfflineTaskAction
        {
            MarkAsRead = 0,
            UnmarkAsRead = 1,
            MarkAsStarred = 2,
            UnmarkAsStarred = 3,
            EditTags = 4,
            Delete = 10
        }
    }
}
