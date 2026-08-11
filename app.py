#!/usr/bin/env python3
"""号码魔方 v2 — 本地离线号码处理工具
对标「指尖聚赢号码魔方」，纯本地运行，不联网。
启动: python3 app.py  → 浏览器打开 http://127.0.0.1:18888
打包: pyinstaller --onefile --windowed --name 号码魔方 app.py
"""

import http.server
import json
import os
import re
import sys
import tempfile
import shutil
import webbrowser
from urllib.parse import parse_qs, urlparse
from collections import Counter

PORT = 18888

# ═══════════════════════════════════════════
# 核心处理逻辑
# ═══════════════════════════════════════════

class NumStore:
    """内存数据存储"""
    def __init__(self):
        self.numbers = []        # 原始号码列表
        self.cleaned = []        # 清洗后列表
        self.compare_a = None    # 对比 A
        self.compare_b = None    # 对比 B

store = NumStore()


def clean_number(raw: str) -> str:
    """清洗单个号码：去空格/横线/括号/+号/前后空白"""
    s = raw.strip()
    s = re.sub(r'[\s\-\+\(\)\.\,]', '', s)
    return s


def load_numbers(text: str) -> dict:
    """加载号码文本，返回统计"""
    lines = [l.strip() for l in text.split('\n') if l.strip()]
    cleaned_all = [clean_number(l) for l in lines]

    # 去重（保序）
    seen = set()
    unique = []
    for n in cleaned_all:
        if n not in seen:
            seen.add(n)
            unique.append(n)

    store.numbers = lines
    store.cleaned = cleaned_all
    dupes = len(cleaned_all) - len(unique)

    # 长度分布
    len_dist = Counter(len(n) for n in cleaned_all)

    return {
        'total': len(lines),
        'unique': len(unique),
        'dupes': dupes,
        'dupe_rate': f"{dupes / len(lines) * 100:.1f}%" if lines else "0%",
        'len_dist': dict(len_dist.most_common(20)),
        'unique_numbers': unique,
    }


def merge_debug(a_text: str, b_text: str) -> dict:
    """对比两组号码"""
    a_result = load_numbers(a_text)
    b_result = load_numbers(b_text)

    a_set = set(a_result['unique_numbers'])
    b_set = set(b_result['unique_numbers'])

    intersection = a_set & b_set
    a_only = a_set - b_set
    b_only = b_set - a_set
    union = a_set | b_set

    return {
        'a_total': a_result['total'],
        'a_unique': len(a_set),
        'b_total': b_result['total'],
        'b_unique': len(b_set),
        'overlap': len(intersection),
        'overlap_rate_a': f"{len(intersection) / len(a_set) * 100:.1f}%" if a_set else "0%",
        'overlap_rate_b': f"{len(intersection) / len(b_set) * 100:.1f}%" if b_set else "0%",
        'a_only': len(a_only),
        'b_only': len(b_only),
        'merged': len(union),
        'merged_list': sorted(union),
    }


# ═══════════════════════════════════════════
# HTTP 路由
# ═══════════════════════════════════════════

HTML = r'''<!DOCTYPE html>
<html lang="zh-CN">
<head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1.0">
<title>号码魔方 v2</title>
<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: 'Segoe UI', system-ui, sans-serif; background: #f0f2f5; color: #1a1a1a; }
header { background: #1a1a1a; color: #fff; padding: 16px 24px; display: flex; align-items: center; gap: 12px; }
header h1 { font-size: 20px; font-weight: 600; }
header .ver { font-size: 12px; opacity: .6; }
.container { max-width: 1000px; margin: 0 auto; padding: 24px 16px; }
.card { background: #fff; border-radius: 10px; padding: 20px; margin-bottom: 16px; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
.card h2 { font-size: 16px; font-weight: 600; margin-bottom: 14px; color: #333; }
.btn { background: #1a1a1a; color: #fff; border: none; padding: 10px 22px; border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 500; transition: opacity .15s; }
.btn:hover { opacity: .85; }
.btn-sm { padding: 6px 14px; font-size: 13px; }
.btn-outline { background: #fff; color: #1a1a1a; border: 1.5px solid #1a1a1a; }
.btn-outline:hover { background: #f5f5f5; opacity: 1; }
.btn-danger { background: #e74c3c; }
.btn-success { background: #27ae60; }
input[type="file"] { display: none; }
textarea { width: 100%; min-height: 120px; border: 1.5px solid #ddd; border-radius: 6px; padding: 12px; font-size: 14px; font-family: 'Consolas', 'Courier New', monospace; resize: vertical; }
textarea:focus { outline: none; border-color: #1a1a1a; }
.row { display: flex; gap: 12px; flex-wrap: wrap; align-items: center; margin-bottom: 10px; }
.stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 12px; }
.stat-card { background: #f8f9fa; border-radius: 8px; padding: 14px; text-align: center; }
.stat-card .value { font-size: 28px; font-weight: 700; color: #1a1a1a; }
.stat-card .label { font-size: 12px; color: #777; margin-top: 4px; }
.compare-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.compare-grid textarea { min-height: 100px; }
.result-box { background: #f8f9fa; border-radius: 8px; padding: 14px; max-height: 200px; overflow-y: auto; font-family: 'Consolas', monospace; font-size: 13px; white-space: pre-wrap; }
.hidden { display: none !important; }
.preview { max-height: 300px; overflow-y: auto; background: #fafafa; border-radius: 6px; padding: 12px; font-family: 'Consolas', monospace; font-size: 12px; line-height: 1.8; }
.msg { padding: 10px 14px; border-radius: 6px; margin-bottom: 10px; font-size: 14px; }
.msg-success { background: #d4edda; color: #155724; }
.msg-error { background: #f8d7da; color: #721c24; }
.spinner { display: inline-block; width: 16px; height: 16px; border: 2px solid #fff; border-radius: 50%; border-top-color: transparent; animation: spin .6s linear infinite; margin-right: 6px; vertical-align: middle; }
@keyframes spin { to { transform: rotate(360deg); } }
.tab-bar { display: flex; gap: 0; border-bottom: 2px solid #e0e0e0; margin-bottom: 16px; }
.tab { padding: 10px 20px; cursor: pointer; font-size: 14px; font-weight: 500; color: #888; border-bottom: 2px solid transparent; margin-bottom: -2px; transition: all .15s; }
.tab.active { color: #1a1a1a; border-bottom-color: #1a1a1a; }
</style>
</head>
<body>

<header>
  <h1>号码魔方 v2</h1>
  <span class="ver">离线版 本地运行</span>
</header>

<div class="container">

  <div id="msg"></div>

  <div class="tab-bar">
    <div class="tab active" data-tab="dedup">去重 & 统计</div>
    <div class="tab" data-tab="compare">文件对比</div>
    <div class="tab" data-tab="clean">格式清洗</div>
  </div>

  <!-- === Tab: 去重 === -->
  <div id="tab-dedup" class="tab-content">
    <div class="card">
      <h2>导入号码（每行一个）</h2>
      <div class="row">
        <button class="btn" onclick="document.getElementById('fileInput').click()">选择文件</button>
        <input type="file" id="fileInput" accept=".txt,.csv" onchange="uploadFile(this)">
        <span style="color:#888;font-size:13px">或粘贴到下方文本框</span>
      </div>
      <textarea id="dedupInput" placeholder="每行一个号码，直接粘贴在此…"></textarea>
      <div class="row" style="margin-top:10px">
        <button class="btn btn-success" onclick="runDedup()">开始去重</button>
        <button class="btn btn-outline btn-sm" onclick="clearDedup()">清空</button>
      </div>
    </div>

    <div id="dedupStats" class="hidden">
      <div class="card">
        <h2>统计结果</h2>
        <div class="stats" id="statsGrid"></div>
        <div class="row" style="margin-top:12px">
          <button class="btn btn-success" onclick="downloadResult()">导出（每行一个，去重后）</button>
          <button class="btn btn-outline btn-sm" onclick="downloadSorted()">导出（数字排序）</button>
          <button class="btn btn-outline btn-sm" onclick="downloadShuffled()">导出（随机打乱）</button>
        </div>
      </div>
      <div class="card">
        <h2>结果预览（前1000条）</h2>
        <div class="preview" id="preview"></div>
      </div>
    </div>
  </div>

  <!-- === Tab: 对比 === -->
  <div id="tab-compare" class="tab-content hidden">
    <div class="card">
      <h2>文件对比</h2>
      <div class="compare-grid">
        <div>
          <strong>文件 A</strong>
          <button class="btn btn-sm btn-outline" style="margin-left:8px" onclick="document.getElementById('fileA').click()">选择</button>
          <input type="file" id="fileA" accept=".txt,.csv" onchange="uploadTo('fileA', 'compareInputA')" style="display:none">
          <textarea id="compareInputA" placeholder="粘贴 A 文件内容"></textarea>
        </div>
        <div>
          <strong>文件 B</strong>
          <button class="btn btn-sm btn-outline" style="margin-left:8px" onclick="document.getElementById('fileB').click()">选择</button>
          <input type="file" id="fileB" accept=".txt,.csv" onchange="uploadTo('fileB', 'compareInputB')" style="display:none">
          <textarea id="compareInputB" placeholder="粘贴 B 文件内容"></textarea>
        </div>
      </div>
      <div class="row" style="margin-top:10px">
        <button class="btn btn-success" onclick="runCompare()">开始对比</button>
        <button class="btn btn-outline btn-sm" onclick="clearCompare()">清空</button>
      </div>
    </div>

    <div id="compareResult" class="hidden">
      <div class="card">
        <h2>对比结果</h2>
        <div class="stats" id="compareStats"></div>
        <div class="row" style="margin-top:12px">
          <button class="btn btn-success" onclick="downloadCompareMerged()">导出合并去重</button>
          <button class="btn btn-outline btn-sm" onclick="downloadCompareAOnly()">仅导出 A 独有</button>
        </div>
      </div>
    </div>
  </div>

  <!-- === Tab: 清洗 === -->
  <div id="tab-clean" class="tab-content hidden">
    <div class="card">
      <h2>格式清洗（去空格/横线/括号/+号）</h2>
      <textarea id="cleanInput" placeholder="粘贴号码…"></textarea>
      <div class="row" style="margin-top:10px">
        <button class="btn btn-success" onclick="runClean()">清洗</button>
        <button class="btn btn-outline btn-sm" onclick="clearClean()">清空</button>
      </div>
    </div>
    <div id="cleanResult" class="hidden">
      <div class="card">
        <h2>清洗结果</h2>
        <div class="result-box" id="cleanPreview"></div>
        <button class="btn btn-success" style="margin-top:10px" onclick="downloadCleanResult()">导出清洗结果</button>
      </div>
    </div>
  </div>

</div>

<script>
let lastResult = null;
let lastCompareResult = null;
let lastCleanResult = null;

// Tab 切换
document.querySelectorAll('.tab').forEach(t => {
  t.onclick = () => {
    document.querySelectorAll('.tab').forEach(x => x.classList.remove('active'));
    document.querySelectorAll('.tab-content').forEach(x => x.classList.add('hidden'));
    t.classList.add('active');
    document.getElementById('tab-' + t.dataset.tab).classList.remove('hidden');
  };
});

function showMsg(text, type) {
  const m = document.getElementById('msg');
  m.className = 'msg msg-' + type;
  m.textContent = text;
  setTimeout(() => m.textContent = '', 5000);
}

async function uploadFile(input) {
  const file = input.files[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = e => {
    document.getElementById('dedupInput').value = e.target.result;
    showMsg('已加载: ' + file.name + ' (' + (file.size/1024).toFixed(0) + ' KB)', 'success');
  };
  reader.readAsText(file);
}

async function uploadTo(inputId, targetId) {
  const file = document.getElementById(inputId).files[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = e => document.getElementById(targetId).value = e.target.result;
  reader.readAsText(file);
}

async function runDedup() {
  const text = document.getElementById('dedupInput').value;
  if (!text.trim()) return showMsg('请先输入号码', 'error');

  const resp = await fetch('/api/dedup', {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({text})
  });
  const data = await resp.json();
  lastResult = data;

  if (data.error) return showMsg(data.error, 'error');

  document.getElementById('dedupStats').classList.remove('hidden');
  document.getElementById('statsGrid').innerHTML = `
    <div class="stat-card"><div class="value">${data.total.toLocaleString()}</div><div class="label">总行数</div></div>
    <div class="stat-card"><div class="value">${data.unique.toLocaleString()}</div><div class="label">去重后</div></div>
    <div class="stat-card"><div class="value">${data.dupes.toLocaleString()}</div><div class="label">重复数</div></div>
    <div class="stat-card"><div class="value">${data.dupe_rate}</div><div class="label">重复率</div></div>
  `;

  const preview = data.preview.join('\n');
  document.getElementById('preview').innerHTML = preview || '(空)';
  showMsg(`去重完成: ${data.total.toLocaleString()} → ${data.unique.toLocaleString()}`, 'success');
}

function downloadResult() {
  if (!lastResult || !lastResult.unique_numbers) return;
  downloadFile(lastResult.unique_numbers.join('\n'), '去重结果.txt');
}

function downloadSorted() {
  if (!lastResult || !lastResult.unique_numbers) return;
  const sorted = [...lastResult.unique_numbers].sort();
  downloadFile(sorted.join('\n'), '去重结果_排序.txt');
}

function downloadShuffled() {
  if (!lastResult || !lastResult.unique_numbers) return;
  const arr = [...lastResult.unique_numbers];
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  downloadFile(arr.join('\n'), '去重结果_随机.txt');
}

async function runCompare() {
  const a = document.getElementById('compareInputA').value;
  const b = document.getElementById('compareInputB').value;
  if (!a.trim() || !b.trim()) return showMsg('请输入 A 和 B', 'error');

  const resp = await fetch('/api/compare', {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({a, b})
  });
  const data = await resp.json();
  lastCompareResult = data;

  document.getElementById('compareResult').classList.remove('hidden');
  document.getElementById('compareStats').innerHTML = `
    <div class="stat-card"><div class="value">${data.a_total.toLocaleString()}</div><div class="label">A 总数</div></div>
    <div class="stat-card"><div class="value">${data.a_unique.toLocaleString()}</div><div class="label">A 去重</div></div>
    <div class="stat-card"><div class="value">${data.b_total.toLocaleString()}</div><div class="label">B 总数</div></div>
    <div class="stat-card"><div class="value">${data.b_unique.toLocaleString()}</div><div class="label">B 去重</div></div>
    <div class="stat-card"><div class="value">${data.overlap.toLocaleString()}</div><div class="label">重复数</div></div>
    <div class="stat-card"><div class="value">${data.overlap_rate_a}</div><div class="label">A 被 B 覆盖</div></div>
    <div class="stat-card"><div class="value">${data.a_only.toLocaleString()}</div><div class="label">A 独有</div></div>
    <div class="stat-card"><div class="value">${data.merged.toLocaleString()}</div><div class="label">合并去重</div></div>
  `;
  showMsg(`对比完成: ${data.overlap.toLocaleString()} 条重复`, 'success');
}

function downloadCompareMerged() {
  if (!lastCompareResult || !lastCompareResult.merged_list) return;
  downloadFile(lastCompareResult.merged_list.join('\n'), '对比合并_去重.txt');
}

function downloadCompareAOnly() {
  alert('A 独有数量: ' + (lastCompareResult?.a_only || 0));
  // 后端重新请求 A only
  const a = document.getElementById('compareInputA').value;
  if (!a.trim()) return;
  fetch('/api/dedup', {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({text:a})})
    .then(r => r.json()).then(ad => {
      const b = document.getElementById('compareInputB').value;
      fetch('/api/dedup', {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({text:b})})
        .then(r => r.json()).then(bd => {
          const aSet = new Set(ad.unique_numbers || []);
          const bSet = new Set(bd.unique_numbers || []);
          const only = [...aSet].filter(x => !bSet.has(x));
          downloadFile(only.join('\n'), 'A独有号码.txt');
        });
    });
}

async function runClean() {
  const text = document.getElementById('cleanInput').value;
  if (!text.trim()) return showMsg('请先输入号码', 'error');

  const resp = await fetch('/api/dedup', {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({text, clean_only: true})
  });
  const data = await resp.json();
  lastCleanResult = data;

  document.getElementById('cleanResult').classList.remove('hidden');
  const preview = data.cleaned_preview.slice(0, 200).join('\n');
  document.getElementById('cleanPreview').textContent = preview;
  showMsg(`清洗完成: ${data.total} 条`, 'success');
}

function downloadCleanResult() {
  if (!lastCleanResult || !lastCleanResult.cleaned_all) return;
  downloadFile(lastCleanResult.cleaned_all.join('\n'), '清洗结果.txt');
}

function downloadFile(content, filename) {
  const blob = new Blob([content], {type: 'text/plain'});
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url; a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
  showMsg('已导出: ' + filename, 'success');
}

function clearDedup() {
  document.getElementById('dedupInput').value = '';
  document.getElementById('dedupStats').classList.add('hidden');
  lastResult = null;
}

function clearCompare() {
  document.getElementById('compareInputA').value = '';
  document.getElementById('compareInputB').value = '';
  document.getElementById('compareResult').classList.add('hidden');
  lastCompareResult = null;
}

function clearClean() {
  document.getElementById('cleanInput').value = '';
  document.getElementById('cleanResult').classList.add('hidden');
  lastCleanResult = null;
}
</script>
</body></html>'''


class Handler(http.server.BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass  # 静默日志

    def _send_json(self, data, status=200):
        body = json.dumps(data, ensure_ascii=False).encode('utf-8')
        self.send_response(status)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Content-Length', len(body))
        self.end_headers()
        self.wfile.write(body)

    def _read_body(self):
        length = int(self.headers.get('Content-Length', 0))
        return self.rfile.read(length).decode('utf-8')

    def do_GET(self):
        path = urlparse(self.path).path
        if path == '/' or path == '/index.html':
            self.send_response(200)
            self.send_header('Content-Type', 'text/html; charset=utf-8')
            self.end_headers()
            self.wfile.write(HTML.encode('utf-8'))
        else:
            self._send_json({'error': 'not found'}, 404)

    def do_POST(self):
        path = urlparse(self.path).path

        if path == '/api/dedup':
            body = json.loads(self._read_body())
            text = body.get('text', '')
            clean_only = body.get('clean_only', False)

            lines = [l.strip() for l in text.split('\n') if l.strip()]
            cleaned_all = [clean_number(l) for l in lines]

            if clean_only:
                self._send_json({
                    'total': len(lines),
                    'cleaned_preview': cleaned_all[:500],
                    'cleaned_all': cleaned_all,
                })
                return

            seen = set()
            unique = []
            for n in cleaned_all:
                if n not in seen:
                    seen.add(n)
                    unique.append(n)

            dupes = len(cleaned_all) - len(unique)
            self._send_json({
                'total': len(lines),
                'unique': len(unique),
                'dupes': dupes,
                'dupe_rate': f"{dupes / len(lines) * 100:.1f}%" if lines else "0%",
                'preview': unique[:1000],
                'unique_numbers': unique,
            })

        elif path == '/api/compare':
            body = json.loads(self._read_body())
            result = merge_debug(body.get('a', ''), body.get('b', ''))
            self._send_json(result)

        else:
            self._send_json({'error': 'not found'}, 404)


def main():
    # 尝试打开浏览器
    webbrowser.open(f'http://127.0.0.1:{PORT}')

    server = http.server.HTTPServer(('127.0.0.1', PORT), Handler)
    print(f'号码魔方 v2 已启动 → http://127.0.0.1:{PORT}')
    print('关闭此窗口或按 Ctrl+C 退出')
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print('\n已退出')
        server.server_close()


if __name__ == '__main__':
    main()
