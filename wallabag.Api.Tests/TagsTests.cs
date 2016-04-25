﻿using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using wallabag.Api.Models;

namespace wallabag.Api.Tests
{
    public partial class GeneralTests
    {
        [TestMethod]
        [TestCategory("Tags")]
        public async Task TagsAreRetrieved()
        {
            List<WallabagTag> tags = (await client.GetTagsAsync()).ToList();
            Assert.IsTrue(tags.Count > 0);
        }

        [TestMethod]
        [TestCategory("Tags")]
        public async Task TagsAreAddedToItem()
        {
            var item = (await client.GetItemsAsync()).ToList().First();

            var tags = (await client.AddTagsAsync(item, new string[] { "wallabag", "test" })).ToList();
            CollectionAssert.AllItemsAreInstancesOfType(tags, typeof(WallabagTag));
            Assert.IsTrue(tags.Count == 2);

            var modifiedItem = await client.GetItemAsync(item.Id);
            CollectionAssert.IsSubsetOf(tags, modifiedItem.Tags.ToList());
        }

        [TestMethod]
        [TestCategory("Tags")]
        public async Task TagsAreRemovedFromItem()
        {
            var item = (await client.GetItemsAsync()).ToList().First();
            Assert.IsTrue(await client.RemoveTagsAsync(item.Id, item.Tags.ToArray()));

            var modifiedItem = await client.GetItemAsync(item.Id);
            Assert.IsTrue(modifiedItem.Tags.Count() == 0);
        }

        [TestMethod]
        [TestCategory("Tags")]
        public async Task TagsIsRemovedFromAllItems()
        {
            var tag = (await client.GetTagsAsync()).First();
            Assert.IsTrue(await client.RemoveTagFromAllItemsAsync(tag));

            var items = (await client.GetItemsAsync(Tags: new string[] { tag.Label })).ToList();
            Assert.IsTrue(items.Count == 0);
        }
    }
}
