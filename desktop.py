#!/usr/bin/env python3
"""号码魔方 v2 — 桌面版
对标「指尖聚赢号码魔方」，原生 Tkinter GUI。
"""

import tkinter as tk
from tkinter import ttk, filedialog, messagebox
import re
import os
import random

# ── 跨平台字体 ──
def _default_font(size=10, bold=False):
    """在 Linux 上优先用 Noto Sans CJK，其次文泉驿"""
    families = ["Noto Sans CJK SC", "WenQuanYi Micro Hei", "DejaVu Sans", "TkDefaultFont"]
    return (families, size, "bold" if bold else "normal")

def _mono_font(size=10):
    families = ["Noto Sans Mono CJK SC", "DejaVu Sans Mono", "Courier", "TkFixedFont"]
    return (families, size, "normal")


class NumMagic:
    def __init__(self):
        self.root = tk.Tk()
        self.root.title("号码魔方 v2")
        self.root.geometry("1080x680")
        self.root.minsize(860, 480)
        self.root.configure(bg="#f0f0f0")

        self.raw_numbers = []
        self.cleaned_numbers = []

        # 加载数据文件编号
        self._data_filename = ""

        self._build_menu()
        self._build_toolbar()
        self._build_main()
        self._build_status()

    # ═══════════════ 菜单 ═══════════════
    def _build_menu(self):
        bar = tk.Menu(self.root)
        f = tk.Menu(bar, tearoff=0)
        f.add_command(label="打开文件...", command=self.open_file, accelerator="Ctrl+O")
        f.add_command(label="粘贴", command=self.paste_clip, accelerator="Ctrl+V")
        f.add_separator()
        f.add_command(label="导出目标...", command=self.export_target, accelerator="Ctrl+S")
        f.add_separator()
        f.add_command(label="退出", command=self.root.quit)
        bar.add_cascade(label="文件", menu=f)

        o = tk.Menu(bar, tearoff=0)
        o.add_command(label="去重", command=self.do_dedup)
        o.add_command(label="排序 (升序)", command=lambda: self.do_sort(True))
        o.add_command(label="排序 (降序)", command=lambda: self.do_sort(False))
        o.add_command(label="随机打乱", command=self.do_shuffle)
        o.add_separator()
        o.add_command(label="对比两文件...", command=self.do_compare)
        o.add_separator()
        o.add_command(label="清空全部", command=self.clear_all)
        bar.add_cascade(label="操作", menu=o)

        self.root.config(menu=bar)
        self.root.bind("<Control-o>", lambda e: self.open_file())
        self.root.bind("<Control-v>", lambda e: self.paste_clip())
        self.root.bind("<Control-s>", lambda e: self.export_target())

    # ═══════════════ 工具栏 ═══════════════
    def _build_toolbar(self):
        bar = tk.Frame(self.root, bg="#e0e0e0")
        bar.pack(fill=tk.X, side=tk.TOP)

        # 第1行：操作按钮
        r1 = tk.Frame(bar, bg="#e0e0e0")
        r1.pack(fill=tk.X, padx=4, pady=(4, 0))
        btns = [
            ("打开文件", self.open_file),
            ("粘贴",    self.paste_clip),
            ("去重",    self.do_dedup),
            ("升序",    lambda: self.do_sort(True)),
            ("降序",    lambda: self.do_sort(False)),
            ("随机",    self.do_shuffle),
            ("对比",    self.do_compare),
            ("导出",    self.export_target),
            ("清空",    self.clear_all),
        ]
        for text, cmd in btns:
            tk.Button(r1, text=text, command=cmd, relief=tk.RAISED,
                      bg="#fafafa", padx=8).pack(side=tk.LEFT, padx=2, pady=2)

        # 第2行：过滤
        r2 = tk.Frame(bar, bg="#e0e0e0")
        r2.pack(fill=tk.X, padx=4, pady=(0, 4))

        self.filter_var = tk.StringVar(value="all")
        tk.Radiobutton(r2, text="全部", variable=self.filter_var, value="all",
                       command=self.apply_filter, bg="#e0e0e0").pack(side=tk.LEFT, padx=4)
        tk.Radiobutton(r2, text="前缀", variable=self.filter_var, value="prefix",
                       command=self.apply_filter, bg="#e0e0e0").pack(side=tk.LEFT, padx=(4, 0))
        self.pfx = tk.Entry(r2, width=8)
        self.pfx.pack(side=tk.LEFT, padx=(0, 8))
        self.pfx.bind("<KeyRelease>", lambda e: self.apply_filter())

        tk.Radiobutton(r2, text="长度", variable=self.filter_var, value="length",
                       command=self.apply_filter, bg="#e0e0e0").pack(side=tk.LEFT, padx=(4, 0))
        self.ln = tk.Entry(r2, width=4)
        self.ln.pack(side=tk.LEFT)
        self.ln.bind("<KeyRelease>", lambda e: self.apply_filter())

        self.file_label = tk.Label(r2, text="", bg="#e0e0e0", fg="#555")
        self.file_label.pack(side=tk.RIGHT, padx=8)

    # ═══════════════ 三栏主区域 ═══════════════
    def _build_main(self):
        pw = tk.PanedWindow(self.root, orient=tk.HORIZONTAL, sashrelief=tk.RAISED, sashwidth=3)
        pw.pack(fill=tk.BOTH, expand=True, padx=4, pady=2)

        # ── 原始列表 ──
        f0 = tk.Frame(pw)
        tk.Label(f0, text="原始列表").pack(anchor=tk.W)
        self.raw_lb = tk.Listbox(f0, selectmode=tk.EXTENDED, exportselection=False,
                                  bg="white", font="TkFixedFont")
        sb0 = tk.Scrollbar(f0, command=self.raw_lb.yview)
        self.raw_lb.configure(yscrollcommand=sb0.set)
        self.raw_lb.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        sb0.pack(side=tk.RIGHT, fill=tk.Y)
        self.raw_lb.bind("<Double-Button-1>", lambda e: self._mv_sel("raw", "target"))
        self.raw_lb.bind("<Delete>", self._del_sel)

        # ── 过滤结果 ──
        f1 = tk.Frame(pw)
        tk.Label(f1, text="过滤结果").pack(anchor=tk.W)
        self.res_lb = tk.Listbox(f1, selectmode=tk.EXTENDED, exportselection=False,
                                  bg="#fffef5", font="TkFixedFont")
        sb1 = tk.Scrollbar(f1, command=self.res_lb.yview)
        self.res_lb.configure(yscrollcommand=sb1.set)
        self.res_lb.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        sb1.pack(side=tk.RIGHT, fill=tk.Y)
        self.res_lb.bind("<Double-Button-1>", lambda e: self._mv_sel("result", "target"))
        self.res_lb.bind("<Delete>", self._del_sel)

        # 移动按钮（夹在结果和目标之间）
        mv = tk.Frame(f1, bg="#f0f0f0")
        mv.pack(pady=4)
        tk.Button(mv, text=">>", command=lambda: self._mv_all("result", "target"), width=3).pack(side=tk.LEFT, padx=2)
        tk.Button(mv, text=">",  command=lambda: self._mv_sel("result", "target"), width=2).pack(side=tk.LEFT, padx=2)
        tk.Button(mv, text="<",  command=lambda: self._mv_sel("target", "result"), width=2).pack(side=tk.LEFT, padx=2)
        tk.Button(mv, text="<<", command=lambda: self._mv_all("target", "result"), width=3).pack(side=tk.LEFT, padx=2)

        # ── 目标列表 ──
        f2 = tk.Frame(pw)
        tk.Label(f2, text="目标列表").pack(anchor=tk.W)
        top2 = tk.Frame(f2)
        top2.pack(fill=tk.X)
        tk.Button(top2, text="清空", command=lambda: self._clear_list("target"),
                  relief=tk.RAISED, bg="#fafafa", padx=8).pack(side=tk.RIGHT)
        self.tgt_lb = tk.Listbox(f2, selectmode=tk.EXTENDED, exportselection=False,
                                  bg="#f0fff0", font="TkFixedFont")
        sb2 = tk.Scrollbar(f2, command=self.tgt_lb.yview)
        self.tgt_lb.configure(yscrollcommand=sb2.set)
        self.tgt_lb.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        sb2.pack(side=tk.RIGHT, fill=tk.Y)
        self.tgt_lb.bind("<Double-Button-1>", lambda e: self._mv_sel("target", "result"))
        self.tgt_lb.bind("<Delete>", self._del_sel)

        pw.add(f0, stretch="always")
        pw.add(f1, stretch="always")
        pw.add(f2, stretch="always")

    # ═══════════════ 状态栏 ═══════════════
    def _build_status(self):
        s = tk.Frame(self.root, bg="#d8d8d8", height=24)
        s.pack(fill=tk.X, side=tk.BOTTOM)
        s.pack_propagate(False)
        self.st_raw = tk.Label(s, text="原始: 0", bg="#d8d8d8")
        self.st_raw.pack(side=tk.LEFT, padx=8)
        self.st_res = tk.Label(s, text="结果: 0", bg="#d8d8d8")
        self.st_res.pack(side=tk.LEFT, padx=8)
        self.st_tgt = tk.Label(s, text="目标: 0", bg="#d8d8d8")
        self.st_tgt.pack(side=tk.LEFT, padx=8)
        self.st_info = tk.Label(s, text="就绪", bg="#d8d8d8", fg="#555")
        self.st_info.pack(side=tk.RIGHT, padx=8)

    # ═══════════════ 数据操作 ═══════════════
    def _clean(self, raw):
        return re.sub(r'[\s\-\+\(\)\.\,]', '', raw.strip())

    def open_file(self):
        fp = filedialog.askopenfilename(filetypes=[("文本", "*.txt"), ("CSV", "*.csv"), ("全部", "*.*")])
        if not fp: return
        for enc in ("utf-8", "gbk", "latin-1"):
            try:
                with open(fp, 'r', encoding=enc) as f:
                    self._load(f.read())
                break
            except: continue
        self._data_filename = os.path.basename(fp)
        self.file_label.config(text=self._data_filename)

    def paste_clip(self):
        try:
            t = self.root.clipboard_get()
        except:
            messagebox.showwarning("提示", "剪贴板为空")
            return
        self._load(t)
        self.file_label.config(text="[剪贴板]")

    def _load(self, text):
        lines = [l.strip() for l in text.split('\n') if l.strip()]
        self.raw_numbers = lines
        self.cleaned_numbers = [self._clean(l) for l in lines]
        self._fill(self.raw_lb, lines)
        self.apply_filter()
        self._upd_status()
        self.st_info.config(text=f"已加载 {len(lines)} 条")

    def apply_filter(self, *_):
        mode = self.filter_var.get()
        pfx_val = self.pfx.get().strip()
        ln_val = self.ln.get().strip()

        out = []
        for c in self.cleaned_numbers:
            if mode == "prefix" and pfx_val and not c.startswith(pfx_val):
                continue
            if mode == "length" and ln_val:
                try:
                    if len(c) != int(ln_val): continue
                except: pass
            out.append(c)

        self._fill(self.res_lb, out)
        self._upd_status()

    def do_dedup(self):
        items = list(self.res_lb.get(0, tk.END))
        seen = set()
        uniq = []
        dup = 0
        for i in items:
            if i not in seen:
                seen.add(i); uniq.append(i)
            else: dup += 1
        self._fill(self.res_lb, uniq)
        self.st_info.config(text=f"去重: 移除 {dup} 条，剩余 {len(uniq)}")

    def do_sort(self, asc=True):
        items = list(self.res_lb.get(0, tk.END))
        items.sort(key=lambda x: int(x) if x.isdigit() else x, reverse=not asc)
        self._fill(self.res_lb, items)
        self.st_info.config(text="升序" if asc else "降序")

    def do_shuffle(self):
        items = list(self.res_lb.get(0, tk.END))
        random.shuffle(items)
        self._fill(self.res_lb, items)
        self.st_info.config(text="已随机打乱")

    def do_compare(self):
        fp = filedialog.askopenfilename(filetypes=[("文本", "*.txt"), ("全部", "*.*")])
        if not fp: return
        for enc in ("utf-8", "gbk", "latin-1"):
            try:
                with open(fp, 'r', encoding=enc) as f:
                    b_raw = set(self._clean(l) for l in f.read().split('\n') if l.strip())
                break
            except: continue
        else: return

        a = set(self.cleaned_numbers)
        overlap = a & b_raw
        msg = (f"A: {len(a)}  B: {len(b_raw)}\n"
               f"重复: {len(overlap)}\nA独有: {len(a-b_raw)}\nB独有: {len(b_raw-a)}\n"
               f"合并 ({len(a|b_raw)} 条) 到列表?")
        if messagebox.askyesno("对比结果", msg):
            merged = sorted(a | b_raw, key=lambda x: int(x) if x.isdigit() else x)
            self.raw_numbers = merged
            self.cleaned_numbers = merged
            self._fill(self.raw_lb, merged)
            self.apply_filter()
            self.st_info.config(text=f"合并: {len(merged)} 条")

    def _mv_sel(self, src, dst):
        """移动选中项"""
        lb_src = getattr(self, f"{src}_lb")
        lb_dst = getattr(self, f"{dst}_lb")
        sel = lb_src.curselection()
        if not sel: return
        items = [lb_src.get(i) for i in sel]
        for item in items: lb_dst.insert(tk.END, item)
        for i in reversed(sel): lb_src.delete(i)
        self._upd_status()

    def _mv_all(self, src, dst):
        lb_src = getattr(self, f"{src}_lb")
        lb_dst = getattr(self, f"{dst}_lb")
        items = list(lb_src.get(0, tk.END))
        for item in items: lb_dst.insert(tk.END, item)
        lb_src.delete(0, tk.END)
        self._upd_status()

    def _del_sel(self, event):
        widget = event.widget
        sel = widget.curselection()
        for i in reversed(sel): widget.delete(i)
        self._upd_status()

    def _clear_list(self, which):
        getattr(self, f"{which}_lb").delete(0, tk.END)
        self._upd_status()

    def clear_all(self):
        if not messagebox.askyesno("确认", "清空全部?"): return
        self.raw_numbers = []
        self.cleaned_numbers = []
        for lb in [self.raw_lb, self.res_lb, self.tgt_lb]:
            lb.delete(0, tk.END)
        self._upd_status()
        self.st_info.config(text="已清空")

    def export_target(self):
        items = list(self.tgt_lb.get(0, tk.END))
        if not items: items = list(self.res_lb.get(0, tk.END))
        if not items:
            messagebox.showwarning("提示", "无号码可导出"); return
        fp = filedialog.asksaveasfilename(defaultextension=".txt",
                                           filetypes=[("文本", "*.txt")])
        if not fp: return
        with open(fp, 'w', encoding='utf-8') as f:
            f.write('\n'.join(items))
        self.st_info.config(text=f"已导出 {len(items)} 条")

    # ═══════════════ 辅助 ═══════════════
    def _fill(self, lb, items):
        lb.delete(0, tk.END)
        for i in items: lb.insert(tk.END, i)

    def _upd_status(self):
        self.st_raw.config(text=f"原始: {len(self.raw_numbers)}")
        self.st_res.config(text=f"结果: {self.res_lb.size()}")
        self.st_tgt.config(text=f"目标: {self.tgt_lb.size()}")

    def run(self):
        self.root.mainloop()


if __name__ == "__main__":
    NumMagic().run()
