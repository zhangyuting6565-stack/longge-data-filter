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
            bFilter = Btn("对比去重", bx + (bw + bg) * 3, upBtnY, 80, bh2, OnFilter);
            bSort = Btn("排序↑", bx + (bw + bg) * 3 + 84, upBtnY, 56, bh2, OnSortUp);
            bClrRpt = Btn("去重", bx + (bw + bg) * 3 + 144, upBtnY, 56, bh2, (s, e) => { int old = sList.Count; sList = DedupNorm(sList); UpdateAllPanes(); MessageBox.Show(string.Format("去重: {0} -> {1}, 移除 {2}", old, sList.Count, old - sList.Count), "完成"); });
            bClrNnum = Btn("清除非号", bx + (bw + bg) * 3 + 204, upBtnY, 70, bh2, OnClrNoPhone);
            bClear = Btn("清空", bx + (bw + bg) * 3 + 278, upBtnY, 56, bh2, (s, e) => { sList.Clear(); UpdateAllPanes(); });
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
            upScroll.HorizontalScroll.Visible = true;
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
            bO2T_Num = Btn("▼ 按值移下", mvMX + (mvMW + mvMG), mvY, mvMW, 26, (s, e) => MoveByValue(true));
            bT2O_Num = Btn("▲ 按值移上", mvMX + (mvMW + mvMG), mvY + 28, mvMW, 26, (s, e) => MoveByValue(false));
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
            bClrRpt2 = Btn("去重", dbx + 62 * 2, dnBtnY, 56, bh2, (s, e) => { int old = xList.Count; xList = DedupNorm(xList); UpdateAllPanes(); MessageBox.Show(string.Format("去重: {0} -> {1}, 移除 {2}", old, xList.Count, old - xList.Count), "完成"); });
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
            dnScroll.HorizontalScroll.Visible = true;
            Controls.Add(dnScroll);

            EnsurePanes(dnScroll, dnPanes, "DN", DEFAULT_PANES);

            // ==== 导出按钮 + 显示全部 + 进度条 ====
            int botY = dnPaneTop + scrollVisH + PAD + 6;
            int exw = 80, exh = 22, exg = 4;
            var bExpAll = Btn("导出全部", pad, botY, exw, exh, OnExportAll);
            var bExpBat = Btn("分批导出", pad + (exw + exg) * 1, botY, exw, exh, OnExportBatch);
            var bExpRng = Btn("按需导出", pad + (exw + exg) * 2, botY, exw, exh, OnExportRange);
            Controls.AddRange(new Control[] { bExpAll, bExpBat, bExpRng });

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
                    lv.DoubleClick += (s2, e2) => MoveSelUpReverse();
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

        // ── 关闭（不保存，每次打开都是干净的）──
        void OnFormClosing(object sender, FormClosingEventArgs e)
        {
        }

        // ── 号码清洗 (手写循环, 比 Regex 快 ~20x) ──
        static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string trimmed = raw.Trim();
            var sb = new StringBuilder(trimmed.Length);
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c == ' ' || c == '\t' || c == '-' || c == '+' || c == '(' || c == ')' || c == '.' || c == ',') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }
        static bool IsAllDigits(string s)
        {
            for (int i = 0; i < s.Length; i++) if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }

        // 匹配基准：统一到「规范国际格式」再做比对（以国际为主）
        // 一个号码在国际文件里多种写法都为同一号：
        //   +8613812345678 / 008613812345678 / 8613812345678 / 裸 13812345678
        //   统统归一成 8613812345678。
        // 非中国国际号同理：+65912345678 / 0065912345678 归一成 65912345678。
        static string Norm(string x)
        {
            if (string.IsNullOrEmpty(x)) return x;
            string s = x;
            // 去除国际拨号前缀写法差异：+ / 00
            if (s.StartsWith("+")) s = s.Substring(1);
            else if (s.StartsWith("00")) s = s.Substring(2);
            // 国内裸 11 位手机号 → 补国际码 86
            if (s.Length == 11 && s[0] == '1' && s[1] >= '3' && s[1] <= '9')
                s = "86" + s;
            return s;   // 其余（国际号/已带码）原样返回
        }

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

        // ── 导入：文件 (支持单个或多个文件一起导入, 合并进原始区) ──
        void OnImportFile(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择号码文件(可 Ctrl 多选/批量拖选)",
                Filter = "文本文件|*.txt|CSV文件|*.csv|所有文件|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            Enabled = false;
            int totalAdded = 0;
            int totalSkipped = 0;
            try
            {
                int fileIdx = 0;
                foreach (string fname in dlg.FileNames)
                {
                    fileIdx++;
                    int added = ImportFileSingle(fname, dlg.FileNames.Length, fileIdx, ref totalSkipped);
                    totalAdded += added;
                }
                if (sList.Count > 0) lInfo.Text = string.Format("原始区: {0:n0} 条", sList.Count);
            }
            finally
            {
                HideProgress();
                Enabled = true;
                UpdateAllPanes();
            }
            string skipMsg = totalSkipped > 0 ? string.Format(", 跳过 {0:n0} 非数字行", totalSkipped) : "";
            MessageBox.Show(string.Format("导入完成: {0} 个文件, 新增 {1:n0} 条{2}",
                dlg.FileNames.Length, totalAdded, skipMsg), "完成");
        }

        // 读取单个号码文件 (UTF-8 失败自动回退 GBK), 返回新增条数
        int ImportFileSingle(string fname, int totalFiles, int fileIdx, ref int skipped)
        {
            var fi = new FileInfo(fname);
            int estLines = (int)Math.Min(fi.Length / 15, int.MaxValue);
            if (estLines > 200000000)
            {
                MessageBox.Show(string.Format("文件过大 (>2亿行), 已跳过: {0}", Path.GetFileName(fname)), "警告");
                return 0;
            }
            var newList = new List<string>(Math.Max(estLines, 10000));
            int localSkip = 0;
            string pfx = totalFiles > 1 ? string.Format("({0}/{1}) {2}", fileIdx, totalFiles, Path.GetFileName(fname)) : Path.GetFileName(fname);
            ShowProgress(string.Format("读取 {0}...", pfx), 0);
            bool ok = false;
            try
            {
                using (var sr = new StreamReader(fname, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var num = Clean(line);
                        if (num.Length > 0)
                        {
                            if (num.Length >= 7 && IsAllDigits(num)) newList.Add(num); else localSkip++;
                        }
                        if ((newList.Count + localSkip) % 50000 == 0)
                            ShowProgress(string.Format("已读 {0} {1:n0} 行...", pfx, newList.Count + localSkip), 0);
                    }
                }
                ok = true;
            }
            catch { }
            if (!ok)
            {
                newList.Clear(); localSkip = 0;
                using (var sr = new StreamReader(fname, Encoding.GetEncoding("GBK")))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var num = Clean(line);
                        if (num.Length > 0)
                        {
                            if (num.Length >= 7 && IsAllDigits(num)) newList.Add(num); else localSkip++;
                        }
                        if ((newList.Count + localSkip) % 50000 == 0)
                            ShowProgress(string.Format("已读 {0} {1:n0} 行...", pfx, newList.Count + localSkip), 0);
                    }
                }
            }
            skipped += localSkip;
            return AppendUnique(sList, newList.Distinct());
        }

        void ImportText(string text)
        {
            Enabled = false;
            ShowProgress("正在解析...", 0);
            var newList = new List<string>();
            foreach (var line in text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var num = Clean(line);
                if (num.Length >= 7 && IsAllDigits(num)) newList.Add(num);
            }
            int added = AppendUnique(sList, newList);
            HideProgress();
            Enabled = true;
            UpdateAllPanes();
            MessageBox.Show(string.Format("导入完成: 新增 {0:n0} 条", added), "完成");
        }

        // ── 导入：号段 ──
        void OnInput(object sender, EventArgs e)
        {
            var f = new Form { Text = "输入号段", Size = new Size(440, 300), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            f.BackColor = SystemColors.Control;
            f.Controls.Add(new Label { Text = "前缀:", Left = 20, Top = 16, AutoSize = true });
            var tbHead = new TextBox { Left = 80, Top = 14, Width = 100 };
            f.Controls.Add(tbHead);
            f.Controls.Add(new Label { Text = "起始:", Left = 20, Top = 50, AutoSize = true });
            var tbStart = new TextBox { Left = 80, Top = 47, Width = 100 };
            f.Controls.Add(tbStart);
            f.Controls.Add(new Label { Text = "结束:", Left = 200, Top = 50, AutoSize = true });
            var tbEnd = new TextBox { Left = 250, Top = 47, Width = 100 };
            f.Controls.Add(tbEnd);
            f.Controls.Add(new Label { Text = "数量(留空=全量生成):", Left = 20, Top = 84, AutoSize = true });
            var tbCount = new TextBox { Left = 150, Top = 81, Width = 100 };
            f.Controls.Add(tbCount);
            f.Controls.Add(new Label { Text = "填数字则从区间内随机抽 N 条不重复号码", Left = 258, Top = 84, AutoSize = true, ForeColor = Color.Gray, Font = new Font("微软雅黑", 8) });
            f.Controls.Add(new Label { Text = "多行前缀 (每行一个前缀, 与区间组合生成):", Left = 20, Top = 118, AutoSize = true });
            var tbMulti = new TextBox { Left = 20, Top = 140, Width = 400, Height = 54, Multiline = true, ScrollBars = ScrollBars.Vertical };
            f.Controls.Add(tbMulti);

            var bOk = new Button { Text = "生成", Left = 330, Top = 205, Width = 80 };
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

                long want = 0;   // 0 = 全量生成
                string countTxt = tbCount.Text.Trim();
                if (countTxt.Length > 0 && !long.TryParse(countTxt, out want))
                {
                    MessageBox.Show("数量必须为数字(或留空=全量)", "错误");
                    return;
                }

                long total = (end - start + 1) * heads.Count;
                if (total > 200000000L) { MessageBox.Show(string.Format("生成数量过多 ({0:n0} 条, 超过 2 亿), 请缩小区间或减少前缀。", total), "警告"); return; }
                // 定量时也不允许超过区间总数
                if (want > total) want = total;

                Enabled = false;
                var newList = new List<string>();

                if (want <= 0 || want == total)
                {
                    // ── 全量生成 (原逻辑) ──
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
                }
                else
                {
                    // ── 随机抽 want 条, 去重 ──
                    int n = (int)Math.Min(want, 500000000L);
                    long span = end - start + 1;
                    var rnd = new Random();
                    var chosen = new HashSet<string>();
                    long guard = 0;
                    long maxGuard = Math.Max(100000L, (long)n * 20L);
                    while (chosen.Count < n && guard < maxGuard)
                    {
                        guard++;
                        string h = heads[rnd.Next(heads.Count)];
                        long v = start + (long)(rnd.NextDouble() * span);
                        if (v > end) v = end;
                        string num = h + v;
                        if (chosen.Add(num)) newList.Add(num);
                        if (newList.Count % 100000 == 0)
                            ShowProgress(string.Format("随机生成 {0:n0}/{1:n0}", newList.Count, n), (int)(newList.Count * 100.0 / n));
                    }
                    if (newList.Count < n)
                        MessageBox.Show(string.Format("仅能生成 {0:n0}/{1:n0} 条(区间剩余可去重组合不足)", newList.Count, n), "提示");
                }

                int added = AppendUnique(sList, newList);
                HideProgress();
                Enabled = true;
                UpdateAllPanes();
                MessageBox.Show(string.Format("生成完成: 新增 {0:n0} 条", added), "完成");
                f.Close();
            };
            f.Controls.Add(bOk);
            var bCancel = new Button { Text = "取消", Left = 240, Top = 205, Width = 80 };
            bCancel.Click += (cS, cE) => f.Close();
            f.Controls.Add(bCancel);
            f.ShowDialog();
        }

        // ── 对比去重 (分批+进度，一亿行不卡UI) ──
        // 语义: 读取"对比上传文件"的号码, 与"原始区"对比;
        //       对比文件中命中原始区(原始区已有=重复) → 过滤移除;
        //       对比文件中原始区没有的号码 → 保留进入目标区。
        // 方向: 对比文件 − 原始区 (保留对比文件里原始区没有的)
        void OnFilter(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "选择对比去重文件", Filter = "文本文件|*.txt|所有文件|*.*" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            // 原始区号码 → 参照集(黑名单) 按国际归一化建key: 对比文件里出现 = 重复, 过滤
            var refSet = new HashSet<string>(sList.Select(Norm));
            Enabled = false;
            ShowProgress("对比去重中...", 0);

            var keep = new List<string>();
            int dupCount = 0;
            int total = 0;
            try
            {
                using (var sr = new StreamReader(dlg.FileName, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var num = Clean(line);
                        // 只收纯数字行 (与导入原始区间规则), 字母/备注行过滤掉
                        if (num.Length >= 7 && IsAllDigits(num))
                        {
                            total++;
                            if (refSet.Contains(Norm(num))) dupCount++;   // 原始区已有 → 重复, 过滤
                            else keep.Add(num);                      // 原始区没有 → 进目标区
                        }
                        if (total % 200000 == 0) { ShowProgress(string.Format("对比去重中 ({0:n0} 行)...", total), 0); }
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
                        if (num.Length >= 7 && IsAllDigits(num))
                        {
                            total++;
                            if (refSet.Contains(Norm(num))) dupCount++;
                            else keep.Add(num);
                        }
                        if (total % 200000 == 0) { ShowProgress(string.Format("对比去重中 ({0:n0} 行)...", total), 0); }
                    }
                }
            }
            xList = keep;            // 目标区 = 对比文件中原始区没有的号码
            sList.Clear();           // 原始区清空（参照完毕）
            HideProgress();
            Enabled = true;
            UpdateAllPanes();
            MessageBox.Show(string.Format("对比去重完成: 原始区 {0:n0} 条, 对比文件 {1:n0} 条, 命中(原始区已有)移除 {2:n0} 条, 保留进目标区 {3:n0} 条",
                            refSet.Count, total, dupCount, keep.Count), "完成");
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
            foreach (var lv in upPanes)
            {
                if (lv.SelectedIndices.Count == 0) continue;
                int paneIdx = int.Parse(lv.Tag.ToString().Split(':')[1]);
                int baseOff = paneIdx * PER_PANE;
                var idxs = lv.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList();
                foreach (int i in idxs)
                {
                    int gi = baseOff + i;
                    if (gi < sList.Count) { xList.Add(sList[gi]); sList.RemoveAt(gi); }
                }
                UpdateAllPanes();
                return;
            }
        }
        void MoveSelUpReverse()
        {
            foreach (var lv in dnPanes)
            {
                if (lv.SelectedIndices.Count == 0) continue;
                int paneIdx = int.Parse(lv.Tag.ToString().Split(':')[1]);
                int baseOff = paneIdx * PER_PANE;
                var idxs = lv.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList();
                foreach (int i in idxs)
                {
                    int gi = baseOff + i;
                    if (gi < xList.Count) { sList.Add(xList[gi]); xList.RemoveAt(gi); }
                }
                UpdateAllPanes();
                return;
            }
        }

        // ── 按值移动 (弹窗输入号码/前缀/子串) ──
        void MoveByValue(bool down)
        {
            var src = down ? sList : xList;
            var dst = down ? xList : sList;
            if (src.Count == 0) { MessageBox.Show((down ? "原始区" : "目标区") + "为空", "提示"); return; }
            var f = new Form { Text = (down ? "▼ 按值移下" : "▲ 按值移上"), Size = new Size(420, 175), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            f.Controls.Add(new Label { Text = "填入要移动的号码 / 前缀 / 任意子串 (支持多行):", Left = 12, Top = 12, AutoSize = true });
            var tb = new TextBox { Left = 12, Top = 34, Width = 380, Height = 55, Multiline = true, ScrollBars = ScrollBars.Vertical };
            f.Controls.Add(tb);
            f.Controls.Add(new Label { Text = "匹配方式: 任意包含(子串)。含 '86' 前缀的也以子串匹配。", Left = 12, Top = 96, AutoSize = true, ForeColor = Color.Gray, Font = new Font("微软雅黑", 8) });
            var bOk = new Button { Text = "移动", Left = 300, Top = 118, Width = 80 };
            bOk.Click += (s2, e2) =>
            {
                string input = tb.Text.Trim();
                if (string.IsNullOrEmpty(input)) { f.Close(); return; }
                var keys = input.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var keyList = new HashSet<string>();
                foreach (var k in keys) { var kk = Clean(k); if (kk.Length > 0) keyList.Add(kk); }
                if (keyList.Count == 0) { f.Close(); return; }
                List<string> toMove, toKeep;
                SplitList(src, n => { foreach (var k in keyList) if (n.Contains(k)) return true; return false; }, out toMove, out toKeep);
                dst.AddRange(toMove);
                if (down) sList = toKeep; else xList = toKeep;
                UpdateAllPanes();
                MessageBox.Show(string.Format("按值移动完成: 移动 {0:n0} 条", toMove.Count), "完成");
                f.Close();
            };
            f.Controls.Add(bOk);
            var bCancel = new Button { Text = "取消", Left = 216, Top = 118, Width = 70 };
            bCancel.Click += (cS, cE) => f.Close();
            f.Controls.Add(bCancel);
            f.ShowDialog();
        }
                // 按特征移动 (可自定义号码长度档位; 以国际为主, 长度为含区号的完整号码位数)
        void MoveByFeat(bool down)
        {
            var src = down ? sList : xList;
            var dst = down ? xList : sList;
            if (src.Count == 0) { MessageBox.Show((down ? "原始区" : "目标区") + "为空", "提示"); return; }
            var f = new Form { Text = (down ? "▼ 按特征移下" : "▲ 按特征移上"), Size = new Size(420, 312), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            f.Controls.Add(new Label { Text = "按号码长度筛选 (长度为完整号码位数, 已含区号):", Left = 14, Top = 12, AutoSize = true });
            var rbCustom = new RadioButton { Text = "自定义长度区间", Left = 22, Top = 40, AutoSize = true, Checked = true };
            f.Controls.Add(rbCustom);
            f.Controls.Add(new Label { Text = "最小位数:", Left = 180, Top = 42, AutoSize = true });
            var tbMin = new TextBox { Left = 240, Top = 40, Width = 46 };
            f.Controls.Add(tbMin);
            f.Controls.Add(new Label { Text = "最大位数:", Left = 180, Top = 68, AutoSize = true });
            var tbMax = new TextBox { Left = 240, Top = 66, Width = 46 };
            f.Controls.Add(tbMax);
            var rbAll = new RadioButton { Text = "全部长度(不限)", Left = 22, Top = 96, AutoSize = true };
            var rbShort = new RadioButton { Text = "≤10位 (区号+短号)", Left = 22, Top = 122, AutoSize = true };
            var rbMid = new RadioButton { Text = "11~12位 (常见国际号)", Left = 22, Top = 148, AutoSize = true };
            var rbLong = new RadioButton { Text = "≥13位 (长国际号)", Left = 22, Top = 174, AutoSize = true };
            f.Controls.AddRange(new Control[] { rbAll, rbShort, rbMid, rbLong });
            f.Controls.Add(new Label { Text = "注: 美国+1本地10位=11位, 中国86+11=13位, 均按含区号计数", Left = 14, Top = 210, AutoSize = true, ForeColor = Color.Gray, Font = new Font(FontFamily.GenericSansSerif, 8) });

            var bOk = new Button { Text = "移动", Left = 316, Top = 244, Width = 80 };
            bOk.Click += (s2, e2) =>
            {
                Func<string, int> dlen = n => { int c = 0; foreach (char ch in n) if (ch >= '0' && ch <= '9') c++; return c; };
                Predicate<string> pred;
                if (rbCustom.Checked)
                {
                    int mn = 0, mx = int.MaxValue;
                    if (!string.IsNullOrWhiteSpace(tbMin.Text) && !int.TryParse(tbMin.Text.Trim(), out mn)) { MessageBox.Show("最小位数必须为整数", "错误"); return; }
                    if (!string.IsNullOrWhiteSpace(tbMax.Text) && !int.TryParse(tbMax.Text.Trim(), out mx)) { MessageBox.Show("最大位数必须为整数", "错误"); return; }
                    pred = n => dlen(n) >= mn && dlen(n) <= mx;
                }
                else if (rbAll.Checked) pred = n => true;
                else if (rbShort.Checked) pred = n => dlen(n) <= 10;
                else if (rbMid.Checked) pred = n => { int l = dlen(n); return l >= 11 && l <= 12; };
                else pred = n => dlen(n) >= 13;
                List<string> toMove, toKeep;
                SplitList(src, pred, out toMove, out toKeep);
                dst.AddRange(toMove);
                if (down) sList = toKeep; else xList = toKeep;
                UpdateAllPanes();
                MessageBox.Show(string.Format("按特征移动完成: 移动 {0:n0} 条", toMove.Count), "完成");
                f.Close();
            };
            f.Controls.Add(bOk);
            var bCancel = new Button { Text = "取消", Left = 226, Top = 244, Width = 80 };
            bCancel.Click += (cS, cE) => f.Close();
            f.Controls.Add(bCancel);
            f.ShowDialog();
        }
// 按类型移动 (号码类型选择弹窗)
        void MoveByType(bool down)
        {
            var src = down ? sList : xList;
            var dst = down ? xList : sList;
            if (src.Count == 0) { MessageBox.Show((down ? "原始区" : "目标区") + "为空", "提示"); return; }
            var f = new Form { Text = (down ? "▼ 按类型移下" : "▲ 按类型移上"), Size = new Size(400, 240), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            f.Controls.Add(new Label { Text = "选择要移动的号码类型:", Left = 16, Top = 14, AutoSize = true });
            var rbA = new RadioButton { Text = "国际号(含国家码, 非86前缀)", Left = 24, Top = 44, AutoSize = true, Checked = true };
            var rbB = new RadioButton { Text = "中国大陆(86/11位国内号)", Left = 24, Top = 72, AutoSize = true };
            var rbC = new RadioButton { Text = "全部(区分国内/国际都移)", Left = 24, Top = 100, AutoSize = true };
            f.Controls.AddRange(new Control[] { rbA, rbB, rbC });
            f.Controls.Add(new Label { Text = "注: 本按钮把选中的类型从源区移到目标区。", Left = 16, Top = 128, AutoSize = true, ForeColor = Color.Gray, Font = new Font("微软雅黑", 8) });
            var bOk = new Button { Text = "移动", Left = 290, Top = 158, Width = 80 };
            bOk.Click += (s2, e2) =>
            {
                Predicate<string> pred;
                if (rbA.Checked)
                    // 清洗后号码已去 +；按国际归一口径判定：非中国号码 && (00开头 或 带区号≥11位)
                    pred = n => !string.IsNullOrEmpty(n) && !IsChineseNumber(n)
                                && (n.StartsWith("00") || n.Length >= 11);
                else if (rbB.Checked)
                    pred = n => !string.IsNullOrEmpty(n) && IsChineseNumber(n);
                else
                    pred = n => true;
                List<string> toMove, toKeep;
                SplitList(src, pred, out toMove, out toKeep);
                dst.AddRange(toMove);
                if (down) sList = toKeep; else xList = toKeep;
                UpdateAllPanes();
                MessageBox.Show(string.Format("按类型移动完成: 移动 {0:n0} 条", toMove.Count), "完成");
                f.Close();
            };
            f.Controls.Add(bOk);
            var bCancel = new Button { Text = "取消", Left = 206, Top = 158, Width = 70 };
            bCancel.Click += (cS, cE) => f.Close();
            f.Controls.Add(bCancel);
            f.ShowDialog();
        }

        // 工具: 一次扫描拆分为两表，避免 O(n²) Remove
        static void SplitList(List<string> src, Predicate<string> pred, out List<string> matched, out List<string> kept)
        {
            matched = new List<string>();
            kept = new List<string>();
            foreach (var n in src) { if (pred(n)) matched.Add(n); else kept.Add(n); }
        }

        // 保序合并去重: 把 newItems 追加到 list, 去掉与 list 已有重复的项(保持原始顺序)
        // 返回实际新增数量。避免多次导入混入重复。
        // 去重key用国际归一化: 同号不同写法(+86138../86138../138..)算同一条, 保留最先写法
        static int AppendUnique(List<string> list, IEnumerable<string> newItems)
        {
            var seen = new HashSet<string>(list.Select(Norm));
            int added = 0;
            foreach (var n in newItems)
            {
                string k = Norm(n);
                if (!string.IsNullOrEmpty(k) && seen.Add(k)) { list.Add(n); added++; }
            }
            return added;
        }

        // ── 去重(国际归一口径)：同号不同写法算同一条, 保留最先出现的写法 ──
        static List<string> DedupNorm(List<string> list)
        {
            var byNorm = new Dictionary<string, string>();
            foreach (var n in list) { string k = Norm(n); if (!byNorm.ContainsKey(k)) byNorm[k] = n; }
            return byNorm.Values.ToList();
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
                        if (num.Length >= 7 && IsAllDigits(num)) set.Add(Norm(num));
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
                        if (num.Length >= 7 && IsAllDigits(num)) set.Add(Norm(num));
                    }
                }
            }
            int both = 0, onlyLocal = 0, onlyFile = 0;
            foreach (var n in xList) if (set.Contains(Norm(n))) both++;
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
            // 去重按国际归一回并(同号不同写法算同一条), 保留最先出现的写法
            var byNorm = new Dictionary<string, string>();
            foreach (var n in xList) { string k = Norm(n); if (!byNorm.ContainsKey(k)) byNorm[k] = n; }
            var sorted = byNorm.Values.OrderBy(n => n).ToList();
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("目标区报告 — {0:n0} 条号码 (去重后)", sorted.Count));
            sb.AppendLine(new string('-', 40));

            // 号段分布
            var seg = new Dictionary<string, int>();
            foreach (var n in sorted)
            {
                string nn = Norm(n);
                string key = nn.Length >= 7 ? nn.Substring(0, 7) : nn;
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

        void ExportList(List<string> list, string desc, string timestamp)
        {
            var dlg = new SaveFileDialog { Title = desc, Filter = "文本文件|*.txt",
                FileName = desc.Replace(" ", "_") + "_" + list.Count.ToString() + "个_" + timestamp };
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
            var list = xList.ToList();
            if (list.Count == 0) { MessageBox.Show("目标区为空", "提示"); return; }
            ExportList(list, "导出全部", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        }

        // 分批导出: 每批数量可自定义(默认10万, 可输入任意正整数)
        void OnExportBatch(object s, EventArgs e)
        {
            var list = xList.ToList();
            if (list.Count == 0) { MessageBox.Show("目标区为空", "提示"); return; }
            var p = new Form { Text = "分批导出 - 每批数量", Size = new Size(330, 175), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            p.Controls.Add(new Label { Text = "输入每批要导出的数量(正整数):", Left = 16, Top = 18, AutoSize = true });
            var tb = new TextBox { Left = 16, Top = 44, Width = 280, Text = "100000" };
            p.Controls.Add(tb);
            var tip = new Label { Text = "如总数 50 万、每批 5 万 → 得到 10 个文件", Left = 16, Top = 76, AutoSize = true, ForeColor = Color.Gray, Font = new Font("微软雅黑", 8) };
            p.Controls.Add(tip);
            var bOk = new Button { Text = "确定", Left = 220, Top = 110, Width = 76 };
            int per = 100000;
            bOk.Click += (s2, e2) =>
            {
                if (!int.TryParse(tb.Text.Trim(), out per) || per <= 0) { MessageBox.Show("请输入正整数", "提示"); return; }
                p.DialogResult = DialogResult.OK;
                p.Close();
            };
            p.Controls.Add(bOk);
            p.Controls.Add(new Button { Text = "取消", Left = 140, Top = 110, Width = 70, DialogResult = DialogResult.Cancel });
            if (p.ShowDialog() != DialogResult.OK) return;

            var dlg = new FolderBrowserDialog { Description = "选择导出目录" };
            if (!string.IsNullOrEmpty(outPath)) dlg.SelectedPath = outPath;
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                int batch = 1;
                for (int i = 0; i < list.Count; i += per)
                {
                    int cnt = Math.Min(per, list.Count - i);
                    string fn = Path.Combine(dlg.SelectedPath,
                        string.Format("分批_{0}_第{1}批_{2}个_{3}.txt", DateTime.Now.ToString("yyyyMMddHHmmss"),
                            batch, cnt, ts));
                    File.WriteAllLines(fn, list.GetRange(i, cnt), Encoding.UTF8);
                    batch++;
                }
                outPath = dlg.SelectedPath;
                IniWrite("OutPath", outPath);
                MessageBox.Show(string.Format("分批导出完成: 每批 {0:n0} 条, 共 {1:n0} 条, {2} 个文件",
                    per, list.Count, batch - 1), "完成");
            }
            catch (Exception ex) { MessageBox.Show("导出失败: " + ex.Message, "错误"); }
        }

        // 按需导出: 自定义 开始行..结束行 (行号从1开始)
        void OnExportRange(object s, EventArgs e)
        {
            var list = xList.ToList();
            if (list.Count == 0) { MessageBox.Show("目标区为空", "提示"); return; }
            var p = new Form { Text = "按需导出 - 选择行区间", Size = new Size(340, 200), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            p.Controls.Add(new Label { Text = string.Format("目标区共 {0:n0} 条(行号从 1 开始)", list.Count), Left = 16, Top = 16, AutoSize = true, Font = new Font("微软雅黑", 9) });
            p.Controls.Add(new Label { Text = "开始行:", Left = 16, Top = 50, AutoSize = true });
            var tStart = new TextBox { Text = "1", Left = 90, Top = 48, Width = 220 };
            p.Controls.Add(tStart);
            p.Controls.Add(new Label { Text = "结束行:", Left = 16, Top = 80, AutoSize = true });
            var tEnd = new TextBox { Text = list.Count.ToString(), Left = 90, Top = 78, Width = 220 };
            p.Controls.Add(tEnd);
            var bOk = new Button { Text = "导出", Left = 232, Top = 130, Width = 80 };
            bOk.Click += (s2, e2) =>
            {
                int start, end;
                if (!int.TryParse(tStart.Text.Trim(), out start) || !int.TryParse(tEnd.Text.Trim(), out end)
                    || start < 1 || end < start || end > list.Count)
                {
                    MessageBox.Show(string.Format("区间无效: 需 1 <= 开始行 <= 结束行 <= {0}", list.Count), "提示");
                    return;
                }
                var sub = new List<string>();
                for (int i = start - 1; i < end && i < list.Count; i++) sub.Add(list[i]);
                p.DialogResult = DialogResult.OK;
                p.Close();
                ExportList(sub, string.Format("按需导出_{0}至{1}行", start, end),
                    DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            };
            p.Controls.Add(bOk);
            p.Controls.Add(new Button { Text = "取消", Left = 148, Top = 130, Width = 70, DialogResult = DialogResult.Cancel });
            p.ShowDialog();
        }

        // ── 帮助 ──
        void OnHelp(object sender, EventArgs e)
        {
            string msg = @"龙哥数据_筛选 v4.3
【基本操作】
• 导入文件：支持 txt/csv (UTF-8/GBK)，只收纯数字≥7位，自动去重
• 导入号码：从剪贴板粘贴
• 输入号段：前缀+区间批量生成；【数量】可填，填N=从区间随机抽N条不重复

【核心功能】
• 对比去重：对比文件 − 原始区，命中(原始区已有)移除，未重复进目标区
• 排序↑↓/乱序：按号码排序或随机打乱
• 去重：按国际归一(同号不同写法算重复)删除
• 清除非号：只保留纯数字>=7位

【判定口径(国际为主)】
• 去重/对比/报告 均按国际归一口径：+86138../0086138../138.. 视为同一号
• 按类型-国际号 = 非中国号码 且(00开头 或 含区号≥11位)；中国大陆=86/11位国内号
• 按特征 = 自定义最小/最大位数(可留空不限)；≤10/11-12/≥13 为快捷档

【窗格说明】
• 默认 8 窗格(每窗 20 万=160 万/区)，可横向滚动查看
• 超 160 万自动增窗；上限 1000 万，可选显示超上限
• 双击窗格内号码可移入/移出

【移动操作】
• 8 个方向按钮：全部/按值/按特征/按类型 上下移动
• 按值(号码/区号/子串多行)/按特征(位数)/按类型 均弹窗选择后再移动

【导出】
• 导出全部 / 分批导出(每批数量可自填) / 按需导出(选开始到结束行)
• 报告：号段分布 TOP20 + 样本(国际归一口径)

【快捷键】Delete 删除选中
";
            MessageBox.Show(msg, "帮助 - 龙哥数据_筛选");
        }
    }
}