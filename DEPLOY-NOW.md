# 一鍵部署（3 分鐘）

## 方式 A：雙擊執行（最簡單）

1. 在檔案總管開啟專案資料夾 `ttriticket`
2. **雙擊 `deploy.bat`**
3. 若要求登入 GitHub，依畫面指示完成
4. 到 GitHub 網站 → 你的 `ttriticket` 儲存庫 → **Settings** → **Pages**
5. **Source** 選 **GitHub Actions**
6. 到 **Actions** 分頁，等 `Deploy GitHub Pages` 完成（綠色 ✓）

完成後網址：

```
https://你的GitHub帳號.github.io/ttriticket/
```

---

## 方式 B：Cursor 終端機（若 deploy.bat 失敗）

先修復 PowerShell（在終端機執行一次）：

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

再執行：

```powershell
cd C:\Users\yslin.1524\Desktop\ttriticket
.\deploy.bat
```

---

## 已設定好的項目（不用再改）

| 項目 | 狀態 |
|------|------|
| 靜態網站 | `docs/index.html` |
| Apps Script URL | `docs/js/config.js` 已填入 |
| 自動部署 | `.github/workflows/deploy-pages.yml` |

---

## 分享給同事

```
投票網址：https://你的帳號.github.io/ttriticket/
輸入職編即可投票，同一職編只能投一次。
```
