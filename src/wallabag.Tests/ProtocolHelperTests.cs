﻿using System;
using wallabag.Data.Common.Helpers;
using Xunit;

namespace wallabag.Tests
{
    public class ProtocolHelperTests
    {
        [Fact]
        public void ValidProtocolUriReturnsCorrectValues()
        {
            string protocol = "wallabag://username@https://localhost/";
            var result = ProtocolHelper.Parse(protocol);

            Assert.NotNull(result);
            Assert.Equal(result.Server, "https://localhost/");
            Assert.Equal(result.Username, "username");
        }

        [Fact]
        public void InvalidProtocolHandlerReturnsNull()
        {
            string protocol = "mytest://username@https://localhost/";
            var result = ProtocolHelper.Parse(protocol);
            Assert.Null(result);
        }

        [Fact]
        public void EmptyUsernameReturnsInvalidResult()
        {
            string protocol = "wallabag://@https://localhost/";
            var result = ProtocolHelper.Parse(protocol);

            Assert.Null(result);
        }

        [Fact]
        public void EmptyServerReturnsInvalidResult()
        {
            string protocol = "wallabag://username@";
            var result = ProtocolHelper.Parse(protocol);

            Assert.Null(result);
        }

        [Fact]
        public void EmptyUsernameAndServerReturnsInvalidResult()
        {
            string protocol = "wallabag://@";
            var result = ProtocolHelper.Parse(protocol);

            Assert.Null(result);
        }
    }
}