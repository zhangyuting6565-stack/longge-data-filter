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
            bIn = Btn("号码生成", bx, upBtnY, bw, bh2, OnInput);
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

        // ── 号码生成：按国家+数量 ──
        void OnInput(object sender, EventArgs e)
        {
            var f = new Form { Text = "号码生成", ClientSize = new Size(470, 270), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            f.BackColor = SystemColors.Control;
            try { f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            f.Controls.Add(new Label { Text = "国家:", Left = 20, Top = 20, AutoSize = true, Font = new Font("微软雅黑", 9, FontStyle.Bold) });
            var cbCountry = new ComboBox { Left = 80, Top = 18, Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
            cbCountry.Items.AddRange(new object[] { "巴西(已验证)", "美国(已验证)", "智利(已验证)", "墨西哥(已验证)", "菲律宾", "越南", "印度" });
            cbCountry.SelectedIndex = 0;
            f.Controls.Add(cbCountry);
            f.Controls.Add(new Label { Text = "数量:", Left = 280, Top = 20, AutoSize = true, Font = new Font("微软雅黑", 9, FontStyle.Bold) });
            var tbCount = new TextBox { Left = 320, Top = 17, Width = 120 };
            f.Controls.Add(tbCount);
            f.Controls.Add(new Label { Text = "配置:", Left = 20, Top = 46, AutoSize = true, Font = new Font("微软雅黑", 9, FontStyle.Bold) });
            var cbCfg = new ComboBox { Left = 80, Top = 44, Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
            cbCfg.Items.AddRange(new object[] { "最优(量大+命中平衡,默认)", "最大量(量大优先,忽略命中)" });
            cbCfg.SelectedIndex = 0;
            f.Controls.Add(cbCfg);
            f.Controls.Add(new Label { Text = "选国家+填数量+选配置直接生成，自动去重加到原始区。", Left = 20, Top = 84, AutoSize = true, ForeColor = Color.Gray, Font = new Font("微软雅黑", 8) });
            f.Controls.Add(new Label { Text = "巴西/美国=已验证号段方案(命中率约60~70%)；其余国家为基础", Left = 20, Top = 104, AutoSize = true, ForeColor = Color.Gray, Font = new Font("微软雅黑", 8) });
            f.Controls.Add(new Label { Text = "规则，可直接测试，随后可逐步回填优化命中。巴西默认13位可切12位/随机混合。", Left = 20, Top = 124, AutoSize = true, ForeColor = Color.Gray, Font = new Font("微软雅黑", 8) });
            var lbBrFmt = new Label { Text = "巴西格式:", Left = 20, Top = 148, AutoSize = true, Font = new Font("微软雅黑", 8) };
            var cbBrFmt = new ComboBox { Left = 90, Top = 146, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            cbBrFmt.Items.AddRange(new object[] { "13位(默认)", "12位", "随机混合" });
            cbBrFmt.SelectedIndex = 0;
            f.Controls.Add(lbBrFmt);
            f.Controls.Add(cbBrFmt);
            cbCountry.SelectedIndexChanged += (csC, csE) => {
                bool isBr = (cbCountry.SelectedIndex == 0);
                lbBrFmt.Visible = isBr;
                cbBrFmt.Visible = isBr;
            };

            var bOk = new Button { Text = "生成", Left = 340, Top = 200, Width = 90 };
            bOk.Click += (s2, e2) => GenerateNumbers(f, cbCountry.SelectedIndex, tbCount, cbBrFmt.SelectedIndex, cbCfg.SelectedIndex);
            f.Controls.Add(bOk);
            var bCancel = new Button { Text = "取消", Left = 240, Top = 200, Width = 90 };
            bCancel.Click += (cS, cE) => f.Close();
            f.Controls.Add(bCancel);
            f.ShowDialog();
        }

        void GenerateNumbers(Form f, int countryIdx, TextBox tbCount, int brFmt, int cfg)
        {
            long want = 0;
            if (!long.TryParse(tbCount.Text.Trim(), out want) || want <= 0)
            { MessageBox.Show("数量必须为大于0的数字", "错误"); return; }
            if (want > 50000000L) { MessageBox.Show("一次最多5千万条", "警告"); return; }

            Enabled = false;
            var rnd = new Random();
            int n = (int)Math.Min(want, 50000000L);
            var newList = new List<string>();
            var pool = new HashSet<string>();
            long guard = 0;
            long maxGuard = Math.Max(100000L, (long)n * 200L);

            // ── 巴西: V11高活跃段(9段,命中率66-71%) 13位=55+DDD+"9"+seg后2+随机6;12位去9;随机混合 ──
string[] brSeg = new string[]{
"2799","6799","9198","1999","1198","4199","1499","9898","1699","5199",
"2197","1196","6199","2199","6299","3199","2198","1194","1197","1195",
"4799","9299","1599","6599","1199","6699","1799","2196","2299","1899",
"6999","3899","8199","8599","9199","9998","3499","4299","1998","4499",
"3198","7599","8899","8499","9399","1299","9499","8399","6399","4399",
"1191","2499","1399","7399","3599","8699","9298","7799","5599","5198",
"6499","3399","4899","4599","8198","5499","7199","7999","8299","4999",
"7198","6899","8598","1193","7598","6298","6198","1298","8799","7499",
"4198","8398","7398","3299","3197","2899","9899","3799","9999","4699",
"6798","9699","8898","9498","2798","1698","9798","8498","9698","4298",
"8298","8798","4798","7798","1798","5399","8999","8698","3398","2298",
"7498","1398","9398","9599","6698","4398","3298","4898","2498","3598",
"1498","8998","1997","6598","4498","7998","3498","9391","4998","3898",
"6398","9598","6998","1598","9984","9885","6392","4598","1898","6498",
"9884","9185","6992","9491","9492","8197","6993","9897","6696","9192",
"9193","6692","6796","8592","9191","9691","9184","9784","6592","9991",
"9291","9392","4796","9292","6791","9293","4196","6492","9294","5398",
"5498","9284","6892","1192","4396","9591","5598","4797","6596","9881",
"6792","8694","9981","3798","6293","8781","8892","4195","9484","8695",
"9181","9985","9182","9891","2195","8981","8994","1397","6291","4792",
"4896","4197","6292","8496","7381","6192","6281","6294","8591","6391",
"9684","8791","9799","4791","6191","9285","5195","6593","8896","9888",
"5596","6196","5496","8396","5197","3195","4497","8881","9186","5196",
"7591","6193","6781","6195","6181","3171","3597","8296","3491","8491",
"3591","8393","4698","4991","7996","9180","6296","3496","3196","9384",
"8596","8888","4788","7581","8391","8189","6793","5181","7781","8193",
"7192","4288","9887","3192","3497","6493","6898","8192","7791","8494",
"8681","6681","9892","7481","8194","8291","7582","3191","7391","8688",
"7196","9584","4891","8191","9281","6684","8796","9870","5193","5192",
"4184","7588","8894","9481","5591","3182","8388","8594","3193","9381",
"7388","8293","1297","5180","7488","3897","6282","4187","6194","9286",
"7191","8196","4491","3891","9188","7592","8597","4188","4391","4192",
"7583","6984","8288","9681","8893","3183","5491","6384","4888","4988",
"6295","8492","8387","4384","9295","6581","7183","8386","7182","5191",
"4784","7788","7382","5189","8587","8585","3185","3172","8488","8788",
"4388","3187","9992","8588","3188","8487","3184","3384","5391","7181",
"6496","7193","5194","7184","8586","6182","8185","8281","6481","8897",
"6285","8381","3186","8589","4191","3189","6284","8195","9187","7491",
"7187","6584","9781","3284","4488","8188","8186","3484","7186","8187",
"3388","8581","3173","5185","7188","3584","3488","8184","6697","8287"
};

int[] brW = new int[]{
50528,43120,41107,40660,39297,38153,37540,37538,36157,36146,36119,34978,34530,34339,34090,33132,32680,32368,32330,32330,
31179,30925,29766,28444,27877,27297,25366,25300,25287,25144,24392,24155,24097,23837,23716,23667,22416,22283,22015,21978,
21683,21238,21131,20841,20619,20352,19785,19350,19345,19249,18887,18757,18563,18355,18334,18235,18201,18132,17700,17547,
17341,17186,16982,16773,16557,16544,16442,16332,16305,16245,15758,15341,15213,14431,14338,14316,13387,13379,12925,12807,
12622,11786,11761,11387,10980,10930,10440,9861,9698,9302,9297,8924,8864,8533,8297,8141,8001,7936,7880,7738,
7736,7659,7568,7550,7473,7318,7177,6937,6867,6681,6545,6539,6443,6210,6017,5728,5722,5391,5375,5371,
5338,5274,5267,5222,5095,5060,5059,4988,4933,4886,4473,4455,4337,3929,3880,3798,3796,3636,3631,3628,
3573,3339,3283,3277,3264,3262,3193,3080,2884,2865,2805,2732,2720,2688,2569,2565,2524,2478,2467,2436,
2427,2403,2391,2358,2348,2324,2298,2258,2254,2251,2249,2225,2219,2151,2143,2104,2099,2090,2073,2072,
2062,2013,2005,1965,1916,1916,1889,1889,1859,1837,1837,1831,1826,1822,1793,1790,1780,1772,1750,1746,
1745,1744,1739,1731,1721,1698,1681,1677,1675,1672,1655,1649,1638,1624,1620,1617,1609,1593,1590,1590,
1574,1566,1565,1528,1527,1503,1499,1494,1492,1483,1480,1470,1465,1447,1443,1429,1426,1416,1405,1398,
1366,1355,1337,1319,1319,1317,1314,1300,1298,1281,1270,1250,1230,1214,1211,1208,1188,1183,1181,1177,
1156,1153,1149,1146,1144,1144,1141,1127,1124,1118,1108,1103,1100,1094,1093,1090,1069,1068,1068,1065,
1064,1064,1064,1063,1063,1062,1057,1057,1053,1046,1044,1035,1034,1029,1025,1023,1019,1013,1002,1001,
999,997,996,996,994,989,986,986,981,976,974,970,959,947,941,934,932,927,927,924,
924,908,896,895,893,881,877,869,868,863,860,858,858,857,856,856,851,847,846,835,
831,828,824,816,802,800,794,792,788,788,781,780,774,774,769,769,764,754,752,747,
747,743,742,740,739,736,735,733,731,730,727,725,724,722,721,721,705,699,690,689,
689,684,682,678,675,668,659,656,656,656,655,653,653,652,650,642,642,638,633,631
};

if (countryIdx == 0)
            {
                // ── 巴西: 权重抽段, 按brFmt输出格式 (0=13位默认/1=12位/2=随机混合) ──
                while (pool.Count < n && guard < maxGuard)
                {
                    guard++;
                    int si = PickWeighted(rnd, brW, (cfg == 0) ? 100 : brW.Length);
                    string ddd = brSeg[si].Substring(0, 2);   // 前2位 = 城市区号
                    string xy = brSeg[si].Substring(2, 2);    // 后2位 = 号段
                    string tail = rnd.Next(100000, 999999).ToString("D6");
                    string num;
                    if (brFmt == 1) num = "55" + ddd + xy + tail;              // 12位(去"9")
                    else if (brFmt == 2) num = (rnd.Next(2) == 0)
                        ? "55" + ddd + "9" + xy + tail                          // 混合: 13位
                        : "55" + ddd + xy + tail;                              // 混合: 12位
                    else num = "55" + ddd + "9" + xy + tail;                  // 13位默认
                    if (pool.Add(num)) newList.Add(num);
                }
            }
            else if (countryIdx == 1)
            {
                // 美国: V7已验证高准段(段命中率>=0.65, 97段) = "1"+区号+交换码+4位=11位
                string[] usSeg = new string[]{
"419364","305518","404854","347777","310666","630658","818818","305305","917500","305988",
"916225","347977","818913","347444","786399","347300","305244","347666","717821","305300",
"305316","347447","305497","305896","786357","347885","347500","818300","305833","305915",
"305333","305927","786720","305924","305905","786486","305399","215984","818800","310980",
"954394","916595","305733","786366","786616","347543","347400","786443","208444","305336",
"786395","818858","818747","786344","305934","305343","347634","786447","305713","786380",
"215935","310359","305778","786683","305450","954709","786370","240423","718300","305766",
"310866","786333","240481","305799","541417","240505","786355","786553","347755","786306",
"786663","305609","619764","305878","786444","818915","305632","786340","305922","305303",
"305798","305505","786554","571276","305608","305904","503380","786223","305801","305965",
"305310","718200","310430","786426","347873","786560","305775","213500","305409","305877",
"646474","347981","347681","818445","917400","347737","757792","347552","305542","347600",
"786277","561305","305206","305776","818919","347567","786343","786973","786286","305496",
"305763","786499","786281","786448","305803","503481","786317","347536","786873","305926",
"305773","818943","347283","305588","305903","786768","786547","786488","786564","786597",
"305339","786260","786556","954274","305318","562563","347279","786252","347278","718427",
"786678","305332","786970","407334","818987","305331","305781","313612","305527","347659",
"347791","347481","347210","718314","347653","954297","305972","760395","305790","786925",
"786325","407860","929327","310993","347553","305879","949394","305613","305345","503267",
"415694","786608","786587","347357","305335","310880","818966","407535","818331","305834",
"305989","212470","929899","305484","718844","786909","786521","786241","347988","305979",
"310498","305951","786603","786718","786451","310779","305898","786328","305302","347720",
"310869","786326","305725","305815","718288","786200","786626","310990","917971","305794",
"310770","786285","929245","646466","786942","786262","646377","346507","786290","786381",
"813942","347355","347608","305796","360804","347337","929422","929969","786826","714930",
"347605","929629","201873","347636","347909","747306","407485","786389","347925","310801",
"201993","407300","917600","347499","929909","305975","551358","786307","305321","929888",
"916743","786543","619277","786712","786760","786234","305890","786303","305793","917975",
"305338","305469","305562","786302","347884","347837","786253","718496","305986","718207",
"347898","310210","949295","201936","786247","718864","786797","718490","347691","310871",
"443592","347707","305842","305586","786222","929355","954638","347922","786609","786715",
"305606","347476","786546","954822","646244","786246","818744","786992","551556","305431",
"347615","786282","347972","818388","929217","954536","347781","786498","786201","954470",
"786203","754366","917588","954865","305746","424977","929363","240476","310593","503317",
"415999","347863","201737","305498","786660","305301","305812","954993","786771","305308",
"646541","949400","786346","646644","818434","305772","561929","786301","305720","347740",
"310926","786266","929471","305495","305494","347319","786620","917744","813770","908764",
"786630","818585","786803","786402","347888","310717","305299","407437","818397","347701",
"718415","321746","310890","646703","323304","949910","202460","347301","310600","347445",
"929393","786537","786312","954330","917755","734290","786356","857237","310849","954937",
"305970","786400","305216","786316","786449","215250","954997","818669","305804","347303",
"786859","786424","305510","305910","786740","206671","201724","201736","305747","754244",
"305342","347217","786352","818640","786354","786569","305491","818288","305992","347259",
"202413","786930","305748","347656","305219","347968","786710","786599","786817","786327",
"786403","347530","415737","310994","347459","347393","305354","305546","949310","718600",
"571275","917292","646363","818636","407486","786458","786308","818284","954600","305587",
"305984","310467","617230","305582","917340","347513","949690","305761","503358","206446",
"917774","347320","347575","407285","305457","818571","213300","347517","347593","646515",
"786238","347969","818720","786368","305334","818448","786256","786612","917615","786985",
"954479","347992","503422","347613","347612","407818","949228","646479","347784","786877",
"706982","954701","818522","347933","954305","786879","717926","347596","347822","786523",
"347806","310922","423654","407873","917754","305467","510383","321440","786202","347249",
"917530","954696","908422","321946","304717","619714","305439","347479","305490","571277",
"929233","786239","347260","305753","571278","646645","786487","917940","347420","954588",
"503381","929444","786479","347951","305298","347336","407729","347833","305785","561541",
"310435","201920","786351","786210","818355","786525","786557","202390","347458","347264",
"347216","202361","786602","503332","347698","747257","407973","916420","786867","347261",
"786208","305607","754246","347322","407272","305322","646399","786853","786606","267423",
"818404","818606","706978","305458","818339","917603","305930","786818","240421","646420",
"305323","908906","408505","305297","786473","773865","818281","929331","650283","786227",
"718877","973444","786956","786754","305987","818726","818415","954673","786614","917771",
"310739","646436","503449","540475","954608","347824","347935","818515","206412","347484",
"310927","786237","323620","407520","347938","718916","310920","786280","818209","407922",
"917355","754234","917757","949350","801205","917854","347339","786339","954663","718790",
"786296","503309","908404","202468","347583","786484","786863","347845","786506","732763",
"310614","916477","818357","425350","954907","954864","954559","754422","973563","347930",
"786694","954225","786445","786856","305850","347858","929340","773332","917403","786857",
"949302","571274","425737","818213","718755","347488","646431","786617","978885","954793",
"754232","347549","818620","929301","561674","305205","908405","310709","786975","305783",
"310497","786716","424333","201878","754423","305742","619723","786390","786532","407953",
"954629","857258","503330","754610","786764","917833","954655","305282","617669","786419",
"310721","612707","347330","347369","310936","718915","718801","786832","347680","305684",
"301675","917930","949351","347299","201952","408306","360909","954444","201647","916410",
"732688","818414","646577","561577","857247","213309","929253","347358","305788","561201",
"973666","253886","347255","347206","818292","949331","786641","518739","612226","786538",
"347403","347272","240615","646270","201640","818731","786868","305528","954552","786805",
"916969","248315","201951","310699","407968","206372","929461","571244","323350","917916",
"786830","201598","786212","929216","407800","917859","310963","305213","818749","202569",
"561255","916220","917497","917520","503313","786365","917770","347304","786299","773766",
"754971","407433","305215","201362","786295","818395","347323","818822","323899","973336",
"949466","305610","310592","954708","347247","240899","718757","503209","347866","503421",
"407692","646409","407408","773807","917972","347761","917543","929527","818633","267333",
"917957","310962","425773","202352","301728","857615","857251","818321","949413","973931",
"786493","818335","347961","954554","786709","617319","407844","415425","571332","305849",
"786512","786816","253632","305710","917544","818468","646730","786769","917747","818984",
"305978","732762","949981","786468","202415","305726","217900","561460","347744","646331",
"917442","917436","253334","954245","917348","347251","818274","818770","707626","646623",
"347446","786838","407733","917815","347302","732397","646322","646353","916507","747272",
"786376","347421","774386","561480","949300","857261","347208","347891","407580","253335",
"818324","845659","917626","305525","917499","305764","408507","786674","818422","718500",
"407715","818310","239200","917515","347515","415424","818272","347733","347257","305502",
"949232","907299","786287","917702","732910","646643","818653","832434","213509","917855",
"786379","732900","862291","347610","646247","347276","305780","408646","347912","347285",
"718909","818917","508350","561294","305522","408431","813900","646339","908590","201238",
"407807","347654","917331","954549","786367","202910","718710","818679","949500","347440",
"347622","312493","786531","206422","917370","440379","917822","360931","201925","347585",
"818220","929320","917669","858405","201888","347258","917533","786229","954226","786757",
"786397","201889","305619","347335","646853","646508","310463","954558","347975","310488",
"954854","818795","203550","786387","646573","916412","954471","240413","917514","917545",
"929404","321310","786461","718213","818481","786334","267205","347856","786304","929330"
};

                int[] usW = new int[]{
1120,715,647,626,496,489,469,466,464,448,446,445,435,430,427,423,419,417,415,413,
413,412,407,407,400,398,397,397,397,395,393,392,392,391,390,389,388,388,387,386,
386,384,384,382,382,381,381,375,375,375,373,373,372,371,370,368,367,367,366,366,
365,363,363,362,362,362,361,360,360,360,360,360,358,358,358,358,357,356,355,355,
355,355,354,354,354,354,353,352,352,351,351,350,350,350,350,350,350,350,348,348,
347,347,344,344,343,343,343,342,342,342,342,341,341,340,340,340,339,339,339,338,
338,338,338,338,338,337,337,337,337,336,336,336,336,335,335,335,334,334,334,334,
333,333,333,332,332,332,332,332,332,332,331,331,331,331,331,330,330,330,330,330,
329,329,329,328,328,328,328,328,327,327,327,327,326,326,326,326,326,325,325,325,
325,325,324,324,324,324,323,323,323,323,323,323,323,323,323,323,322,322,321,321,
321,321,321,321,321,321,320,320,320,320,320,320,319,319,319,319,319,319,319,319,
319,318,318,318,318,318,318,318,317,317,317,317,316,316,316,316,316,316,316,315,
315,315,315,315,315,315,314,314,314,314,313,313,313,313,312,312,312,312,312,311,
311,311,311,310,310,310,310,310,310,310,310,310,309,309,309,309,309,309,309,309,
309,308,308,308,308,307,307,307,307,307,307,307,307,307,306,306,306,306,306,305,
305,305,305,305,305,304,304,304,304,304,303,303,303,303,303,302,302,302,302,302,
302,302,302,302,302,301,301,301,301,301,301,301,301,301,300,300,300,300,300,300,
300,300,300,300,300,299,299,299,299,299,299,298,298,298,298,297,297,297,297,297,
297,297,297,297,297,297,296,296,296,296,296,296,296,296,296,296,296,295,295,295,
295,295,295,295,295,295,295,295,295,295,295,295,295,294,294,294,294,294,294,294,
294,294,294,293,293,293,293,292,292,292,292,292,292,292,291,291,291,291,291,291,
291,291,291,290,290,290,290,290,290,290,290,289,289,289,289,289,289,289,289,289,
289,289,289,289,288,288,288,288,288,288,288,288,288,288,288,288,288,288,288,288,
287,287,287,287,287,287,287,287,286,286,286,286,286,286,286,286,286,286,286,286,
286,285,285,285,285,285,285,285,285,285,285,285,284,284,284,284,284,284,284,283,
283,283,283,283,283,283,283,283,283,283,283,283,283,283,283,283,283,283,283,282,
282,282,282,282,282,282,282,282,282,281,281,281,281,281,281,281,281,280,280,280,
280,280,280,279,279,279,279,279,279,279,279,279,278,278,278,278,278,278,278,278,
278,278,278,278,277,277,277,277,277,277,277,277,277,277,277,276,276,276,276,276,
276,276,276,276,276,276,275,275,275,275,275,275,275,275,275,275,275,275,275,275,
275,274,274,274,274,274,274,274,274,274,274,274,274,274,273,273,273,273,273,273,
273,273,273,273,273,272,272,272,272,272,272,272,272,272,272,272,271,271,271,271,
271,271,271,271,270,270,270,270,270,270,270,270,270,270,270,269,269,269,269,269,
269,269,269,269,269,269,269,268,268,268,268,268,268,268,268,268,268,268,268,268,
267,267,267,267,267,267,267,267,267,267,267,267,267,267,266,266,266,266,266,266,
266,266,266,266,266,266,266,266,266,266,265,265,265,265,265,265,264,264,264,264,
264,264,264,264,264,264,264,264,264,264,264,264,264,263,263,263,263,263,263,263,
263,263,263,263,262,262,262,262,262,262,262,262,262,262,262,262,262,262,262,262,
262,261,261,261,261,261,261,261,261,261,261,261,261,261,261,260,260,260,260,260,
260,260,260,260,260,260,260,260,260,260,260,260,260,259,259,259,259,259,259,259,
259,259,259,259,259,259,259,259,259,258,258,258,258,258,258,258,258,258,258,258,
258,258,258,258,258,258,258,258,258,258,258,258,257,257,257,257,257,257,257,257,
257,257,257,257,257,257,257,257,257,257,257,257,257,257,257,257,256,256,256,256,
256,256,256,256,256,256,256,256,256,256,256,256,256,256,255,255,255,255,255,255,
255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,254,254,
254,254,254,254,254,254,254,254,254,254,254,253,253,253,253,253,253,253,253,253,
253,253,253,253,253,253,253,253,253,253,253,252,252,252,252,252,252,252,252,252,
252,252,252,252,252,252,251,251,251,251,251,251,251,251,251,251,251,251,251,251,
251,251,251,251,251,251,250,250,250,250,250,250,250,250,250,250,250,250,249,249,
249,249,249,249,249,249,249,249,249,249,249,249,249,249,248,248,248,248,248,248
};

                while (pool.Count < n && guard < maxGuard)
                {
                    guard++;
                    int ui = PickWeighted(rnd, usW, (cfg == 0) ? 500 : usW.Length);
                    string sub = rnd.Next(0, 9999).ToString("D4");
                    string num = "1" + usSeg[ui] + sub;
                    if (pool.Add(num)) newList.Add(num);
                }
            }
            else if (countryIdx == 2)
            {
                // ── 智利(已验证): 56 + 9 + 4位段 + 4位  (V7实测26段/56%) ──
                string[] clSeg = new string[]{
"9982","9987","9991","9981","9766","9770","9403","9983","9764","9984",
"9765","9950","9954","9420","9949","9501","9951","9769","9620","9988",
"9986","9621","9357","9358","9989","9424","9624","9405","9994","9500",
"9971","9622","9366","9421","9423","9408","9768","9302","9941","9425",
"9404","9367","9767","9428","9361","9623","9363","9948","9368","9922",
"9369","9961","9426","9364","9365","9414","9980","9626","9353","9969",
"9362","9340","9947","9943","9953","9944","9429","9993","9427","9959",
"9418","9957","9964","9356","9967","9400","9352","9354","9844","9544",
"9307","9305","9985","9303","9416","9509","9351","9310","9311","9350",
"9308","9342","9540","9334","9407","9785","9329","9313","9309","9413",
"9415","9822","9321","9417","9320","9977","9955","9301","9312","9979",
"9625","9333","9330","9921","9923","9541","9341","9779","9739","9300",
"9306","9612","9402","9331","9777","9317","9930","9996","9332","9627",
"9813","9338","9401","9883","9815","9359","9888","9628","9826","9542",
"9570","9721","9823","9343","9774","9824","9843","9932","9890","9322",
"9327","9595","9776","9318","9453","9339","9657","9939","9909","9355",
"9335","9672","9877","9751","9738","9629","9572","9565","9846","9825",
"9881","9880","9828","9797","9840","9842","9974","9819","9931","9314",
"9775","9782","9900","9929","9845","9411","9912","9337","9452","9662",
"9422","9571","9733","9543","9521","9760","9787","9324","9875","9522",
"9508","9560","9789","9667","9520","9740","9325","9850","9664","9455",
"9796","9882","9580","9968","9784","9780","9669","9590","9663","9519",
"9614","9562","9658","9537","9326","9665","9849","9457","9497","9710",
"9561","9668","9820","9758","9794","9821","9456","9913","9749","9978",
"9661","9906","9486","9566","9874","9788","9973","9757","9907","9752",
"9545","9829","9818","9652","9917","9412","9660","9690","9783","9328",
"9670","9563","9683","9841","9873","9577","9736","9847","9504","9594",
"9650","9448","9908","9450","9459","9651","9827","9747","9634","9640",
"9656","9551","9655","9619","9755","9323","9506","9642","9319","9458",
"9575","9892","9781","9963","9552","9499","9720","9945","9578","9719",
"9876","9786","9792","9935","9666","9574","9573","9576","9872","9798",
"9523","9745","9643","9933","9659","9684","9814","9790","9502","9791",
"9915","9440","9862","9990","9870","9653","9507","9718","9970","9498",
"9737","9485","9889","9488","9454","9564","9491","9729","9920","9866",
"9645","9671","9505","9771","9753","9490","9410","9569","9778","9674",
"9756","9673","9677","9748","9860","9503","9934","9610","9592","9799",
"9795","9743","9492","9641","9568","9924","9553","9531","9717","9585",
"9546","9730","9754","9484","9487","9635","9722","9549","9864","9451",
"9958","9496","9633","9834","9762","9593","9489","9999","9995","9725",
"9598","9644","9734","9547","9533","9649","9727","9494","9867","9597",
"9925","9865","9724","9630","9927","9898","9446","9532","9444","9615",
"9940","9443","9848","9675","9447","9534","9409","9726","9584","9442",
"9587","9905","9493","9952","9812","9744","9613","9731","9838","9539",
"9639","9596","9654","9732","9406","9548","9861","9589","9992","9582",
"9965","9723","9956","9713","9946","9579","9910","9942","9871","9759",
"9735","9646","9495","9449","9901","9926","9857","9761","9616","9868",
"9793","9728","9481","9869","9567","9591","9611","9617","9886","9863",
"9583","9916","9599","9586","9851","9836","9480","9852","9648","9618",
"9998","9631","9742","9746","9833","9899","9530","9714","9884","9588",
"9445","9966","9467","9441","9535","9832","9773","9687","9462","9854"
};

                int[] clW = new int[]{
7679,7352,7191,6752,6581,6551,6516,6508,6401,6326,6316,6246,6085,6058,5951,5822,5815,5743,5727,5702,
5675,5644,5610,5515,5511,5499,5475,5456,5443,5385,5360,5319,5308,5301,5250,5249,5235,5228,5218,5211,
5207,5202,5191,5178,5173,5172,5149,5127,5118,5096,5092,5091,5087,5083,5075,5041,5024,4990,4980,4958,
4937,4910,4901,4881,4862,4843,4828,4815,4815,4733,4702,4691,4657,4640,4637,4601,4596,4586,4577,4577,
4540,4535,4534,4532,4532,4527,4526,4508,4499,4472,4452,4424,4415,4412,4381,4350,4349,4336,4304,4291,
4285,4283,4253,4242,4237,4232,4205,4203,4199,4191,4174,4168,4164,4164,4160,4158,4130,4127,4124,4119,
4115,4108,4096,4095,4083,4072,4055,4052,4050,4049,4040,4035,4026,4025,4008,3976,3976,3969,3954,3946,
3946,3945,3935,3910,3908,3906,3903,3901,3900,3898,3895,3893,3880,3875,3874,3870,3863,3857,3854,3852,
3851,3847,3833,3830,3826,3823,3815,3809,3805,3798,3791,3787,3786,3786,3772,3767,3766,3763,3756,3755,
3753,3745,3743,3743,3737,3726,3719,3715,3714,3713,3710,3709,3708,3707,3701,3694,3692,3687,3678,3674,
3658,3657,3643,3640,3633,3631,3628,3617,3616,3615,3611,3611,3604,3603,3589,3583,3582,3582,3581,3567,
3560,3550,3550,3548,3548,3541,3541,3540,3537,3537,3535,3533,3532,3531,3529,3528,3527,3526,3519,3505,
3505,3503,3481,3478,3474,3473,3464,3461,3460,3457,3457,3454,3454,3453,3451,3444,3442,3441,3438,3434,
3433,3429,3410,3410,3410,3408,3405,3402,3398,3394,3391,3390,3389,3387,3384,3383,3381,3376,3358,3354,
3352,3349,3346,3345,3338,3330,3326,3325,3323,3320,3315,3313,3303,3296,3296,3294,3293,3288,3285,3270,
3265,3264,3264,3262,3260,3260,3259,3257,3254,3254,3251,3249,3247,3247,3238,3234,3221,3217,3215,3210,
3202,3200,3200,3198,3198,3196,3191,3191,3188,3188,3188,3187,3185,3183,3178,3177,3177,3176,3170,3169,
3169,3164,3163,3153,3153,3151,3146,3145,3137,3136,3132,3125,3124,3111,3111,3110,3105,3104,3098,3094,
3091,3089,3084,3081,3073,3072,3065,3061,3055,3051,3045,3044,3040,3039,3037,3033,3033,3030,3022,3016,
3016,3012,3011,3011,3005,3002,3001,2988,2984,2981,2980,2975,2974,2971,2970,2965,2958,2954,2949,2948,
2948,2946,2942,2941,2940,2934,2910,2909,2907,2906,2900,2893,2890,2888,2869,2866,2866,2866,2855,2852,
2845,2844,2835,2835,2832,2820,2820,2819,2818,2812,2812,2809,2808,2796,2790,2787,2784,2781,2776,2775,
2775,2773,2768,2766,2747,2743,2734,2709,2706,2702,2701,2697,2693,2692,2687,2685,2678,2676,2672,2668,
2664,2659,2655,2654,2652,2650,2648,2643,2631,2629,2618,2613,2610,2605,2603,2599,2589,2587,2585,2584,
2579,2575,2575,2573,2562,2562,2557,2556,2556,2556,2552,2550,2538,2529,2520,2516,2513,2512,2508,2500
};

                while (pool.Count < n && guard < maxGuard)
                {
                    guard++;
                    int ci = PickWeighted(rnd, clW, (cfg == 0) ? 300 : clW.Length);
                    // 智利手机: 56 + 9 + 8位(clSeg后3位交换码 + 5位尾号). clSeg[0]='9'网络码前缀
                    string sub = rnd.Next(0, 99999).ToString("D5");
                    string num = "56" + "9" + clSeg[ci].Substring(1) + sub;
                    if (pool.Add(num)) newList.Add(num);
                }
            }
            else if (countryIdx == 3)
            {
                // ── 墨西哥(已验证): 52 + 区 + 号前3位块 + 子号  (V7实测5块/61%) ──
                // 块格式: 前2字符=区号(2位=大城市,3位=中小城), 后3字符=号码前3位(运营商号段)
                string[] mxBlockSeg = new string[]{"899160","844277","664188","664120","55540"};
                // 各块子号空间(3位区子号4位=1万, 2位区子号5位=10万): 用于供量权重
                int[] mxBlockSpace = new int[]{10000,10000,10000,10000,100000};
                while (pool.Count < n && guard < maxGuard)
                {
                    guard++;
                    int mi = PickWeighted(rnd, mxBlockSpace, mxBlockSpace.Length);
                    string bk = mxBlockSeg[mi];
                    string area = bk.Substring(0, bk.Length - 3);
                    string num3 = bk.Substring(bk.Length - 3);
                    int sublen = (area.Length == 2) ? 5 : 4;
                    string num = "52" + area + num3 + rnd.Next(0, (int)Math.Pow(10, sublen)).ToString("D" + sublen);
                    if (pool.Add(num)) newList.Add(num);
                }
            }
            else if (countryIdx == 4)
            {
                // ── 菲律宾: 63 + 前缀4位(网络码) + 7位 ──
                string[] phPre = new string[]{"917","918","920","921","899","998","977","939","949","947","908","916","927","905","906","991","915","989","977"};
                while (pool.Count < n && guard < maxGuard)
                {
                    guard++;
                    string p = phPre[rnd.Next(phPre.Length)];
                    string num = "63" + p + rnd.Next(1000000, 9999999).ToString();
                    if (pool.Add(num)) newList.Add(num);
                }
            }
            else if (countryIdx == 5)
            {
                // ── 越南: 84 + 网码2/3位 + 7位 ──
                string[] vnPre = new string[]{"96","97","98","32","33","34","35","36","37","38","39","90","91","93","94","81","82","83","84","85","70","76","77","78","79","88","86"};
                while (pool.Count < n && guard < maxGuard)
                {
                    guard++;
                    string num = "84" + vnPre[rnd.Next(vnPre.Length)] + rnd.Next(1000000, 9999999).ToString();
                    if (pool.Add(num)) newList.Add(num);
                }
            }
            else
            {
                // ── 印度: 91 + 10位(首 6/7/8/9) ──
                while (pool.Count < n && guard < maxGuard)
                {
                    guard++;
                    string num = "91" + "6789"[rnd.Next(4)] + rnd.Next(100000000, 999999999).ToString();
                    if (pool.Add(num)) newList.Add(num);
                }
            }

            int added = AppendUnique(sList, newList);
            HideProgress();
            Enabled = true;
            UpdateAllPanes();
            MessageBox.Show(string.Format("生成完成: 新增 {0:n0} 条 (共生成 {1:n0} 条)", added, newList.Count), "完成");
            f.Close();
        }

        // 按权重随机选下标
        static int PickWeighted(Random rnd, int[] w, int limit)
        {
            int tot = 0; for (int i = 0; i < limit && i < w.Length; i++) tot += w[i];
            int r = rnd.Next(tot);
            int n = Math.Min(limit, w.Length);
            for (int i = 0; i < n; i++)
            { r -= w[i]; if (r < 0) return i; }
            return n - 1;
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
• 号码生成：选国家(巴西/美国/智利/墨西哥/菲律宾/越南/印度)+填数量直接生成，自动去重。巴西=已验证号段方案

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