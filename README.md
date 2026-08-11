# 龙哥数据_筛选

Windows 桌面号码数据处理工具，C# WinForms + .NET Framework 4.0 单文件。

## 功能

- 导入号码 (文件/剪贴板/号段生成)
- 过滤重复 / 合并重复 (核心)
- 排序 (升序/降序) · 乱序 (Fisher-Yates)
- 去重 · 清除非号 · 前后缀增删
- 16 窗格 VirtualMode 显示 (默认 8 窗，超 160 万自动增窗，上限 1000 万，水平滑动)
- 导出 (全部/分批/按区域/按运营商) · 文件对比 · 报告

## 编译

需要 .NET Framework 4.0 SDK (`C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe`)。

```powershell
csc /target:winexe /out:nm.exe /win32icon:app.ico /codepage:65001 ^
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /nologo NumMagic.cs
```

> `/codepage:65001` 必需——源码 UTF-8 无 BOM，不加则中文乱码。

## 运行

单文件 exe，双击即用。Windows 7/8/10/11 自带 .NET 4.0+，无需安装任何运行时。

首次运行在同目录生成 `settings.ini`。

## 技术栈

- C# 4.0 (无 `is` 模式匹配、无 `$""` 插值)
- WinForms VirtualMode ListView (百万级不卡)
- Fisher-Yates 原地乱序 O(n)
- INI (kernel32) 配置持久化

## 项目结构

```
NumMagic.cs    - 主源码 (单文件)
app.ico        - 应用图标 (浅灰底+深色"筛"字 32×32)
build.ps1      - 编译脚本
```