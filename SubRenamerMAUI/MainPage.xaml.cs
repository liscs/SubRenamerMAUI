using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Layouts;
using SubRenamerMAUI.Models;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using static SubRenamerMAUI.Models.Global;

namespace SubRenamerMAUI;
public partial class MainPage : ContentPage
{
    public ObservableCollection<VsItem> VsList = new ObservableCollection<VsItem>();

    public MainPage()
    {

        InitializeComponent();
        //InitPage();
    }

    private void InitPage()
    {


        MainLV = new ListView
        {
            ItemsSource = VsList,

            ItemTemplate = new DataTemplate(() =>
            {
                // Create views with bindings for displaying each property.
                Label matchKeyLabel = new Label();
                matchKeyLabel.SetBinding(Label.TextProperty, "MatchKey");

                Label subLabel = new Label();
                subLabel.SetBinding(Label.TextProperty, "SubFileInfo.Name");

                Label videoLabel = new Label();
                videoLabel.SetBinding(Label.TextProperty, "VideoFileInfo.Name");

                Label statusLabel = new Label();
                statusLabel.SetBinding(Label.TextProperty, "Status");

                FlexLayout.SetBasis(matchKeyLabel, new FlexBasis(0.1f, true));
                FlexLayout.SetBasis(subLabel, new FlexBasis(0.4f, true));
                FlexLayout.SetBasis(videoLabel, new FlexBasis(0.4f, true));
                FlexLayout.SetBasis(statusLabel, new FlexBasis(0.1f, true));

                // Return an assembled ViewCell.
                return new ViewCell
                {
                    View = new FlexLayout
                    {

                        VerticalOptions = LayoutOptions.Center,
                        Children =
                                        {
                                            matchKeyLabel,
                                            subLabel,
                                            videoLabel,
                                            statusLabel
                                        }
                    }

                };
            })
        };

        MainGrid.Clear();
        MainGrid.Add(MainLV);
        MainGrid.Add(MainVSL);
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

    private void Start_Clicked(object sender, EventArgs e)
    {
        var vsItem = new VsItem();
        vsItem.MatchKey = "匹";
        vsItem.Sub = "幕";
        vsItem.Video = "频";
        vsItem.Status = VsStatus.Unmatched;
        VsList.Add(vsItem);
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
}

