﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using wallabag.Api.Models;
using wallabag.Api.Responses;

namespace wallabag.Api
{
    public partial class WallabagClient
    {
        public async Task<IEnumerable<WallabagItem>> GetItemsAsync(
            bool? IsRead = null,
            bool? IsStarred = null,
            WallabagDateOrder? DateOrder = null,
            WallabagSortOrder? SortOrder = null,
            int? PageNumber = null,
            int? ItemsPerPage = null,
            string[] Tags = null)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            var requestUriSubString = "/entries";

            if (IsRead != null)
                parameters.Add("archive", IsRead);
            if (IsStarred != null)
                parameters.Add("starred", IsStarred);
            if (DateOrder != null)
                parameters.Add("sort", (DateOrder == WallabagDateOrder.ByCreationDate ? "created" : "updated"));
            if (SortOrder != null)
                parameters.Add("order", (SortOrder == WallabagSortOrder.Ascending ? "asc" : "desc"));
            if (PageNumber != null)
                parameters.Add("page", PageNumber);
            if (ItemsPerPage != null)
                parameters.Add("perPage", ItemsPerPage);
            if (Tags != null)
                parameters.Add("tags", System.Net.WebUtility.HtmlEncode(Tags.ToCommaSeparatedString()));

            if (parameters.Count > 0)
            {
                requestUriSubString += "?";

                foreach (var item in parameters)
                    requestUriSubString += $"{item.Key}={item.Value.ToString()}&";

                // Remove the last ampersand (&).
                requestUriSubString.Remove(requestUriSubString.Length - 1);
            }

            var jsonString = await ExecuteHttpRequestAsync(HttpRequestMethod.Get, requestUriSubString);
            return (await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<ItemCollectionResponse>(jsonString))).Embedded.Items;
        }
        public Task<WallabagItem> GetItemAsync(int itemId)
        {
            throw new NotImplementedException();
        }

        public enum WallabagDateOrder { ByCreationDate, ByLastModificationDate }
        public enum WallabagSortOrder { Ascending, Descending }
    }
}
