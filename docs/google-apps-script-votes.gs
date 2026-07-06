/**
 * TtriTicket：Google 試算表 API（讀候選人 / 讀票數 / 寫投票）
 * 部署為「網路應用程式」，存取權：任何人
 *
 * 靜態網站（GitHub Pages）透過此腳本串接試算表。
 */
var SPREADSHEET_ID = '1AQGEom8myrDYbQBCJPAYIvx9DdkEnFBgtkAmbR0rdpQ';
var CANDIDATES_GID = 736958374;
var VOTE_SHEET_NAME = '投票紀錄';

var NAME_COLUMN_KEY = '姓名/職編';
var INTRO_COLUMN_KEY = '請以20字內短文';
var PHOTO_COLUMN_KEY = '請上傳投稿照片';

function doGet(e) {
  try {
    var action = (e && e.parameter && e.parameter.action) || 'ping';
    var payload;

    if (action === 'ping') {
      payload = {
        success: true,
        message: 'TtriTicket API 已就緒（候選人 / 票數 / 投票）'
      };
    } else if (action === 'candidates') {
      payload = { success: true, candidates: getCandidates_() };
    } else if (action === 'stats') {
      var employeeId = (e.parameter.employeeId || '').trim();
      payload = { success: true, stats: getVoteStats_(employeeId) };
    } else {
      payload = { success: false, error: '未知的 action: ' + action };
    }

    return respond_(payload, e);
  } catch (err) {
    return respond_({ success: false, error: String(err) }, e);
  }
}

function doPost(e) {
  try {
    if (!e || !e.postData || !e.postData.contents) {
      return respond_({ success: false, error: '沒有收到 POST 資料' }, e);
    }

    var data = JSON.parse(e.postData.contents);
    var employeeId = String(data.employeeId || '').trim();
    var candidateId = data.candidateId;
    var candidateName = String(data.candidateName || '').trim();

    if (!employeeId) {
      return respond_({ success: false, error: '職編不可為空' }, e);
    }

    var ss = SpreadsheetApp.openById(SPREADSHEET_ID);
    var sheet = ss.getSheetByName(VOTE_SHEET_NAME);
    if (!sheet) {
      return respond_({ success: false, error: '找不到「' + VOTE_SHEET_NAME + '」工作表' }, e);
    }

    sheet.getRange('B:B').setNumberFormat('@');

    var MAX_VOTES = 3;
    var lastRow = sheet.getLastRow();
    if (lastRow >= 2) {
      var existingRows = sheet.getRange(2, 1, lastRow - 1, 4).getDisplayValues();
      var voteCount = 0;
      for (var i = 0; i < existingRows.length; i++) {
        var existingId = String(existingRows[i][1]).trim();
        if (employeeIdsEqual(existingId, employeeId)) {
          voteCount++;
          var existingCandidateId = parseSheetInt_(existingRows[i][2]);
          if (existingCandidateId === Number(candidateId)) {
            return respond_({ success: false, error: '不能重複投票給同一位候選人' }, e);
          }
        }
      }
      if (voteCount >= MAX_VOTES) {
        return respond_({ success: false, error: '已達投票上限（最多 ' + MAX_VOTES + ' 票）' }, e);
      }
    }

    var newRow = lastRow + 1;
    sheet.getRange(newRow, 1).setValue(new Date());
    sheet.getRange(newRow, 2).setNumberFormat('@').setValue(employeeId);
    sheet.getRange(newRow, 3).setValue(candidateId);
    sheet.getRange(newRow, 4).setValue(candidateName);

    return respond_({ success: true }, e);
  } catch (err) {
    return respond_({ success: false, error: String(err) }, e);
  }
}

function getCandidates_() {
  var sheet = getSheetByGid_(SPREADSHEET_ID, CANDIDATES_GID);
  var values = sheet.getDataRange().getValues();
  if (!values.length) {
    return [];
  }

  var headers = values[0].map(String);
  var candidates = [];
  var id = 1;

  for (var r = 1; r < values.length; r++) {
    var row = values[r];
    var name = getCellByHeader_(headers, row, NAME_COLUMN_KEY);
    if (!name) {
      continue;
    }

    candidates.push({
      id: id++,
      name: name,
      introduction: getCellByHeader_(headers, row, INTRO_COLUMN_KEY),
      photoUrl: normalizePhotoUrl_(getCellByHeader_(headers, row, PHOTO_COLUMN_KEY))
    });
  }

  return candidates;
}

function getVoteStats_(employeeId) {
  var ss = SpreadsheetApp.openById(SPREADSHEET_ID);
  var sheet = ss.getSheetByName(VOTE_SHEET_NAME);
  if (!sheet) {
    return { totalVotes: 0, voteCounts: {}, voteCountsByName: {}, hasVoted: false };
  }

  var lastRow = sheet.getLastRow();
  var voteCounts = {};
  var voteCountsByName = {};
  var totalVotes = 0;
  var myVoteCount = 0;
  var normalizedEmployeeId = normalizeEmployeeId_(employeeId);

  if (lastRow < 2) {
    return {
      totalVotes: 0,
      voteCounts: voteCounts,
      voteCountsByName: voteCountsByName,
      voteCount: 0
    };
  }

  var votedCandidateIds = [];
  var rows = sheet.getRange(2, 1, lastRow - 1, 4).getDisplayValues();
  for (var i = 0; i < rows.length; i++) {
    var rowEmployeeId = normalizeEmployeeId_(rows[i][1]);
    var candidateId = parseSheetInt_(rows[i][2]);
    var candidateName = String(rows[i][3] || '').trim();

    if (!rowEmployeeId) {
      continue;
    }

    totalVotes++;
    if (candidateId > 0) {
      voteCounts[candidateId] = (voteCounts[candidateId] || 0) + 1;
    }
    if (candidateName) {
      var nameKey = candidateName.toUpperCase();
      voteCountsByName[nameKey] = (voteCountsByName[nameKey] || 0) + 1;
    }
    if (normalizedEmployeeId && employeeIdsEqual(rowEmployeeId, normalizedEmployeeId)) {
      myVoteCount++;
      if (candidateId > 0) {
        votedCandidateIds.push(candidateId);
      }
    }
  }

  return {
    totalVotes: totalVotes,
    voteCounts: voteCounts,
    voteCountsByName: voteCountsByName,
    voteCount: myVoteCount,
    votedCandidateIds: votedCandidateIds
  };
}

function getSheetByGid_(spreadsheetId, gid) {
  var ss = SpreadsheetApp.openById(spreadsheetId);
  var sheets = ss.getSheets();
  for (var i = 0; i < sheets.length; i++) {
    if (sheets[i].getSheetId() === Number(gid)) {
      return sheets[i];
    }
  }
  throw new Error('找不到 gid=' + gid + ' 的工作表');
}

function getCellByHeader_(headers, row, keyword) {
  var index = findHeaderIndex_(headers, keyword);
  if (index < 0) {
    return '';
  }
  return String(row[index] || '').trim();
}

function findHeaderIndex_(headers, keyword) {
  var key = String(keyword).toUpperCase();
  for (var i = 0; i < headers.length; i++) {
    var header = String(headers[i]).trim().toUpperCase();
    if (header === key || header.indexOf(key) >= 0) {
      return i;
    }
  }
  return -1;
}

function normalizePhotoUrl_(raw) {
  if (!raw) {
    return '';
  }

  var firstUrl = String(raw).split(/[\n,;]/)[0].trim().replace(/\\/g, '');
  if (!/^https?:\/\//i.test(firstUrl)) {
    return '';
  }

  var fileId = extractDriveFileId_(firstUrl);
  if (fileId) {
    return 'https://drive.google.com/thumbnail?id=' + fileId + '&sz=w800';
  }

  return firstUrl;
}

function extractDriveFileId_(url) {
  var cleaned = String(url).trim();
  var marker = '/file/d/';
  var index = cleaned.indexOf(marker);
  if (index >= 0) {
    var start = index + marker.length;
    var end = cleaned.indexOf('/', start);
    return (end > start ? cleaned.substring(start, end) : cleaned.substring(start)).trim();
  }

  var match = cleaned.match(/(?:[?&]id=|\/d\/)([a-zA-Z0-9_-]+)/i);
  return match ? match[1] : '';
}

function parseSheetInt_(value) {
  var text = String(value || '').trim();
  if (!text) {
    return 0;
  }
  if (/^-?\d+$/.test(text)) {
    return parseInt(text, 10);
  }
  var number = parseFloat(text);
  return isNaN(number) ? 0 : Math.round(number);
}

function normalizeEmployeeId_(value) {
  var trimmed = String(value || '').trim().replace(/^['"]+/, '');
  if (!trimmed) {
    return '';
  }

  var slashIndex = trimmed.lastIndexOf('/');
  if (slashIndex >= 0 && slashIndex < trimmed.length - 1) {
    trimmed = trimmed.substring(slashIndex + 1).trim();
  }

  if (/[A-Za-z]/.test(trimmed)) {
    return trimmed;
  }

  var number = parseFloat(trimmed);
  return isNaN(number) ? trimmed : String(Math.round(number));
}

function employeeIdsEqual(a, b) {
  var left = normalizeEmployeeId_(a);
  var right = normalizeEmployeeId_(b);
  if (!left || !right) {
    return false;
  }
  return left.toUpperCase() === right.toUpperCase();
}

function respond_(obj, e) {
  var callback = e && e.parameter && e.parameter.callback;
  var json = JSON.stringify(obj);

  if (callback) {
    return ContentService
      .createTextOutput(callback + '(' + json + ')')
      .setMimeType(ContentService.MimeType.JAVASCRIPT);
  }

  return ContentService
    .createTextOutput(json)
    .setMimeType(ContentService.MimeType.JSON);
}
