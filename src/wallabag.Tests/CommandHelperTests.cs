﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Input;
using System;

namespace wallabag.Tests
{
    [TestClass]
    public class CommandHelperTests
    {
        [TestMethod]
        public void CommandExecutionWithoutAParameterUsesNull()
        {
            var myCommand = new TestCommand(parameter => {
                Assert.IsNull(parameter);
            });
            Data.Common.Helpers.CommandHelper.Execute(myCommand);
        }

        public class TestCommand : ICommand
        {
            private Action<object> _action;

            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter) => true;

            public void Execute(object parameter) => _action.Invoke(parameter);

            public TestCommand(Action<object> action)
            {
                _action = action;
            }
        }
    }
}
