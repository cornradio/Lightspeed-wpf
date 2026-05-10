using System;

namespace Lightspeed_wpf
{
    public class lightspeed_obj
    {
        public string Title { get; set; }
        public string HotkeyStr { get; set; }
        public string Path { get; set; }

        public lightspeed_obj(string title, string path, string hotkeystr)
        {
            Title = title;
            HotkeyStr = hotkeystr;
            Path = path;
            if (Path.Contains(","))
            {
                Path = Path.Replace(",", "`,");
            }
        }

        public string getAhkString()
        {
            string content = $@"
{HotkeyStr}::
open_or_activate(""{Title}"",""{Path}"")
return
";
            return content;
        }
    }
}