@echo off
chcp 65001 >nul
cd /d "%~dp0"

set "GIT=C:\Program Files\Git\bin\git.exe"
set "GH=C:\Program Files\GitHub CLI\gh.exe"

echo ========================================
echo  TtriTicket 一鍵部署到 GitHub Pages
echo ========================================
echo.

if not exist "%GIT%" (
    echo [錯誤] 找不到 Git，請先安裝：
    echo https://git-scm.com/download/win
    pause
    exit /b 1
)

if not exist .git (
    echo [1/4] 初始化 Git...
    "%GIT%" init
) else (
    echo [1/4] Git 已存在，略過 init
)

echo [2/4] 加入檔案並提交...
"%GIT%" add .
"%GIT%" diff --cached --quiet
if %errorlevel%==0 (
    echo 沒有新變更，略過 commit
) else (
    "%GIT%" commit -m "Deploy: GitHub Pages voting site"
)
"%GIT%" branch -M main 2>nul

if not exist "%GH%" (
    echo.
    echo [3/4] 未安裝 GitHub CLI，請手動執行：
    echo   1. 到 https://github.com/new 建立儲存庫 ttriticket
    echo   2. 執行：
    echo      git remote add origin https://github.com/你的帳號/ttriticket.git
    echo      git push -u origin main
    echo   3. GitHub - Settings - Pages - Source 選 GitHub Actions
    pause
    exit /b 0
)

echo [3/4] 檢查 GitHub 登入...
"%GH%" auth status >nul 2>&1
if errorlevel 1 (
    echo 請先登入 GitHub：
    "%GH%" auth login
)

echo [4/4] 建立/推送儲存庫...
"%GH%" repo view ttriticket >nul 2>&1
if errorlevel 1 (
    "%GH%" repo create ttriticket --private --source=. --remote=origin --push
) else (
    "%GIT%" push -u origin main
)

echo.
echo ========================================
echo  推送完成！請到 GitHub 網站完成最後一步：
echo  儲存庫 - Settings - Pages
echo  - Build and deployment - Source: GitHub Actions
echo  然後到 Actions 分頁等待綠色勾勾
echo.
echo  網址將是：https://你的帳號.github.io/ttriticket/
echo ========================================
pause
