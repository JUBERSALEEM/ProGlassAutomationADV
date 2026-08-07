using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Models
{
    public class UpdateInfo
    {
        public string Application { get; set; } = "ProGlassAutomation";
        public string LatestVersion { get; set; } = "1.0.0";
        public string DownloadUrl { get; set; } = "";
        public bool Mandatory { get; set; } = false;
        public string ReleaseDate { get; set; } = "";
        public List<string> ReleaseNotes { get; set; } = new List<string>();
    }
}
