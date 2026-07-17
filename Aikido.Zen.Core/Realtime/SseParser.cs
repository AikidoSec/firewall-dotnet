using System.Collections.Generic;

namespace Aikido.Zen.Core.Realtime
{
    internal sealed class SseParser
    {
        private string _eventName;
        private readonly List<string> _dataLines = new List<string>();
        private bool _isFirstLine = true;

        internal bool TryProcessLine(string line, out string eventName, out string data)
        {
            eventName = null;
            data = null;

            if (_isFirstLine)
            {
                line = line?.TrimStart('\uFEFF');
                _isFirstLine = false;
            }

            if (string.IsNullOrEmpty(line))
            {
                if (_dataLines.Count == 0)
                {
                    ResetEvent();
                    return false;
                }

                eventName = _eventName;
                data = string.Join("\n", _dataLines);
                ResetEvent();
                return true;
            }

            if (line[0] == ':')
            {
                return false;
            }

            var separatorIndex = line.IndexOf(':');
            var field = separatorIndex >= 0 ? line.Substring(0, separatorIndex) : line;
            var value = separatorIndex >= 0 ? line.Substring(separatorIndex + 1) : string.Empty;
            if (value.StartsWith(" "))
            {
                value = value.Substring(1);
            }

            switch (field)
            {
                case "event":
                    _eventName = value;
                    break;
                case "data":
                    _dataLines.Add(value);
                    break;
            }

            return false;
        }

        private void ResetEvent()
        {
            _eventName = null;
            _dataLines.Clear();
        }
    }
}
