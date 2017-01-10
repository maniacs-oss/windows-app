$vsixPath = "$($env:USERPROFILE)\sqlite-uwp.vsix"
(New-Object Net.WebClient).DownloadFile('https://www.sqlite.org/2016/sqlite-uwp-3150200.vsix', $vsixPath)
"`"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\VSIXInstaller.exe`" /q /a $vsixPath" | out-file ".\install-vsix.cmd" -Encoding ASCII
& .\install-vsix.cmd
Write-Host "Installed SQLite." -ForegroundColor green

nuget restore
Write-Host "Installed NuGet dependencies." -ForegroundColor green