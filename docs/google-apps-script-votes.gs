/**
 * TtriTicket 投票寫入腳本
 * 部署為「網路應用程式」，存取權：任何人
 *
 * 重要：請用 openById 指定試算表（獨立腳本專案無法用 getActiveSpreadsheet）
 */
var SPREADSHEET_ID = '1AQGEom8myrDYbQBCJPAYIvx9DdkEnFBgtkAmbR0rdpQ';
var VOTE_SHEET_NAME = '投票紀錄';

/** 用瀏覽器開啟部署網址時顯示（僅供測試，實際投票由網站 POST） */
function doGet() {
  return jsonResponse({
    success: true,
    message: 'TtriTicket 投票寫入服務已就緒。請由投票網站 POST 寫入，勿直接用瀏覽器投票。'
  });
}

function doPost(e) {
  try {
    if (!e || !e.postData || !e.postData.contents) {
      return jsonResponse({ success: false, error: '沒有收到 POST 資料' });
    }

    var data = JSON.parse(e.postData.contents);
    var employeeId = String(data.employeeId || '').trim();
    var candidateId = data.candidateId;
    var candidateName = String(data.candidateName || '').trim();

    if (!employeeId) {
      return jsonResponse({ success: false, error: '職編不可為空' });
    }

    var ss = SpreadsheetApp.openById(SPREADSHEET_ID);
    var sheet = ss.getSheetByName(VOTE_SHEET_NAME);
    if (!sheet) {
      return jsonResponse({ success: false, error: '找不到「' + VOTE_SHEET_NAME + '」工作表' });
    }

    // 職編欄固定為純文字（596 與 D596 為不同人，不可被試算表轉成數字）
    sheet.getRange('B:B').setNumberFormat('@');

    var lastRow = sheet.getLastRow();
    if (lastRow >= 2) {
      var employeeIds = sheet.getRange('B2:B' + lastRow).getDisplayValues();
      for (var i = 0; i < employeeIds.length; i++) {
        var existingId = String(employeeIds[i][0]).trim();
        if (employeeIdsEqual(existingId, employeeId)) {
          return jsonResponse({ success: false, error: '已投票' });
        }
      }
    }

    var newRow = lastRow + 1;
    sheet.getRange(newRow, 1).setValue(new Date());
    sheet.getRange(newRow, 2).setNumberFormat('@').setValue(employeeId);
    sheet.getRange(newRow, 3).setValue(candidateId);
    sheet.getRange(newRow, 4).setValue(candidateName);

    return jsonResponse({ success: true });
  } catch (err) {
    return jsonResponse({ success: false, error: String(err) });
  }
}

function jsonResponse(obj) {
  return ContentService
    .createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}

/** 596 與 D596 為不同人；英數職編僅忽略大小寫（D596 = d596） */
function employeeIdsEqual(a, b) {
  var left = String(a || '').trim();
  var right = String(b || '').trim();
  if (!left || !right) {
    return false;
  }
  return left.toUpperCase() === right.toUpperCase();
}
