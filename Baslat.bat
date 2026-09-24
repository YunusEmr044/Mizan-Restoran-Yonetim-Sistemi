@echo off
REM Mizan - Restoran Yonetim Sistemi - tek tikla baslatma. Site zaten calisiyorsa sadece tarayiciyi acar.
cd /d "%~dp0"

tasklist /FI "IMAGENAME eq RestoranYonetim.exe" 2>nul | find /I "RestoranYonetim.exe" >nul
if not errorlevel 1 goto ac

start "Mizan - bu pencere kapanirsa site durur" /min RestoranYonetim.exe

REM Sunucu cevap verene kadar en fazla 30 sn bekle (ilk acilista migration/yedek kontrolu surebilir).
set /a deneme=0
:bekle
curl -s -o nul http://localhost:5080/health && goto ac
set /a deneme+=1
if %deneme% geq 30 goto ac
timeout /t 1 /nobreak >nul
goto bekle

:ac
start "" http://localhost:5080/admin
