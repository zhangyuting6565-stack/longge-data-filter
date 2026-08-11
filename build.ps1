$csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
$out = "C:\Users\zhang\Desktop\nm.exe"
$src = "C:\Users\zhang\num-magic.cs"
$icon = "C:\Users\zhang\app.ico"
& $csc /target:winexe /out:$out /win32icon:$icon /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /nologo $src 2>&1
Write-Host "EXIT:$LASTEXITCODE"
if ($LASTEXITCODE -eq 0) { Write-Host "OK" (Get-Item $out).Length "bytes" }