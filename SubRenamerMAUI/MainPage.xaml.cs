using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Maui.Views;
using SubRenamerMAUI.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using static SubRenamerMAUI.Models.Global;

namespace SubRenamerMAUI;
public partial class MainPage : ContentPage
{
    public ObservableCollection<VsItem> VsList = new ObservableCollection<VsItem>();

    public MainPage()
    {
        InitializeComponent();
    }

    private async Task Open_FolderAsync()
    {
        CancellationTokenSource source = new();
        CancellationToken token = source.Token;
        var result = await FolderPicker.Default.PickAsync(token);

        if (result.IsSuccessful)
        {
            MainLV.ItemsSource = null;
            var folder = new DirectoryInfo(result.Folder.Path);
            var files = folder.GetFiles("*");

            // 添加所有 视频/字幕 文件
            foreach (var file in files) FileListAdd(file);
            MatchVideoSub();
            MainLV.ItemsSource = VsList;
        }
    }
    public void MatchVideoSub()
    {
        if (VsList.Count <= 0) return;

        // VsItem
        TryHandleVsListMatch(AppFileType.Video);
        TryHandleVsListMatch(AppFileType.Sub);
    }
    private void FileListAdd(FileInfo file)
    {
        AppFileType fileType;
        if (VideoExts.Contains(file.Extension.ToString().ToLower()))
            fileType = AppFileType.Video;
        else if (SubExts.Contains(file.Extension.ToString().ToLower()))
            fileType = AppFileType.Sub;
        else return;

        var vsItem = new VsItem();
        if (fileType == AppFileType.Video)
        {
            if (VsList.Where(o => o.Video == file.FullName).Count() != 0) return; // 重名排除
            vsItem.Video = file.FullName;
        }
        else if (fileType == AppFileType.Sub)
        {
            if (VsList.Where(o => o.Sub == file.FullName).Count() != 0) return;
            vsItem.Sub = file.FullName;
        }

        vsItem.Status = VsStatus.Unmatched;
        VsList.Add(vsItem);
    }

    private void Open_Folder_Clicked(object sender, EventArgs e)
    {
        _ = Open_FolderAsync();
    }

    private void TryHandleVsListMatch(AppFileType FileType)
    {
        // 自动匹配数据归零
        M_Auto_Begin = int.MinValue;
        M_Auto_End = null;

        var FileList = GetFileListByVsList(FileType);
        foreach (var file in FileList)
        {
            string matchKey = GetMatchKeyByFileName(file.Name, FileType, FileList);

            VsItem findVsItem = null;
            if (matchKey != null)
            {
                foreach (var o in VsList)
                {
                    if (o.MatchKey == matchKey)
                    {
                        findVsItem = o;
                        break;
                    }
                }

            }

            if (findVsItem == null)
            // 通过文件名查找到的现成的 VsItem
            {
                findVsItem = VsList.Where(o =>
                {
                    if (FileType == AppFileType.Video) return o.Video == file.FullName;
                    if (FileType == AppFileType.Sub) return o.Sub == file.FullName;
                    return false;
                }).ElementAt(0);
            }

            // 仅更新数据
            if (findVsItem != null)
            {
                if (FileType == AppFileType.Video)
                {
                    findVsItem.Video = file.FullName;
                    findVsItem.Status = VsStatus.SubLack;
                    for (int i = VsList.Count - 1; i >= 0; i--)
                    {
                        if (VsList[i] != findVsItem && VsList[i].Video == findVsItem.Video)
                        {
                            VsList.RemoveAt(i);
                        }
                    }
                }
                else if (FileType == AppFileType.Sub)
                {
                    findVsItem.Sub = file.FullName;
                    findVsItem.Status = VsStatus.VideoLack;
                    for (int i = VsList.Count - 1; i >= 0; i--)
                    {
                        if (VsList[i] != findVsItem && VsList[i].Sub == findVsItem.Sub)
                        {
                            VsList.RemoveAt(i);
                        }
                    }
                }

                if (findVsItem.Video != null && findVsItem.Sub != null)
                    findVsItem.Status = VsStatus.Ready;

                findVsItem.MatchKey = matchKey;
                if (string.IsNullOrWhiteSpace(findVsItem.MatchKey))
                    findVsItem.Status = VsStatus.Unmatched;
            }
        }
    }
    private List<FileInfo> GetFileListByVsList(AppFileType FileType)
    {
        if (FileType == AppFileType.Video)
            return new List<FileInfo>(VsList.Where(o => o.Video != null).Select(o => o.VideoFileInfo));
        if (FileType == AppFileType.Sub)
            return new List<FileInfo>(VsList.Where(o => o.Sub != null).Select(o => o.SubFileInfo));
        return null;
    }

    // 自动匹配配置
    private int M_Auto_Begin = int.MinValue;
    private string M_Auto_End = null;
    // 获取匹配字符
    private string GetMatchKeyByFileName(string fileName, AppFileType fileType, List<FileInfo> FileList)
    {
        string matchKey = null;
        if (M_Auto_Begin == int.MinValue) M_Auto_Begin = GetEpisodePosByList(FileList); // 视频文件名集数开始位置
        if (M_Auto_End == null) M_Auto_End = GetEndStrByList(FileList, M_Auto_Begin);
        if (M_Auto_Begin > -1 && M_Auto_End != null)
            matchKey = GetEpisByFileName(fileName, M_Auto_Begin, M_Auto_End); // 匹配字符

        if (string.IsNullOrWhiteSpace(matchKey)) matchKey = null;
        return matchKey;
    }
    // 遍历所有 list 中的项目，尝试得到集数开始位置
    private int GetEpisodePosByList(List<FileInfo> list)
    {
        int aIndex = 0;
        int bIndex = 1;
        int beginPos = -1;

        while (true)
        {
            try
            {
                int result = GetEpisodePosByTwoStr(list[aIndex].Name, list[bIndex].Name);
                beginPos = result;
                break;
            }
            catch
            {
                aIndex++;
                bIndex++;
                if (aIndex >= list.Count || bIndex >= list.Count) break;
            }
        }

        return beginPos;
    }
    // 通过比对两个文件名中 数字 不同的部分来得到 集数 的位置
    private int GetEpisodePosByTwoStr(string strA, string strB)
    {
        var numGrpA = Regex.Matches(strA.ToString(), @"(\d+)");
        var numGrpB = Regex.Matches(strB.ToString(), @"(\d+)");
        int beginPos = -1;

        for (int i = 0; i < numGrpA.Count; i++)
        {
            var A = numGrpA[i];
            var B = numGrpB[i];
            if (A.Value != B.Value && A.Index == B.Index)
            {
                // 若两个 val 不同，则记录位置
                beginPos = numGrpA[i].Index;
                break;
            }
        }

        if (beginPos == -1) throw new Exception("beginPos == -1");

        return beginPos;
    }
    // 获取终止字符
    private string GetEndStrByList(List<FileInfo> list, int beginPos)
    {
        if (list.Count() < 2) return null;
        if (beginPos <= -1) return null;

        string fileName = list.Where(o =>
        {
            if (o.Name == null || o.Name.Length <= beginPos) return false;
            return Regex.IsMatch(o.Name.Substring(beginPos)[0].ToString(), @"^\d+$");
        }).ToList()[0].Name; // 获取开始即是数字的文件名
        fileName = fileName.Substring(beginPos); // 从指定开始位置 (beginPos) 开始读取数字（忽略开始位置前的所有内容）
        var grp = Regex.Matches(fileName, @"(\d+)");
        if (grp.Count <= 0) return null;
        Match firstNum = grp[0];
        int afterNumStrIndex = firstNum.Index + firstNum.Length; // 数字后面的第一个字符 index

        // 不把特定字符（空格等）作为结束字符
        string strTmp = fileName.Substring(afterNumStrIndex);
        string result = null;
        for (int i = 0; i < strTmp.Length; i++)
        {
            if (strTmp[i].ToString() != " ")
            {
                result = strTmp[i].ToString();
                break;
            }
        }
        return result;
    }
    // 获取集数
    private string GetEpisByFileName(string fileName, int beginPos, string endStr)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        if (beginPos <= -1) return null;
        if (beginPos >= fileName.Length) return null;
        var str = fileName.Substring(beginPos);

        var result = "";
        // 通过 endStr 获得集数
        for (int i = 0; i < str.Length; i++)
        {
            if (str[i].ToString() == endStr) break;
            result += str[i];
        }

        result = result.TrimStart('0'); // 开头为零的情况：替换 0001 为 1
        result = result.Trim(); // 去掉前后空格

        string ans = "";
        bool sflag = false;
        foreach (var i in result)
        {

            if (i >= '0' && i <= '9')
            {
                sflag = true;
                ans += i.ToString();
            }
            else if (sflag)
            {
                break;
            }
        }
        return ans;
    }

    private void Clear_List_Clicked(object sender, EventArgs e)
    {
        VsList.Clear();

    }

    private void Start_Clicked(object sender, EventArgs e)
    {
        var subRenameDict = GetSubRenameDict();
        if (subRenameDict.Count() <= 0) return;
        Task.Factory.StartNew(() => _StartRename(subRenameDict));
    }
    // 获取修改的字幕文件名 (原始完整路径->修改后完整路径)
    private Dictionary<string, string> GetSubRenameDict()
    {
        var dict = new Dictionary<string, string>() { };
        if (VsList.Count <= 0)
            return dict;
        foreach (var item in VsList)
        {
            if (item.Video == null || item.Sub == null) continue;

            string videoName = Path.GetFileNameWithoutExtension(item.VideoFileInfo.Name); // 去掉后缀的视频文件名
            string subAfterFilename = videoName + item.SubFileInfo.Extension; // 修改的字幕文件名
            dict[item.SubFileInfo.FullName] = Path.Combine(item.VideoFileInfo.DirectoryName, subAfterFilename);

        }
        return dict;
    }
    /// 执行改名操作
    private void _StartRename(Dictionary<string, string> subRenameDict)
    {
        try
        {
            foreach (var subRename in subRenameDict)
            {
                _RenameOnce(subRename);
            }
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("成功", "改名成功！", "确定");
            });

        }
        catch (Exception e)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("失败", e.Message, "确定");
            });
        }

    }

    private void _RenameOnce(KeyValuePair<string, string> subRename)
    {
        var vsFile = VsList.Where(o => o.Sub == subRename.Key).ElementAt(0);
        if (vsFile == null) throw new Exception("找不到修改项");
        if (vsFile.Status == VsStatus.Done) return; // 无需再改名了
        if (vsFile.Status != VsStatus.Ready && vsFile.Status != VsStatus.Fatal) throw new Exception("当前状态无法修改");
        if (vsFile.Video == null || vsFile.Sub == null) throw new Exception("字幕/视频文件不完整");

        var before = new FileInfo(subRename.Key);
        var after = new FileInfo(subRename.Value);

        // 若无需修改
        if (before.FullName.Equals(after.FullName))
        {
            vsFile.Status = VsStatus.Done;
            throw new Exception($"文件{vsFile.SubFileInfo.Name}未修改，因为改名后的文件已存在，无需改名");
        }

        // 若原文件不存在
        if (!before.Exists)
        {
            vsFile.Status = VsStatus.Fatal;
            throw new Exception("字幕源文件不存在");
        }

        // 执行备份
        try
        {
            // 前字幕文件 和 后字幕文件 若是在同一个目录下
            if (before.DirectoryName == after.DirectoryName && File.Exists(before.FullName))
                BackupFile(before.FullName);

            if (File.Exists(after.FullName))
                BackupFile(after.FullName);
        }
        catch (Exception e)
        {
            throw new Exception($"改名前备份发生错误 {e.GetType().FullName} {e}");
        }


        // 执行更名
        try
        {
            if (before.DirectoryName == after.DirectoryName)
            {
                if (File.Exists(after.FullName)) File.Delete(after.FullName); // 若后文件存在，则先删除 (上面有备份的)
                File.Move(before.FullName, after.FullName); // 前后字幕相同目录，执行改名
            }
            else
            {
                File.Copy(before.FullName, after.FullName, true); // 前后字幕不同文件，执行复制
            }

            vsFile.Status = VsStatus.Done;
        }
        catch (Exception e)
        {
            // 更名失败
            vsFile.Status = VsStatus.Fatal;
            throw new Exception($"改名发生错误 {e.GetType().FullName} {e}");
        }
    }
    private void BackupFile(string filename)
    {
        if (!File.Exists(filename)) return;
        var bkFolder = Path.Combine(Path.GetDirectoryName(filename), "SubBackup/");
        if (!Directory.Exists(bkFolder)) Directory.CreateDirectory(bkFolder);

        string bkDistFile = Path.Combine(bkFolder, Path.GetFileName(filename));
        if (File.Exists(bkDistFile)) // 解决文件重名问题
            bkDistFile = Path.Combine(
                bkFolder, Path.GetFileNameWithoutExtension(filename) + $".{DateTime.Now}{Path.GetExtension(filename)}"
            );

        File.Copy(filename, bkDistFile, true);
    }
}

