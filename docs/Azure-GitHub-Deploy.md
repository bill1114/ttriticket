# TtriTicket：GitHub + Azure 雲端部署（給協作人試用）

讓不同網域、不同地點的使用者，透過 **HTTPS 網址** 開啟投票系統。

```
你的電腦開發  →  push GitHub  →  Azure 自動部署  →  https://xxx.azurewebsites.net
```

> **GitHub 只放程式碼，不執行網站。** 實際跑網站的是 Azure App Service。

---

## 部署前檢查清單

在開始前，請確認本機已可正常運作：

- [ ] `dotnet run` 可登入、看候選人、投票成功
- [ ] Google 試算表已設「知道連結的使用者 → **檢視者**」
- [ ] 「投票紀錄」分頁 B 欄為 **純文字** 格式
- [ ] Apps Script 已部署（存取權：**任何人**）
- [ ] 瀏覽器開啟 Apps Script URL 可看到「服務已就緒」JSON

準備好以下資訊（稍後填入 Azure）：

| 設定項 | 你的值（範例） |
|--------|----------------|
| SpreadsheetId | `1AQGEom8myrDYbQBCJPAYIvx9DdkEnFBgtkAmbR0rdpQ` |
| SheetGid（候選人） | `736958374` |
| VotesSheetGid（投票紀錄） | `2096965003` |
| VoteAppendWebhookUrl | `https://script.google.com/macros/s/.../exec` |

---

## 第一部分：上傳程式碼到 GitHub

### 步驟 1-1：安裝 Git（若尚未安裝）

下載：https://git-scm.com/download/win  
安裝後開啟 **PowerShell** 或 **Git Bash**。

### 步驟 1-2：在 GitHub 建立儲存庫

1. 登入 https://github.com
2. 右上角 **+** → **New repository**
3. 設定：
   - Repository name：`ttriticket`（自訂）
   - Visibility：**Private**（建議，避免公開程式碼）
   - **不要**勾選 Add README（若本機已有專案）
4. 按 **Create repository**

### 步驟 1-3：本機推送程式碼

在 PowerShell 執行（路徑請依實際調整）：

```powershell
cd C:\Users\yslin.1524\Desktop\ttriticket

git init
git add .
git status
```

確認 **沒有** 出現 `appsettings.Local.json`（已在 .gitignore，含 Webhook 機密）。

```powershell
git commit -m "Initial commit: TtriTicket voting system"
git branch -M main
git remote add origin https://github.com/你的GitHub帳號/ttriticket.git
git push -u origin main
```

第一次 push 會要求登入 GitHub（瀏覽器或 Personal Access Token）。

---

## 第二部分：建立 Azure 帳號與 Web App

### 步驟 2-1：註冊 Azure

1. 開啟 https://azure.microsoft.com/free/
2. 使用 Microsoft 帳號註冊（新帳號通常有免費試用額度）
3. 完成信用卡驗證（免費層通常不會扣款，但 Azure 要求驗證）

### 步驟 2-2：建立 Web App

1. 登入 https://portal.azure.com
2. 首頁 → **建立資源** → 搜尋 **Web App** → **建立**

#### 「基本」分頁

| 欄位 | 建議值 |
|------|--------|
| 訂用帳戶 | 你的訂用帳戶 |
| 資源群組 | 新建 `ttriticket-rg` |
| 名稱 | `ttriticket`（全網唯一，網址會是 `ttriticket.azurewebsites.net`） |
| 發佈 | **程式碼** |
| 執行階段堆疊 | **.NET 8 (LTS)** |
| 作業系統 | **Linux**（較省）或 Windows 皆可 |
| 區域 | **East Asia** 或 **Japan East**（離台灣較近） |
| Linux 方案 | 選現有或新建 App Service 方案 |

#### 「定價」分頁

| 試用情境 | 建議 |
|----------|------|
| 2～3 人短期測 | **Free F1**（有冷啟動、資源少） |
| 多人、較穩定試用 | **Basic B1**（約每月少量費用） |

3. 其餘預設 → **檢閱 + 建立** → **建立**
4. 等待 1～2 分鐘，按 **移至資源**

記下你的網址：

```
https://ttriticket.azurewebsites.net
```

（名稱若不同，以你建立時為準。）

---

## 第三部分：連接 GitHub 自動部署

### 步驟 3-1：部署中心

1. Azure Portal → 你的 Web App
2. 左側選 **部署中心**（Deployment Center）
3. 來源：**GitHub**
4. 按 **授權**，登入 GitHub 並允許 Azure 存取
5. 選擇：
   - Organization：你的帳號
   - Repository：`ttriticket`
   - Branch：`main`
6. **建置類型**：GitHub Actions（預設）
7. 按 **儲存**

Azure 會在 GitHub 儲存庫自動建立 `.github/workflows/` 部署檔，並觸發第一次部署。

### 步驟 3-2：查看部署進度

**方式 A：GitHub**

1. 開啟 GitHub 儲存庫 → **Actions** 分頁
2. 看最新的 workflow 是否 **綠色勾勾**

**方式 B：Azure**

1. Web App → **部署中心** → **記錄**
2. 狀態應為 **成功 (Succeeded)**

第一次部署約 3～8 分鐘。

### 步驟 3-3：驗證網站有起來

瀏覽器開啟：

```
https://你的名稱.azurewebsites.net
```

應看到 **職編登入** 頁面（即使 Google 尚未設定，登入頁也應可開）。

---

## 第四部分：設定 Google 連線（必做）

本機的 `appsettings.Local.json` **不會** 上傳 GitHub，需在 Azure 手動設定。

### 步驟 4-1：新增應用程式設定

1. Azure Web App → **設定** → **環境變數**（或「組態」→「應用程式設定」）
2. **應用程式設定** 分頁 → **+ 新增**

逐一新增（名稱必須完全一致，使用 **雙底線 `__`**）：

| 名稱 | 值 |
|------|-----|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `GoogleSheets__SpreadsheetId` | `1AQGEom8myrDYbQBCJPAYIvx9DdkEnFBgtkAmbR0rdpQ` |
| `GoogleSheets__SheetGid` | `736958374` |
| `GoogleSheets__VotesSheetGid` | `2096965003` |
| `GoogleSheets__VoteAppendWebhookUrl` | 你的 Apps Script 完整 URL |
| `Voting__Title` | `「那個熟悉的背影」攝影人氣投稿` |
| `Voting__AllowMultipleVotes` | `false` |

3. 按 **套用** → **確認**
4. 左側 **概觀** → **重新啟動**

> 雙底線說明：`GoogleSheets__SpreadsheetId` 等同 `appsettings.json` 裡的 `GoogleSheets:SpreadsheetId`。

### 步驟 4-2：雲端完整測試

| 測試 | 預期 |
|------|------|
| 開啟網址 | 職編登入頁 |
| 輸入職編登入 | 看到候選人（綠色串接成功） |
| 投票 | 成功訊息，試算表「投票紀錄」多一列 |
| 投票結果 | 票數正確 |
| 同職編再投 | 「您已經投過票了」 |

---

## 第五部分：分享給協作人

### 給對方的網址

```
https://你的名稱.azurewebsites.net
```

### 可複製給協作人的說明

```
【投票系統試用】

1. 開啟：https://你的名稱.azurewebsites.net
2. 輸入您的職編（例：1524、D596、R000）
3. 選擇候選人 → 按「投票」→ 按「確定」
4. 上方「投票結果」可看即時票數
5. 同一職編只能投一次，投票後無法更改

不需安裝軟體，用手機或電腦瀏覽器即可。
```

---

## 第六部分：之後更新程式

本機修改程式後：

```powershell
cd C:\Users\yslin.1524\Desktop\ttriticket
git add .
git commit -m "說明你改了什麼"
git push
```

Push 後 GitHub Actions 會自動部署到 Azure（約 3～8 分鐘）。  
可在 GitHub **Actions** 查看進度。

---

## 常見問題排解

### HTTP 404 / 無法連線

- 確認 Web App 狀態為 **執行中**
- 網址是否為 `https://名稱.azurewebsites.net`（勿漏 `https`）

### 網站有開，但候選人載入失敗

- Azure 應用程式設定是否已填 `GoogleSheets__SpreadsheetId`、`GoogleSheets__SheetGid`
- 試算表是否公開可讀
- Web App → **記錄串流** 查看錯誤

### 投票失敗

- `GoogleSheets__VoteAppendWebhookUrl` 是否正確
- Apps Script 是否重新部署
- 試算表「投票紀錄」B 欄是否為純文字

### GitHub Actions 部署失敗（紅色 X）

1. GitHub → Actions → 點失敗的 run → 看錯誤訊息
2. 常見原因：專案路徑不對（workflow 需指向 `TtriTicket/TtriTicket.csproj`）
3. 若 Azure 自動產生的 workflow 路徑錯誤，請確認 workflow 內：

```yaml
working-directory: ./TtriTicket
```

或 project 路徑為 `TtriTicket/TtriTicket.csproj`

### Free 方案很慢或常斷

- Free F1 閒置會 **冷啟動**，第一次開啟要等 30 秒～1 分鐘
- 多人試用建議升級 **Basic B1**

### 不想把 Webhook 放在 Azure 介面

可改用 Azure **Key Vault**，試用階段直接用應用程式設定即可。

---

## 架構圖

```
協作人瀏覽器
    │
    ▼
Azure App Service（執行 TtriTicket）
    │
    ├── 讀 CSV ──────────► Google 試算表（候選人、投票紀錄）
    │
    └── POST 投票 ───────► Apps Script Webhook ──► 寫入「投票紀錄」
```

---

## 安全提醒（試用階段）

- 知道網址 + 職編即可投票，**無密碼**
- GitHub 用 **Private** 儲存庫
- **不要** commit `appsettings.Local.json`
- 試用結束可刪除 Azure 資源群組 `ttriticket-rg` 停止計費

---

## 刪除資源（試用結束）

Azure Portal → **資源群組** → `ttriticket-rg` → **刪除資源群組**

會一併刪除 Web App 與相關資源。
