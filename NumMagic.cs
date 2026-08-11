using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Windows.Forms;

namespace NumMagic
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
    }

    class MainForm : Form
    {
        // ── 数据层 ──
        List<string> sList = new List<string>();  // 原始区
        List<string> xList = new List<string>();  // 目标区

        // ── 动态窗格 ──
        const int PER_PANE = 200000;         // 每窗 20 万
        const int PANES_PER_ROW = 4;         // 每行 4 列
        const int DEFAULT_PANES = 8;         // 默认 8 窗(2行×4列)
        const int ROWS = 2;                  // 固定 2 行
        const int MAX_PANES = 50;            // 1000 万 / 20 万

        List<ListView> upPanes = new List<ListView>();
        List<ListView> dnPanes = new List<ListView>();
        Panel upScroll, dnScroll;            // 滚动容器
        int paneW;                           // 每窗宽度
        int rowH = 95;                       // 每行高度
        bool showAll = false;
        Label lShowMore, lCnt1, lCnt2, lInfo, lTitle;
        ProgressBar progress;
        BackgroundWorker worker;

        Button bExit, bHelp, bIn, bImpFile, bImpNum, bFilter, bSort, bClrRpt, bClrNnum, bClear;
        Button bSort2, bNsort, bClrRpt2, bClear2, bOutAll, bCompare, bInsert, bDelete;
        Button bO2T_All, bT2O_All, bO2T_Num, bT2O_Num, bO2T_Feat, bT2O_Feat, bO2T_Type, bT2O_Type;
        string outPath = "", iniPath = "";
        bool sortAsc = true, sort2Asc = true;

        // INI 读写
        [DllImport("kernel32.dll")]
        static extern int GetPrivateProfileString(string lpApp, string lpKey, string lpDefault, StringBuilder lpReturnedString, int nSize, string lpFileName);
        [DllImport("kernel32.dll")]
        static extern bool WritePrivateProfileString(string lpApp, string lpKey, string lpString, string lpFileName);

        string IniRead(string key, string def) { var sb = new StringBuilder(512); GetPrivateProfileString("Settings", key, def, sb, sb.Capacity, iniPath); return sb.ToString(); }
        void IniWrite(string key, string val) { WritePrivateProfileString("Settings", key, val, iniPath); }

        // ── 构造 ──
        public MainForm()
        {
            Text = "龙哥数据_筛选";
            ClientSize = new Size(1300, 720);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            if (Icon == null) try { Icon = SystemIcons.Application; } catch { }
            iniPath = Path.Combine(Application.StartupPath, "settings.ini");
            outPath = IniRead("OutPath", "");
            BuildUI();
            KeyPreview = true;
            KeyDown += OnKeyDown;
            FormClosing += OnFormClosing;
            // 启动时自动加载上次保存的数据
            string dataFile = Path.Combine(Application.StartupPath, "data.txt");
            if (File.Exists(dataFile))
            {
                try
                {
                    sList.AddRange(File.ReadAllLines(dataFile, Encoding.UTF8));
                    lInfo.Text = string.Format("已恢复 {0:n0} 条", sList.Count);
                }
                catch { }
            }
            UpdateAllPanes();
        }

        // ── 构建 UI ──
        void BuildUI()
        {
            BackColor = SystemColors.Control;
            int W = ClientSize.Width, pad = 12, PAD = 6;
            int upY = 44, upBtnY = upY + 2, bw = 68, bh2 = 24, bg = 4;
            paneW = (W - pad * 2 - PAD * (PANES_PER_ROW - 1)) / PANES_PER_ROW;

            // ---- 标题栏 ----
            var logo = new PictureBox { Left = pad, Top = 6, Width = 32, Height = 32, SizeMode = PictureBoxSizeMode.Zoom };
            try { logo.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap(); } catch { }
            Controls.Add(logo);
            lTitle = new Label { Text = "龙哥数据_筛选", Left = 50, Top = 8, Font = new Font("微软雅黑", 12, FontStyle.Bold), AutoSize = true, ForeColor = Color.Black };
            Controls.Add(lTitle);
            lInfo = new Label { Left = 240, Top = 12, AutoSize = true, Font = new Font("微软雅黑", 10, FontStyle.Bold), ForeColor = Color.DarkSlateGray };
            Controls.Add(lInfo);
            bHelp = Btn("帮助", W - 140, 4, 56, 28, OnHelp); bHelp.FlatStyle = FlatStyle.Flat; bHelp.FlatAppearance.BorderColor = Color.Gray; Controls.Add(bHelp);
            bExit = Btn("退出", W - 72, 4, 60, 28, (s2, e2) => Close()); bExit.FlatStyle = FlatStyle.Flat; bExit.FlatAppearance.BorderColor = Color.Gray; Controls.Add(bExit);

            // ==== 原始区按钮行 —— 居中 ====
            int mainW = bw * 4 + 56 * 3 + 70 + 56 + bg * 7;
            int bx = (W - mainW) / 2;
            lCnt1 = new Label { Text = "共 0 个", Left = pad, Top = upBtnY + 2, Font = new Font("微软雅黑", 9, FontStyle.Bold), ForeColor = Color.DarkBlue, AutoSize = true };
            Controls.Add(lCnt1);
            bIn = Btn("输入号段", bx, upBtnY, bw, bh2, OnInput);
            bImpFile = Btn("导入文件", bx + (bw + bg) * 1, upBtnY, bw, bh2, OnImportFile);
            bImpNum = Btn("导入号码", bx + (bw + bg) * 2, upBtnY, bw, bh2, OnImportNum);
            bFilter = Btn("过滤", bx + (bw + bg) * 3, upBtnY, 56, bh2, OnFilter);
            bSort = Btn("排序↑", bx + (bw + bg) * 3 + 60, upBtnY, 56, bh2, OnSortUp);
            bClrRpt = Btn("去重", bx + (bw + bg) * 3 + 120, upBtnY, 56, bh2, (s, e) => { int old = sList.Count; sList = sList.Distinct().ToList(); UpdateAllPanes(); MessageBox.Show(string.Format("去重: {0} -> {1}, 移除 {2}", old, sList.Count, old - sList.Count), "完成"); });
            bClrNnum = Btn("清除非号", bx + (bw + bg) * 3 + 180, upBtnY, 70, bh2, OnClrNoPhone);
            bClear = Btn("清空", bx + (bw + bg) * 3 + 254, upBtnY, 56, bh2, (s, e) => { sList.Clear(); UpdateAllPanes(); });
            Controls.AddRange(new Control[] { bIn, bImpFile, bImpNum, bFilter, bSort, bClrRpt, bClrNnum, bClear });

            // ==== 原始区 滚动窗格 ====
            int uPaneTop = upBtnY + 30;
            int scrollVisH = ROWS * (rowH + PAD);   // 默认可见 2 行高度
            upScroll = new Panel {
                Left = pad, Top = uPaneTop,
                Width = W - pad * 2, Height = scrollVisH,
                BorderStyle = BorderStyle.None,
                AutoScroll = true
            };
            upScroll.HorizontalScroll.Visible = false;
            Controls.Add(upScroll);

            // 预创建默认 8 窗格在滚动面板内
            EnsurePanes(upScroll, upPanes, "UP", DEFAULT_PANES);

            // ==== +前缀/-前缀/-删除 ====
            int insY = uPaneTop + scrollVisH + PAD + 4;
            int pfxsW = 68 + 74 + 68 + bg * 2;
            int pfxX = (W - pfxsW) / 2;
            bInsert = Btn("+ 前缀", pfxX, insY, 68, 22, OnInsert);
            var bRemovePre = Btn("- 前缀", pfxX + 74, insY, 68, 22, OnRemovePrefix);
            bDelete = Btn("- 删除", pfxX + 148, insY, 68, 22, OnDelete);
            Controls.AddRange(new Control[] { bInsert, bRemovePre, bDelete });

            // ==== 分隔线 ====
            int sepY = insY + 28;
            Controls.Add(new Label { Left = pad, Top = sepY, Width = W - pad * 2, Height = 2, BackColor = Color.LightGray, BorderStyle = BorderStyle.None });

            // ==== 移动按钮 (8个, 两行) ====
            int mvY = sepY + 8, mvMW = 100, mvMG = 8;
            int mvMX = (W - (mvMW * 4 + mvMG * 3)) / 2;
            bO2T_All = Btn("▼ 全部移下", mvMX, mvY, mvMW, 26, (s, e) => { xList.AddRange(sList); sList.Clear(); UpdateAllPanes(); });
            bT2O_All = Btn("▲ 全部移上", mvMX, mvY + 28, mvMW, 26, (s, e) => { sList.AddRange(xList); xList.Clear(); UpdateAllPanes(); });
            bO2T_Num = Btn("▼ 按值移下", mvMX + (mvMW + mvMG), mvY, mvMW, 26, (s, e) => MoveSelUp());
            bT2O_Num = Btn("▲ 按值移上", mvMX + (mvMW + mvMG), mvY + 28, mvMW, 26, (s, e) => MoveSelDn());
            bO2T_Feat = Btn("▼ 按特征移下", mvMX + (mvMW + mvMG) * 2, mvY, mvMW, 26, (s, e) => MoveByFeat(true));
            bT2O_Feat = Btn("▲ 按特征移上", mvMX + (mvMW + mvMG) * 2, mvY + 28, mvMW, 26, (s, e) => MoveByFeat(false));
            bO2T_Type = Btn("▼ 按类型移下", mvMX + (mvMW + mvMG) * 3, mvY, mvMW, 26, (s, e) => MoveByType(true));
            bT2O_Type = Btn("▲ 按类型移上", mvMX + (mvMW + mvMG) * 3, mvY + 28, mvMW, 26, (s, e) => MoveByType(false));
            Controls.AddRange(new Control[] { bO2T_All, bT2O_All, bO2T_Num, bT2O_Num, bO2T_Feat, bT2O_Feat, bO2T_Type, bT2O_Type });

            // ==== 目标区按钮行 —— 居中 ====
            int dnY = mvY + 60, dnBtnY = dnY + 2;
            int dnBtnsW = 56 * 4 + 72 + 56 + bg * 5;
            int dbx = (W - dnBtnsW) / 2;
            lCnt2 = new Label { Text = "共 0 个", Left = pad, Top = dnBtnY + 2, Font = new Font("微软雅黑", 9, FontStyle.Bold), ForeColor = Color.DarkBlue, AutoSize = true };
            Controls.Add(lCnt2);
            bSort2 = Btn("排序↑", dbx, dnBtnY, 56, bh2, OnSortDn);
            bNsort = Btn("乱序", dbx + 62, dnBtnY, 56, bh2, (s, e) => {
                ShowProgress("乱序中...", 0);
                Shuffle(xList);
                UpdateAllPanes();
                HideProgress();
            });
            bClrRpt2 = Btn("去重", dbx + 62 * 2, dnBtnY, 56, bh2, (s, e) => { int old = xList.Count; xList = xList.Distinct().ToList(); UpdateAllPanes(); MessageBox.Show(string.Format("去重: {0} -> {1}, 移除 {2}", old, xList.Count, old - xList.Count), "完成"); });
            bClear2 = Btn("清空", dbx + 62 * 3, dnBtnY, 56, bh2, (s, e) => { xList.Clear(); UpdateAllPanes(); });
            bCompare = Btn("文件对比", dbx + 62 * 4, dnBtnY, 72, bh2, OnCompare);
            bOutAll = Btn("报告", dbx + 62 * 4 + 78, dnBtnY, 56, bh2, OnReport);
            Controls.AddRange(new Control[] { bSort2, bNsort, bClrRpt2, bClear2, bCompare, bOutAll });

            // ==== 目标区 滚动窗格 ====
            int dnPaneTop = dnBtnY + 30;
            dnScroll = new Panel {
                Left = pad, Top = dnPaneTop,
                Width = W - pad * 2, Height = scrollVisH,
                BorderStyle = BorderStyle.None,
                AutoScroll = true
            };
            dnScroll.HorizontalScroll.Visible = false;
            Controls.Add(dnScroll);

            EnsurePanes(dnScroll, dnPanes, "DN", DEFAULT_PANES);

            // ==== 导出按钮 + 显示全部 + 进度条 ====
            int botY = dnPaneTop + scrollVisH + PAD + 6;
            int exw = 80, exh = 22, exg = 4;
            var bExpAll = Btn("导出全部", pad, botY, exw, exh, OnExportAll);
            var bExpBat = Btn("分批导出", pad + (exw + exg) * 1, botY, exw, exh, OnExportBatch);
            var bExpRgn = Btn("按区域导出", pad + (exw + exg) * 2, botY, exw, exh, OnExportRgn);
            var bExpOpr = Btn("按运营商导出", pad + (exw + exg) * 3, botY, 84, exh, OnExportOprt);
            Controls.AddRange(new Control[] { bExpAll, bExpBat, bExpRgn, bExpOpr });

            lShowMore = new Label { Left = pad + (exw + exg) * 4 + 20, Top = botY + 2, AutoSize = true, Font = new Font("微软雅黑", 9), ForeColor = Color.DarkGray };
            Controls.Add(lShowMore);
            var cbShowAll = new CheckBox { Text = "显示超上限(>1000万)", Left = pad + (exw + exg) * 4 + 20, Top = botY + 22, AutoSize = true };
            cbShowAll.CheckedChanged += (s, e) => { showAll = cbShowAll.Checked; UpdateAllPanes(); };
            Controls.Add(cbShowAll);

            progress = new ProgressBar {
                Left = pad, Top = botY + 44, Width = W - pad * 2, Height = 18,
                Minimum = 0, Maximum = 100, Visible = false, Style = ProgressBarStyle.Continuous
            };
            Controls.Add(progress);

            worker = new BackgroundWorker { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            worker.ProgressChanged += (s, e) => { if (e.UserState is string) { string m = (string)e.UserState; lInfo.Text = m; } progress.Value = e.ProgressPercentage; };
            worker.RunWorkerCompleted += (s, e) => { progress.Visible = false; UpdateAllPanes(); };
        }

        // ── 动态创建窗格 ── 固定2行, 列向右增长(水平滚动)
        void EnsurePanes(Panel scrollPanel, List<ListView> panes, string prefix, int count)
        {
            while (panes.Count < count)
            {
                int i = panes.Count;
                int r = i % ROWS;          // 行: 0,1,0,1,...
                int c = i / ROWS;          // 列: 0,0,1,1,2,2,... 向右增长
                int x = c * (paneW + 6);
                int y = r * (rowH + 6);

                var lv = new ListView {
                    Left = x, Top = y, Width = paneW, Height = rowH,
                    View = View.Details, FullRowSelect = true, VirtualMode = true,
                    BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White,
                    Font = new Font("Consolas", 10), Tag = prefix + ":" + i
                };
                lv.Columns.Add("号码", paneW - 25);
                if (prefix == "UP")
                {
                    lv.RetrieveVirtualItem += OnRetrieveUp;
                    lv.DoubleClick += (s2, e2) => MoveSelUp();
                }
                else
                {
                    lv.RetrieveVirtualItem += OnRetrieveDn;
                    lv.DoubleClick += (s2, e2) => MoveSelDn();
                }
                lv.MouseClick += OnPaneClick;
                panes.Add(lv);
                scrollPanel.Controls.Add(lv);
            }
        }

        // ── VirtualMode 回调 ──
        void OnRetrieveUp(object sender, RetrieveVirtualItemEventArgs e)
        {
            var lv = (ListView)sender;
            int idx = int.Parse(lv.Tag.ToString().Split(':')[1]) * PER_PANE + e.ItemIndex;
            if (idx < sList.Count) e.Item = new ListViewItem(sList[idx]);
        }
        void OnRetrieveDn(object sender, RetrieveVirtualItemEventArgs e)
        {
            var lv = (ListView)sender;
            int idx = int.Parse(lv.Tag.ToString().Split(':')[1]) * PER_PANE + e.ItemIndex;
            if (idx < xList.Count) e.Item = new ListViewItem(xList[idx]);
        }

        // ── 更新所有窗格 ──
        void UpdateAllPanes()
        {
            // 计算每区需要的窗格数
            int upMax = showAll ? int.MaxValue : MAX_PANES * PER_PANE;
            int upItems = Math.Min(sList.Count, upMax);
            int upNeed = Math.Min(MAX_PANES, Math.Max(DEFAULT_PANES, (sList.Count + PER_PANE - 1) / PER_PANE));

            int dnMax = showAll ? int.MaxValue : MAX_PANES * PER_PANE;
            int dnItems = Math.Min(xList.Count, dnMax);
            int dnNeed = Math.Min(MAX_PANES, Math.Max(DEFAULT_PANES, (xList.Count + PER_PANE - 1) / PER_PANE));

            // 动态增窗
            EnsurePanes(upScroll, upPanes, "UP", upNeed);
            EnsurePanes(dnScroll, dnPanes, "DN", dnNeed);

            // 设置每窗可见条数
            for (int i = 0; i < upPanes.Count; i++)
            {
                int start = i * PER_PANE;
                int cnt = (start >= upItems) ? 0 : Math.Min(upItems - start, PER_PANE);
                upPanes[i].VirtualListSize = cnt;
            }
            for (int i = 0; i < dnPanes.Count; i++)
            {
                int start = i * PER_PANE;
                int cnt = (start >= dnItems) ? 0 : Math.Min(dnItems - start, PER_PANE);
                dnPanes[i].VirtualListSize = cnt;
            }

            // 标签
            string upLabel = sList.Count > DEFAULT_PANES * PER_PANE
                ? string.Format("原始区 {0:n0} / 共{1:n0}", upItems, sList.Count)
                : string.Format("原始区 共{0:n0}个", sList.Count);
            string dnLabel = xList.Count > DEFAULT_PANES * PER_PANE
                ? string.Format("目标区 {0:n0} / 共{1:n0}", dnItems, xList.Count)
                : string.Format("目标区 共{0:n0}个", xList.Count);
            lCnt1.Text = upLabel;
            lCnt2.Text = dnLabel;
            lInfo.Text = string.Format("原始 {0:n0} | 目标 {1:n0}", sList.Count, xList.Count);

            int total = sList.Count + xList.Count;
            if (total > DEFAULT_PANES * PER_PANE && !showAll)
                lShowMore.Text = string.Format("已隐藏 {0:n0} 条，勾选「显示全部」查看", total - DEFAULT_PANES * PER_PANE);
            else
                lShowMore.Text = "";
        }

        // ── 窗格点击 —— 跟踪焦点 ──
        ListView focusedPane = null;
        void OnPaneClick(object sender, MouseEventArgs e)
        {
            focusedPane = (ListView)sender;
        }

        // ── 辅助方法 ──
        Button Btn(string text, int x, int y, int w, int h, EventHandler onClick)
        {
            var b = new Button { Text = text, Left = x, Top = y, Width = w, Height = h, FlatStyle = FlatStyle.Flat };
            b.FlatAppearance.BorderColor = Color.Gray;
            b.Click += onClick;
            return b;
        }

        // ── 进度条 ──
        void ShowProgress(string msg, int pct)
        {
            lInfo.Text = msg;
            progress.Visible = true;
            progress.Value = Math.Min(pct, 100);
            Application.DoEvents();
        }
        void HideProgress() { progress.Visible = false; }
        void EnableUI(bool enable) { Enabled = enable; }

        // ── 乱序 ── Fisher-Yates 原地 O(n)
        static void Shuffle(List<string> list)
        {
            var r = new Random();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = r.Next(i + 1);
                string t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }

        // ── 排序 (异步, 百万级不卡 UI) ──
        void OnSortUp(object sender, EventArgs e)
        {
            sortAsc = !sortAsc;
            bSort.Text = sortAsc ? "排序↑" : "排序↓";
            EnableUI(false);
            var bw = new BackgroundWorker();
            bw.DoWork += (s2, e2) =>
            {
                sList.Sort(sortAsc ? (Comparison<string>)((a, b) => string.Compare(a, b, StringComparison.Ordinal))
                                   : (Comparison<string>)((a, b) => string.Compare(b, a, StringComparison.Ordinal)));
            };
            bw.RunWorkerCompleted += (s2, e2) => { EnableUI(true); UpdateAllPanes(); };
            bw.RunWorkerAsync();
        }
        void OnSortDn(object sender, EventArgs e)
        {
            sort2Asc = !sort2Asc;
            bSort2.Text = sort2Asc ? "排序↑" : "排序↓";
            EnableUI(false);
            var bw = new BackgroundWorker();
            bw.DoWork += (s2, e2) =>
            {
                xList.Sort(sort2Asc ? (Comparison<string>)((a, b) => string.Compare(a, b, StringComparison.Ordinal))
                                    : (Comparison<string>)((a, b) => string.Compare(b, a, StringComparison.Ordinal)));
            };
            bw.RunWorkerCompleted += (s2, e2) => { EnableUI(true); UpdateAllPanes(); };
            bw.RunWorkerAsync();
        }

        // ── 键盘 ──
        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A) { return; }
            if (e.KeyCode == Keys.Delete) OnDelete(sender, e);
        }

        // ── 关闭 ──
        void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            // 自动保存数据 (启动时恢复)
            string dataFile = Path.Combine(Application.StartupPath, "data.txt");
            int total = sList.Count + xList.Count;
            if (total > 0)
            {
                try
                {
                    var all = new List<string>(sList);
                    all.AddRange(xList);
                    File.WriteAllLines(dataFile, all.Distinct(), Encoding.UTF8);
                }
                catch { }
            }
        }

        // ── 号码清洗 ──
        string Clean(string raw) { return Regex.Replace(raw.Trim(), @"[\s\-\+\(\)\.\,]", ""); }

        // ── 插入前缀 ──
        void OnInsert(object sender, EventArgs e)
        {
            var f = new Form { Text = "插入前缀", Size = new Size(350, 160), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            f.BackColor = SystemColors.Control;
            f.Controls.Add(new Label { Text = "输入要插入到每行号码前的内容:", Left = 12, Top = 12, AutoSize = true });
            var tb = new TextBox { Left = 12, Top = 38, Width = 310 };
            f.Controls.Add(tb);
            f.Controls.Add(new Label { Text = "例如输入 +86 →  138xxx 变成 +86138xxx", Left = 12, Top = 70, AutoSize = true, ForeColor = Color.Gray, Font = new Font("微软雅黑", 8) });
            var bOk = new Button { Text = "插入前缀", Left = 240, Top = 95, Width = 80 };
            bOk.Click += (okS, okE) =>
            {
                string prefix = tb.Text;
                if (string.IsNullOrEmpty(prefix)) { f.Close(); return; }
                int old = sList.Count;
                for (int i = 0; i < sList.Count; i++) sList[i] = prefix + sList[i];
                UpdateAllPanes();
                MessageBox.Show(string.Format("已为 {0} 条号码添加前缀 \"{1}\"", old, prefix), "完成");
                f.Close();
            };
            f.Controls.Add(bOk);
            var bCancel = new Button { Text = "取消", Left = 160, Top = 95, Width = 70 };
            bCancel.Click += (cS, cE) => f.Close();
            f.Controls.Add(bCancel);
            f.ShowDialog();
        }

        // ── 去除前缀 ──
        void OnRemovePrefix(object sender, EventArgs e)
        {
            var f = new Form { Text = "去除前缀", Size = new Size(350, 160), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            f.BackColor = SystemColors.Control;
            f.Controls.Add(new Label { Text = "输入要去除的前缀内容:", Left = 12, Top = 12, AutoSize = true });
            var tb = new TextBox { Left = 12, Top = 38, Width = 310 };
            f.Controls.Add(tb);
            f.Controls.Add(new Label { Text = "例如输入 +86 →  +86138xxx 变成 138xxx", Left = 12, Top = 70, AutoSize = true, ForeColor = Color.Gray, Font = new Font("微软雅黑", 8) });
            var bOk = new Button { Text = "去除前缀", Left = 240, Top = 95, Width = 80 };
            bOk.Click += (okS, okE) =>
            {
                string prefix = tb.Text;
                if (string.IsNullOrEmpty(prefix)) { f.Close(); return; }
                int old = sList.Count, changed = 0;
                for (int i = 0; i < sList.Count; i++)
                {
                    if (sList[i].StartsWith(prefix)) { sList[i] = sList[i].Substring(prefix.Length); changed++; }
                }
                UpdateAllPanes();
                MessageBox.Show(string.Format("{0} 条中 {1} 条已去除前缀 \"{2}\"", old, changed, prefix), "完成");
                f.Close();
            };
            f.Controls.Add(bOk);
            var bCancel = new Button { Text = "取消", Left = 160, Top = 95, Width = 70 };
            bCancel.Click += (cS, cE) => f.Close();
            f.Controls.Add(bCancel);
            f.ShowDialog();
        }

        // ── 删除选中 ──
        void OnDelete(object sender, EventArgs e)
        {
            foreach (var lv in upPanes)
            {
                if (lv.SelectedIndices.Count > 0)
                {
                    var idxs = lv.SelectedIndices.Cast<int>().ToList();
                    int paneIdx = int.Parse(lv.Tag.ToString().Split(':')[1]);
                    int baseOff = paneIdx * PER_PANE;
                    var globalIdxs = idxs.Select(i => baseOff + i).OrderByDescending(i => i).ToList();
                    foreach (int gi in globalIdxs) { if (gi < sList.Count) sList.RemoveAt(gi); }
                    UpdateAllPanes();
                    return;
                }
            }
            foreach (var lv in dnPanes)
            {
                if (lv.SelectedIndices.Count > 0)
                {
                    var idxs = lv.SelectedIndices.Cast<int>().ToList();
                    int paneIdx = int.Parse(lv.Tag.ToString().Split(':')[1]);
                    int baseOff = paneIdx * PER_PANE;
                    var globalIdxs = idxs.Select(i => baseOff + i).OrderByDescending(i => i).ToList();
                    foreach (int gi in globalIdxs) { if (gi < xList.Count) xList.RemoveAt(gi); }
                    UpdateAllPanes();
                    return;
                }
            }
        }

        // ── 导入：剪贴板 ──
        void OnImportNum(object sender, EventArgs e)
        {
            string text = "";
            try { text = Clipboard.GetText(); } catch { }
            if (string.IsNullOrWhiteSpace(text)) { MessageBox.Show("剪贴板为空", "提示"); return; }
            ImportText(text);
        }

        // ── 导入：文件 ──
        void OnImportFile(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "选择号码文件", Filter = "文本文件|*.txt|CSV文件|*.csv|所有文件|*.*" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            Enabled = false;
            var newList = new List<string>();
            try
            {
                using (var sr = new StreamReader(dlg.FileName, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var num = Clean(line);
                        if (num.Length > 0) newList.Add(num);
                        if (newList.Count % 50000 == 0) ShowProgress(string.Format("已读 {0:n0} 行...", newList.Count), 0);
                    }
                }
            }
            catch
            {
                using (var sr = new StreamReader(dlg.FileName, Encoding.GetEncoding("GBK")))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var num = Clean(line);
                        if (num.Length > 0) newList.Add(num);
                        if (newList.Count % 50000 == 0) ShowProgress(string.Format("已读 {0:n0} 行...", newList.Count), 0);
                    }
                }
            }
            sList.AddRange(newList.Distinct());
            if (sList.Count > 0) { lInfo.Text = string.Format("原始区: {0:n0} 条", sList.Count); }
            HideProgress();
            Enabled = true;
            UpdateAllPanes();
            MessageBox.Show(string.Format("导入完成: {0:n0} 条", newList.Count), "完成");
        }

        void ImportText(string text)
        {
            Enabled = false;
            ShowProgress("正在解析...", 0);
            var newList = new List<string>();
            foreach (var line in text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var num = Clean(line);
                if (num.Length > 0) newList.Add(num);
            }
            sList.AddRange(newList);
            HideProgress();
            Enabled = true;
            UpdateAllPanes();
            MessageBox.Show(string.Format("导入完成: {0:n0} 条", newList.Count), "完成");
        }

        // ── 导入：号段 ──
        void OnInput(object sender, EventArgs e)
        {
            var f = new Form { Text = "输入号段", Size = new Size(400, 240), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            f.BackColor = SystemColors.Control;
            f.Controls.Add(new Label { Text = "前缀:", Left = 20, Top = 20, AutoSize = true });
            var tbHead = new TextBox { Left = 80, Top = 18, Width = 100 };
            f.Controls.Add(tbHead);
            f.Controls.Add(new Label { Text = "起始:", Left = 20, Top = 55, AutoSize = true });
            var tbStart = new TextBox { Left = 80, Top = 52, Width = 100 };
            f.Controls.Add(tbStart);
            f.Controls.Add(new Label { Text = "结束:", Left = 200, Top = 55, AutoSize = true });
            var tbEnd = new TextBox { Left = 250, Top = 52, Width = 100 };
            f.Controls.Add(tbEnd);
            f.Controls.Add(new Label { Text = "多行前缀 (每行一个前缀, 与区间组合生成):", Left = 20, Top = 90, AutoSize = true });
            var tbMulti = new TextBox { Left = 20, Top = 115, Width = 350, Height = 50, Multiline = true, ScrollBars = ScrollBars.Vertical };
            f.Controls.Add(tbMulti);

            var bOk = new Button { Text = "生成", Left = 290, Top = 170, Width = 80 };
            bOk.Click += (s2, e2) =>
            {
                long start = 0, end = 0;
                if (!long.TryParse(tbStart.Text.Trim(), out start) || !long.TryParse(tbEnd.Text.Trim(), out end))
                {
                    MessageBox.Show("起始/结束必须为数字", "错误");
                    return;
                }
                if (start > end) { long tmp = start; start = end; end = tmp; }

                var heads = new List<string>();
                if (!string.IsNullOrWhiteSpace(tbMulti.Text))
                    heads.AddRange(tbMulti.Text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
                if (heads.Count == 0)
                    heads.Add(tbHead.Text.Trim());

                Enabled = false;
                var newList = new List<string>();
                long total = (end - start + 1) * heads.Count;
                long count = 0;
                foreach (string h in heads)
                {
                    for (long i = start; i <= end; i++)
                    {
                        newList.Add(h + i);
                        count++;
                        if (count % 100000 == 0 || count == total)
                            ShowProgress(string.Format("生成 {0:n0}/{1:n0}", count, total), (int)(count * 100 / total));
                    }
                }
                sList.AddRange(newList);
                HideProgress();
                Enabled = true;
                UpdateAllPanes();
                MessageBox.Show(string.Format("生成完成: {0:n0} 条", newList.Count), "完成");
                f.Close();
            };
            f.Controls.Add(bOk);
            var bCancel = new Button { Text = "取消", Left = 200, Top = 170, Width = 70 };
            bCancel.Click += (cS, cE) => f.Close();
            f.Controls.Add(bCancel);
            f.ShowDialog();
        }

        // ── 过滤 ──
        void OnFilter(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "选择过滤文件", Filter = "文本文件|*.txt|所有文件|*.*" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            Enabled = false;
            ShowProgress("加载过滤文件...", 0);
            var filterSet = new HashSet<string>();
            try
            {
                using (var sr = new StreamReader(dlg.FileName, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var num = Clean(line);
                        if (num.Length > 0) filterSet.Add(num);
                    }
                }
            }
            catch
            {
                using (var sr = new StreamReader(dlg.FileName, Encoding.GetEncoding("GBK")))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var num = Clean(line);
                        if (num.Length > 0) filterSet.Add(num);
                    }
                }
            }
            ShowProgress("过滤中...", 0);
            int total = sList.Count, removed = 0;
            for (int i = sList.Count - 1; i >= 0; i--)
            {
                if (filterSet.Contains(sList[i])) { sList.RemoveAt(i); removed++; }
            }
            HideProgress();
            Enabled = true;
            UpdateAllPanes();
            MessageBox.Show(string.Format("过滤完成: 上传 {0:n0} 条过滤词, 共扫描 {1:n0} 条, 移除 {2:n0} 条", filterSet.Count, total, removed), "完成");
        }

        // ── 清除非手机号 ──
        void OnClrNoPhone(object sender, EventArgs e)
        {
            int total = sList.Count;
            var valid = new List<string>();
            foreach (var num in sList)
            {
                if (num.Length >= 7 && Regex.IsMatch(num, @"^\d+$"))
                    valid.Add(num);
            }
            sList = valid;
            UpdateAllPanes();
            MessageBox.Show(string.Format("清除非号: {0} -> {1}, 移除 {2}", total, sList.Count, total - sList.Count), "完成");
        }

        // ── 移动选中 ──
        void MoveSelUp()
        {
            if (focusedPane == null || !focusedPane.Tag.ToString().StartsWith("UP")) return;
            if (focusedPane.SelectedIndices.Count == 0) return;
            int paneIdx = int.Parse(focusedPane.Tag.ToString().Split(':')[1]);
            int baseOff = paneIdx * PER_PANE;
            var idxs = focusedPane.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList();
            foreach (int i in idxs)
            {
                int gi = baseOff + i;
                if (gi < sList.Count) { xList.Add(sList[gi]); sList.RemoveAt(gi); }
            }
            UpdateAllPanes();
        }
        void MoveSelDn()
        {
            if (focusedPane == null || !focusedPane.Tag.ToString().StartsWith("DN")) return;
            if (focusedPane.SelectedIndices.Count == 0) return;
            int paneIdx = int.Parse(focusedPane.Tag.ToString().Split(':')[1]);
            int baseOff = paneIdx * PER_PANE;
            var idxs = focusedPane.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList();
            foreach (int i in idxs)
            {
                int gi = baseOff + i;
                if (gi < xList.Count) { sList.Add(xList[gi]); xList.RemoveAt(gi); }
            }
            UpdateAllPanes();
        }

        // ── 按特征/类型移动 (简化, 实际按选中) ──
        // 按特征移动 (数字长度: >=13长号码/11手机/<=10短号)
        void MoveByFeat(bool down)
        {
            var src = focusedPane == null || !focusedPane.Tag.ToString().StartsWith(down ? "UP" : "DN") ? null : (down ? sList : xList);
            var dst = down ? xList : sList;
            if (src == null) return;
            var toMove = src.Where(n => n.Length >= 13).ToList();
            dst.AddRange(toMove);
            foreach (var n in toMove) src.Remove(n);
            UpdateAllPanes();
        }
        // 按类型移动 (国际号/中国手机/固话)
        void MoveByType(bool down)
        {
            var src = focusedPane == null || !focusedPane.Tag.ToString().StartsWith(down ? "UP" : "DN") ? null : (down ? sList : xList);
            var dst = down ? xList : sList;
            if (src == null) return;
            var toMove = src.Where(n => !string.IsNullOrEmpty(n) && (n.StartsWith("+") || n.StartsWith("00")) && !n.StartsWith("+86")).ToList();
            dst.AddRange(toMove);
            foreach (var n in toMove) src.Remove(n);
            UpdateAllPanes();
        }

        // ── 文件对比 ──
        void OnCompare(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "选择对比文件", Filter = "文本文件|*.txt|所有文件|*.*" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            Enabled = false;
            ShowProgress("加载对比文件...", 0);
            var set = new HashSet<string>();
            try
            {
                using (var sr = new StreamReader(dlg.FileName, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var num = Clean(line);
                        if (num.Length > 0) set.Add(num);
                    }
                }
            }
            catch
            {
                using (var sr = new StreamReader(dlg.FileName, Encoding.GetEncoding("GBK")))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var num = Clean(line);
                        if (num.Length > 0) set.Add(num);
                    }
                }
            }
            int both = 0, onlyLocal = 0, onlyFile = 0;
            foreach (var n in xList) if (set.Contains(n)) both++;
            onlyLocal = xList.Count - both;
            onlyFile = set.Count - both;
            HideProgress();
            Enabled = true;
            MessageBox.Show(string.Format("对比结果:\n目标区: {0:n0}\n对比文件: {1:n0}\n共同: {2:n0}\n仅目标区: {3:n0}\n仅对比文件: {4:n0}",
                xList.Count, set.Count, both, onlyLocal, onlyFile), "完成");
        }

        // ── 报告 ──
        void OnReport(object sender, EventArgs e)
        {
            if (xList.Count == 0) { MessageBox.Show("目标区为空", "提示"); return; }
            var sorted = xList.Distinct().OrderBy(n => n).ToList();
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("目标区报告 — {0:n0} 条号码 (去重后)", sorted.Count));
            sb.AppendLine(new string('-', 40));

            // 号段分布
            var seg = new Dictionary<string, int>();
            foreach (var n in sorted)
            {
                string key = n.Length >= 7 ? n.Substring(0, 7) : n;
                if (!seg.ContainsKey(key)) seg[key] = 0;
                seg[key]++;
            }
            sb.AppendLine("TOP 20 号段分布:");
            foreach (var kv in seg.OrderByDescending(kv => kv.Value).Take(20))
                sb.AppendLine(string.Format("  {0}xxx: {1:n0}", kv.Key, kv.Value));

            // 前20条样本
            sb.AppendLine(new string('-', 40));
            sb.AppendLine("前 20 条样本:");
            for (int i = 0; i < Math.Min(20, sorted.Count); i++)
                sb.AppendLine(string.Format("  {0}. {1}", i + 1, sorted[i]));

            var result = sb.ToString();
            MessageBox.Show(result, "报告");
        }

        // ── 区域/国家检测 ──
        static Dictionary<string, string> _countryMap;
        static Dictionary<string, string> CountryMap()
        {
            if (_countryMap != null) return _countryMap;
            var m = new Dictionary<string, string>();
            // 亚洲
            m["86"]="中国";m["852"]="香港";m["853"]="澳门";m["886"]="台湾";
            m["81"]="日本";m["82"]="韩国";m["84"]="越南";m["66"]="泰国";
            m["62"]="印尼";m["60"]="马来西亚";m["63"]="菲律宾";m["65"]="新加坡";
            m["91"]="印度";m["92"]="巴基斯坦";m["95"]="缅甸";m["855"]="柬埔寨";
            m["856"]="老挝";m["880"]="孟加拉";m["94"]="斯里兰卡";m["977"]="尼泊尔";
            m["976"]="蒙古";m["850"]="朝鲜";m["98"]="伊朗";m["90"]="土耳其";
            m["966"]="沙特";m["971"]="阿联酋";m["972"]="以色列";m["961"]="黎巴嫩";
            // 欧洲
            m["7"]="俄罗斯/哈萨克斯坦";m["44"]="英国";m["49"]="德国";m["33"]="法国";
            m["39"]="意大利";m["34"]="西班牙";m["31"]="荷兰";m["32"]="比利时";
            m["41"]="瑞士";m["43"]="奥地利";m["46"]="瑞典";m["47"]="挪威";
            m["45"]="丹麦";m["358"]="芬兰";m["48"]="波兰";m["380"]="乌克兰";
            m["375"]="白俄罗斯";m["40"]="罗马尼亚";m["36"]="匈牙利";m["420"]="捷克";
            m["421"]="斯洛伐克";m["30"]="希腊";m["351"]="葡萄牙";m["353"]="爱尔兰";
            // 非洲
            m["20"]="埃及";m["234"]="尼日利亚";m["254"]="肯尼亚";m["27"]="南非";
            m["233"]="加纳";m["256"]="乌干达";m["255"]="坦桑尼亚";m["251"]="埃塞俄比亚";
            // 美洲
            m["1"]="美国/加拿大";m["52"]="墨西哥";m["55"]="巴西";m["54"]="阿根廷";
            m["57"]="哥伦比亚";m["51"]="秘鲁";m["56"]="智利";m["58"]="委内瑞拉";
            // 大洋洲
            m["61"]="澳大利亚";m["64"]="新西兰";
            _countryMap = m;
            return m;
        }

        // 中国手机运营商检测 (前3位)
        static string GetCarrier(string num)
        {
            if (num.Length < 3) return "未知";
            string p3 = num.Substring(0, 3);
            // 移动
            if (p3 == "134" || (string.Compare(p3, "135") >= 0 && string.Compare(p3, "139") <= 0) ||
                p3 == "147" || p3 == "148" || p3 == "150" || p3 == "151" || p3 == "152" ||
                p3 == "157" || p3 == "158" || p3 == "159" || p3 == "165" || p3 == "172" ||
                p3 == "178" || p3 == "182" || p3 == "183" || p3 == "184" || p3 == "187" ||
                p3 == "188" || p3 == "195" || p3 == "197" || p3 == "198")
                return "中国移动";
            // 联通
            if (p3 == "130" || p3 == "131" || p3 == "132" || p3 == "145" || p3 == "146" ||
                p3 == "155" || p3 == "156" || p3 == "166" || p3 == "167" || p3 == "171" ||
                p3 == "175" || p3 == "176" || p3 == "185" || p3 == "186" || p3 == "196")
                return "中国联通";
            // 电信
            if (p3 == "133" || p3 == "141" || p3 == "149" || p3 == "153" || p3 == "162" ||
                p3 == "170" || p3 == "173" || p3 == "174" || p3 == "177" || p3 == "180" ||
                p3 == "181" || p3 == "189" || p3 == "190" || p3 == "191" || p3 == "193" || p3 == "199")
                return "中国电信";
            // 广电
            if (p3 == "192") return "中国广电";
            return "未知";
        }

        // 中国手机号省份/区域 (按前3位号段)
        static string GetProvince(string num)
        {
            if (num.Length < 3) return "中国";
            string p3 = num.Substring(0, 3);
            // 注意: 同一前缀可能分配给多个省份(号段重分配), 仅作为近似参考
            var prov = new Dictionary<string, string> {
                {"134","北京/广东/上海"},{"135","北京"},{"136","北京/广东"},{"137","北京"},{"138","北京/上海/广东/江苏"},{"139","北京/上海"},
                {"150","上海"},{"151","上海"},{"152","上海"},
                {"153","福建"},{"155","广东"},{"156","广东"},{"157","北京/广东"},{"158","广东"},{"159","广东/浙江"},
                {"178","浙江"},{"180","江苏"},{"181","江苏"},{"182","江苏"},{"183","广东"},{"184","广东"},
            };
            if (prov.ContainsKey(p3)) return prov[p3];
            return "中国(" + p3 + ")";
        }

        // 去前缀 提取纯净号码 + 区域标签
        string GetRegion(string num)
        {
            string raw = num.Replace(" ", "").Replace("-", "");
            string clean = raw.Replace("+", "");
            bool hasIntlPrefix = raw.StartsWith("+") || raw.StartsWith("00");
            if (clean.StartsWith("00")) clean = clean.Substring(2);

            // 86前缀: +86138... 或 0086138...
            if (clean.StartsWith("86") && clean.Length >= 13)
            {
                string cn = clean.Substring(2);
                if (cn.Length == 11 && cn.StartsWith("1"))
                    return GetProvince(cn);
                if (cn.StartsWith("0"))
                    return "中国(固话)";
                return "中国";
            }
            // 无前缀中国手机号: 1[3-9]xxxxxxxx (仅在没有+前缀时生效)
            if (!hasIntlPrefix && clean.Length == 11 && clean[0] == '1' && clean[1] >= '3' && clean[1] <= '9')
                return "中国(手机)";
            // 中国固话
            if (!hasIntlPrefix && clean.StartsWith("0") && clean.Length >= 10)
                return "中国(固话)";

            var cmap = CountryMap();
            var keys = new List<string>(cmap.Keys);
            keys.Sort((a, b) => b.Length.CompareTo(a.Length));
            foreach (string code in keys)
            {
                if (clean.StartsWith(code) && clean.Length >= code.Length + 4)
                    return cmap[code];
            }
            return "未知区域";
        }

        // 判断是否中国号码
        bool IsChineseNumber(string num)
        {
            string raw = num.Replace(" ", "").Replace("-", "");
            string clean = raw.Replace("+", "");
            bool hasIntlPrefix = raw.StartsWith("+") || raw.StartsWith("00");
            // 0086 → 中国; 001/0044 → 非中国
            if (clean.StartsWith("00"))
            {
                string after = clean.Substring(2);
                return after.StartsWith("86");
            }
            if (clean.StartsWith("86") && clean.Length >= 13) return true;
            // 无前缀 11位 1[3-9] 手机号 (有+前缀的不在此列)
            if (!hasIntlPrefix && clean.Length == 11 && clean[0] == '1' && clean[1] >= '3' && clean[1] <= '9') return true;
            if (!hasIntlPrefix && clean.StartsWith("0") && clean.Length >= 10) return true;
            return false;
        }

        void ExportList(List<string> list, string desc)
        {
            var dlg = new SaveFileDialog { Title = desc, Filter = "文本文件|*.txt",
                FileName = desc.Replace(" ", "_") + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") };
            if (!string.IsNullOrEmpty(outPath)) dlg.InitialDirectory = outPath;
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                File.WriteAllLines(dlg.FileName, list, Encoding.UTF8);
                outPath = Path.GetDirectoryName(dlg.FileName);
                IniWrite("OutPath", outPath);
                MessageBox.Show(string.Format("导出完成: {0:n0} 条", list.Count), "完成");
            }
            catch (Exception ex) { MessageBox.Show("导出失败: " + ex.Message, "错误"); }
        }

        void OnExportAll(object s, EventArgs e) {
            var list = xList.Where(n => !IsChineseNumber(n)).ToList();
            ExportList(list, "导出全部");
        }

        void OnExportBatch(object s, EventArgs e)
        {
            var list = xList.Where(n => !IsChineseNumber(n)).ToList();
            if (list.Count == 0) { MessageBox.Show("目标区为空", "提示"); return; }
            var dlg = new FolderBrowserDialog { Description = "选择导出目录" };
            if (!string.IsNullOrEmpty(outPath)) dlg.SelectedPath = outPath;
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                int batch = 1, per = 100000;
                for (int i = 0; i < list.Count; i += per)
                {
                    int cnt = Math.Min(per, list.Count - i);
                    string fn = Path.Combine(dlg.SelectedPath,
                        string.Format("batch_{0}_{1}.txt", batch, DateTime.Now.ToString("yyyyMMddHHmmss")));
                    File.WriteAllLines(fn, list.GetRange(i, cnt), Encoding.UTF8);
                    batch++;
                }
                outPath = dlg.SelectedPath;
                IniWrite("OutPath", outPath);
                MessageBox.Show(string.Format("分批导出完成: {0:n0} 条, {1} 个文件", list.Count, batch - 1), "完成");
            }
            catch (Exception ex) { MessageBox.Show("导出失败: " + ex.Message, "错误"); }
        }

        void OnExportRgn(object s, EventArgs e)
        {
            var list = xList.Where(n => !IsChineseNumber(n)).ToList();
            if (list.Count == 0) { MessageBox.Show("目标区为空", "提示"); return; }
            var dlg = new FolderBrowserDialog { Description = "选择按区域导出目录" };
            if (!string.IsNullOrEmpty(outPath)) dlg.SelectedPath = outPath;
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var groups = new Dictionary<string, List<string>>();
                foreach (string num in list)
                {
                    string region = GetRegion(num);
                    if (!groups.ContainsKey(region)) groups[region] = new List<string>();
                    groups[region].Add(num);
                }

                var sb = new StringBuilder();
                int total = 0;
                foreach (var kv in groups)
                {
                    string fname = kv.Key.Replace("/", "_").Replace("(", "").Replace(")", "");
                    string fn = Path.Combine(dlg.SelectedPath,
                        string.Format("区域_{0}_{1}.txt", fname, DateTime.Now.ToString("yyyyMMddHHmmss")));
                    File.WriteAllLines(fn, kv.Value, Encoding.UTF8);
                    sb.AppendLine(string.Format("  {0}: {1:n0} 条", kv.Key, kv.Value.Count));
                    total += kv.Value.Count;
                }
                outPath = dlg.SelectedPath;
                IniWrite("OutPath", outPath);
                MessageBox.Show(string.Format("按区域导出完成:\n{0}\n共 {1:n0} 条, {2} 个文件",
                    sb.ToString(), total, groups.Count), "完成");
            }
            catch (Exception ex) { MessageBox.Show("导出失败: " + ex.Message, "错误"); }
        }

        void OnExportOprt(object s, EventArgs e)
        {
            var list = xList.Where(n => !IsChineseNumber(n)).ToList();
            if (list.Count == 0) { MessageBox.Show("目标区为空", "提示"); return; }
            var dlg = new FolderBrowserDialog { Description = "选择按运营商导出目录" };
            if (!string.IsNullOrEmpty(outPath)) dlg.SelectedPath = outPath;
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var groups = new Dictionary<string, List<string>>();
                foreach (string num in list)
                {
                    string label = GetRegion(num);
                    if (!groups.ContainsKey(label)) groups[label] = new List<string>();
                    groups[label].Add(num);
                }

                var sb = new StringBuilder();
                int total = 0;
                foreach (var kv in groups)
                {
                    string fname = kv.Key.Replace("/", "_");
                    string fn = Path.Combine(dlg.SelectedPath,
                        string.Format("运营商_{0}_{1}.txt", fname, DateTime.Now.ToString("yyyyMMddHHmmss")));
                    File.WriteAllLines(fn, kv.Value, Encoding.UTF8);
                    sb.AppendLine(string.Format("  {0}: {1:n0} 条", kv.Key, kv.Value.Count));
                    total += kv.Value.Count;
                }
                outPath = dlg.SelectedPath;
                IniWrite("OutPath", outPath);
                MessageBox.Show(string.Format("按运营商导出完成:\n{0}\n共 {1:n0} 条, {2} 个文件",
                    sb.ToString(), total, groups.Count), "完成");
            }
            catch (Exception ex) { MessageBox.Show("导出失败: " + ex.Message, "错误"); }
        }

        // ── 帮助 ──
        void OnHelp(object sender, EventArgs e)
        {
            string msg = @"龙哥数据_筛选 v4.0

【基本操作】
• 导入文件：支持 txt/csv (UTF-8/GBK)
• 导入号码：从剪贴板粘贴
• 输入号段：按前缀+区间批量生成

【核心功能】
• 过滤重复/合并重复：最核心功能
• 排序/乱序：按号码排序或随机打乱
• 去重：删除重复号码
• 清除非号：只保留纯数字>=7位

【窗格说明】
• 默认 8 窗格(每窗 20 万=160 万/区)
• 超 160 万自动增窗 + 滚动查看
• 上限 1000 万，可选显示超上限
• 双击窗格内号码可移入/移出

【移动操作】
• 8 个方向按钮：全部/按值/按特征/按类型 上下移动

【导出】
• 导出全部/分批/按区域/按运营商
• 报告：号段分布 TOP20 + 样本

【快捷键】Delete 删除选中 | Ctrl+A 全选";
            MessageBox.Show(msg, "帮助 - 龙哥数据_筛选");
        }
    }
}