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
        }

        public string getAhkString()
        {
            string ahkPath = Path.Contains(",") ? Path.Replace(",", "`,") : Path;
            string content = $@"
{HotkeyStr}::
open_or_activate(""{Title}"",""{ahkPath}"")
return
";
            return content;
        }
    }
}