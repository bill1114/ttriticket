# GitHub Pages 靜態部署（不用 Azure）

靜態網站在 `docs/`，透過 **Google Apps Script** 讀寫試算表。

```
同事瀏覽器 → https://你的帳號.github.io/ttriticket/
                    ↓
            Apps Script API
                    ↓
            Google 試算表（候選人 + 投票紀錄）
```

---

## 第一步：更新 Apps Script（必做）

1. 開啟 [Google Apps Script](https://script.google.com)
2. 貼上 `docs/google-apps-script-votes.gs` **完整內容**（已含讀候選人、讀票數、寫投票）
3. 執行一次 `doGet` → 完成授權
4. **部署** → **管理部署作業** → **新版本** → **部署**
   - 執行身分：**我**
   - 存取權：**任何人**
5. 複製 **網路應用程式 URL**

測試：瀏覽器開啟

```
你的URL?action=candidates&callback=test
```

若看到 JSON 或 `test({...})` 代表成功。

---

## 第二步：設定靜態網站

編輯 `docs/js/config.js`：

```javascript
window.TTRI_CONFIG = {
  webAppUrl: 'https://script.google.com/macros/s/你的部署ID/exec',
  title: '「那個熟悉的背影」攝影人氣投稿'
};
```

---

## 第三步：推送到 GitHub

```powershell
cd C:\Users\yslin.1524\Desktop\ttriticket
git add .
git commit -m "Add GitHub Pages static site"
git push
```

---

## 第四步：啟用 GitHub Pages

1. GitHub 儲存庫 → **Settings** → **Pages**
2. **Build and deployment**
   - Source：**GitHub Actions**
3. 到 **Actions** 分頁，確認 `Deploy GitHub Pages` workflow 綠色成功

網址會是：

```
https://你的GitHub帳號.github.io/儲存庫名稱/
```

例如：`https://yslin1524.github.io/ttriticket/`

---

## 第五步：分享給同事

```
【投票系統試用】
https://你的帳號.github.io/ttriticket/

1. 輸入職編（例：1524、D596）
2. 選候選人 → 投票 → 確定
3. 「投票結果」可看票數
```

---

## 與 ASP.NET 版的差異

| 項目 | ASP.NET 本機版 | GitHub Pages 靜態版 |
|------|----------------|---------------------|
| 執行環境 | 需 `dotnet run` | 瀏覽器直接開 |
| 資料串接 | 直接讀 CSV + Apps Script | 全部經 Apps Script |
| 照片 | 後端代理 Drive | Drive 縮圖網址（需公開權限） |

本機 `TtriTicket/` 專案可保留開發用；對外測試用 `docs/` 靜態版即可。

---

## 常見問題

### 候選人載入失敗

- Apps Script 是否重新部署
- `config.js` 的 `webAppUrl` 是否正確
- 試算表是否公開可讀

### 投票失敗

- Apps Script 存取權是否為「任何人」
- 「投票紀錄」B 欄是否為純文字

### 照片無法顯示

- Google Drive 投稿資料夾需設「知道連結的任何人可檢視」

---

## 安全提醒

- `webAppUrl` 會出現在前端程式碼中（試用可接受）
- 防重複投票靠 Apps Script 檢查職編
- 建議 GitHub 儲存庫設 **Private**
