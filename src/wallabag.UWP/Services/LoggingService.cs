﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using wallabag.Data.Services;

namespace wallabag.Services
{
    class LoggingService : ILoggingService
    {
        private const string m_SCHEMA = "[{0}] [{1}] {2}";
        private List<string> _lines = new List<string>();

        public string Log => string.Join("\r\n", _lines);

        public void TrackException(Exception e, LoggingCategory category = LoggingCategory.Critical, [CallerMemberName] string member = "", [CallerLineNumber] int lineNumber = 0)
            => Write(e.Message, category, member, lineNumber);

        public void WriteLine(string text, LoggingCategory category = LoggingCategory.Debug, [CallerMemberName] string member = "", [CallerLineNumber] int lineNumber = 0)
            => Write(text, category, member, lineNumber);

        public void WriteLineIf(bool condition, string text, LoggingCategory category = LoggingCategory.Debug, [CallerMemberName] string member = "", [CallerLineNumber] int lineNumber = 0)
        {
            if (condition)
                Write(text, category, member, lineNumber);
        }

        public void WriteObject(object obj, LoggingCategory category = LoggingCategory.Debug, [CallerMemberName] string member = "", [CallerLineNumber] int lineNumber = 0)
        {
            string json = JsonConvert.SerializeObject(obj, Formatting.Indented);
            Write(json, category, member, lineNumber);
        }

        private void Write(string text, LoggingCategory category, string member, int lineNumber)
        {
            string line = string.Format(m_SCHEMA, DateTime.Now, category.ToString(), text);

            if (System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debug.WriteLine(line, category.ToString());

            _lines.Add(line);
        }
    }
}
