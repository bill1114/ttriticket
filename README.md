# TtriTicket 投票系統

ASP.NET Core MVC 投票系統，候選人資料（照片、姓名、介紹）從 **Google 表單** 連結的試算表自動讀取。

## 功能

- 從 Google 表單回應試算表載入候選人
- 卡片式候選人展示（照片、姓名、介紹）
- 一人一票投票（可於設定中調整）
- 即時投票結果與百分比長條圖

## 系統需求

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

## 快速開始

```bash
cd TtriTicket
dotnet restore
dotnet run
```

瀏覽器開啟：`https://localhost:5001`

## Google 表單設定

本專案已對應你的表單欄位：

| 表單欄位 | 用途 |
|----------|------|
| 姓名/職編 | 候選人姓名 |
| 請上傳投稿照片… | 候選人照片（Google Drive 連結） |
| 請以20字內短文… | 候選人介紹 |

表單網址：`https://docs.google.com/forms/d/1z7E_xuu7tKTpIzKZwf-x5xinXsUIJIDACoswIJw2Wpk`

### 1. 取得試算表 ID（必要）

系統無法直接用表單網址讀資料，需從**連結的試算表**讀取：

1. 開啟表單 →「回應」分頁
2. 點右上角綠色圖示 **「在試算表中查看」**
3. 從試算表網址複製 ID：
   ```
   https://docs.google.com/spreadsheets/d/{這段就是SpreadsheetId}/edit
   ```

### 2. 設定試算表為公開可讀

1. 試算表右上角「共用」
2. 一般存取權 →「知道連結的使用者」→ **檢視者**

### 3. 修改 `appsettings.json`

```json
"GoogleSheets": {
  "SpreadsheetId": "貼上你的試算表ID",
  "SheetGid": "0",
  "NameColumn": "姓名/職編",
  "IntroductionColumn": "請以20字內短文",
  "PhotoColumn": "請上傳投稿照片"
}
```

> 欄位名稱只需填**關鍵字**即可，系統會自動比對完整題目（例如「請上傳投稿照片 比例1:1…」）。

### 4. 照片顯示注意事項

- 表單上傳的照片會存到 Google Drive，試算表中會是連結
- 若照片無法顯示，請確認 Drive 檔案權限為「知道連結的使用者均可檢視」

### 5. 取得 Sheet Gid（多工作表時）

若回應不在第一個工作表，開啟該工作表後從網址取得 `gid` 參數：
```
https://docs.google.com/spreadsheets/d/xxx/edit#gid=123456789
```

## 專案架構（MVC）

```
TtriTicket/
├── Controllers/
│   ├── VoteController.cs    # 投票與結果
│   └── HomeController.cs
├── Models/
│   ├── Candidate.cs         # 候選人
│   └── VoteResultViewModel.cs
├── Services/
│   ├── GoogleSheetsCandidateService.cs  # 讀取 Google 試算表
│   └── VoteService.cs                   # 投票邏輯
├── Views/
│   └── Vote/
│       ├── Index.cshtml     # 候選人列表與投票
│       └── Results.cshtml   # 投票結果
└── wwwroot/css/site.css
```

## 注意事項

- 未設定 `SpreadsheetId` 時會顯示**示範資料**，方便本地開發測試
- 投票資料目前儲存在記憶體中，重啟應用程式後會清空；正式環境建議改用資料庫
- 防重複投票以 IP + User-Agent 判斷，非絕對安全；正式活動可改為登入驗證

## 後續可擴充

- SQLite / SQL Server 持久化投票紀錄
- Google OAuth 登入限制投票資格
- 管理後台手動同步候選人
- 投票截止時間與活動開關
