# Google 試算表投票紀錄設定

投票紀錄改為儲存在 **Google 試算表**（不再使用 SQLite）。

- **讀取**票數 / 是否已投票：透過試算表 CSV 匯出（公開可讀）
- **寫入**投票紀錄：透過 **Google Apps Script** 網址（Webhook）

---

## 步驟 1：建立「投票紀錄」工作表

在同一個試算表（或新試算表）新增工作表，命名為 **`投票紀錄`**。

第一列標題請設為：

| 投票時間 | 職編 | 候選人ID | 候選人姓名 |
|----------|------|----------|------------|

從網址取得此工作表的 **gid**：
```
https://docs.google.com/spreadsheets/d/xxx/edit#gid=【這段數字】
```

---

## 步驟 2：部署 Google Apps Script（寫入投票）

1. 試算表 → **擴充功能** → **Apps Script**
2. 貼上 `docs/google-apps-script-votes.gs` 內容
3. 點 **部署** → **新增部署作業**
4. 類型：**網路應用程式**
5. 執行身分：**我**
6. 存取權：**任何人**
7. 複製產生的 **網路應用程式 URL**

---

## 步驟 3：修改 `appsettings.json` 或 `appsettings.Local.json`

```json
"GoogleSheets": {
  "SpreadsheetId": "1AQGEom8myrDYbQBCJPAYIvx9DdkEnFBgtkAmbR0rdpQ",
  "VotesSheetGid": "你的投票紀錄工作表gid",
  "VoteAppendWebhookUrl": "https://script.google.com/macros/s/xxxx/exec"
}
```

---

## 運作方式

| 功能 | 說明 |
|------|------|
| 登入 | 輸入職編即可，**不需白名單** |
| 投票 | 寫入「投票紀錄」工作表 |
| 防重複 | 同一職編已有紀錄則無法再投 |
| 重啟程式 | 資料仍在 Google 試算表 |

---

## 試算表欄位對應（CSV 讀取）

```sql
-- 邏輯結構（實際為 Google 試算表，非 SQL 資料庫）
投票時間 | 職編 | 候選人ID | 候選人姓名
```

查詢票數：在試算表中對「候選人姓名」做 COUNT 即可。

---

## 注意事項

- 試算表需設為 **知道連結的使用者 → 檢視者**（讀取票數）
- Apps Script 部署者需有試算表 **編輯權限**（寫入投票）
- 修改 `VoteAppendWebhookUrl` 後需重新啟動網站
