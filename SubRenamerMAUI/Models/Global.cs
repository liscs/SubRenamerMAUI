namespace SubRenamerMAUI.Models
{
    public class Global
    {
        public static readonly string LOG_FILENAME = "subrenamer.log";
        public static HashSet<string> VideoExts = new HashSet<string> { ".mkv", ".mp4", ".flv", ".avi", ".mov", ".rmvb", ".wmv", ".mpg", ".avs" };
        public static HashSet<string> SubExts = new HashSet<string> { ".srt", ".ass", ".ssa", ".sub", ".idx" };
        public enum AppFileType
        {
            Video,
            Sub
        }
    }
}
