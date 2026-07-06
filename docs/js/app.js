(function () {
  const loginSection = document.getElementById('login-section');
  const voteSection = document.getElementById('vote-section');
  const alertBox = document.getElementById('alert-box');
  const connectionAlert = document.getElementById('connection-alert');
  const candidateContainer = document.getElementById('candidate-container');
  const navUser = document.getElementById('nav-user');
  const btnLogout = document.getElementById('btn-logout');
  const pageTitle = document.getElementById('page-title');
  const pageSubtitle = document.getElementById('page-subtitle');

  const MAX_VOTES = 3;
  let candidates = [];
  let stats = { totalVotes: 0, voteCounts: {}, voteCountsByName: {}, voteCount: 0 };

  function showAlert(message, type) {
    alertBox.className = 'alert alert-' + type;
    alertBox.textContent = message;
    alertBox.classList.remove('hidden');
  }

  function hideAlert() {
    alertBox.classList.add('hidden');
  }

  function applyVoteCounts(list, voteStats) {
    list.forEach((candidate) => {
      let count = voteStats.voteCounts[candidate.id] || 0;
      if (!count && candidate.name) {
        const key = candidate.name.toUpperCase();
        count = voteStats.voteCountsByName[key] || 0;
      }
      candidate.voteCount = count;
    });
    return list.sort((a, b) => b.voteCount - a.voteCount);
  }

  function renderCandidates() {
    const employeeId = TtriSession.get();
    const hasVoted = (stats.voteCount || 0) >= MAX_VOTES;
    const remaining = MAX_VOTES - (stats.voteCount || 0);
    const sorted = applyVoteCounts([...candidates], stats);

    if (!sorted.length) {
      candidateContainer.innerHTML = '<div class="empty-state"><p>目前尚無候選人資料。</p></div>';
      return;
    }

    candidateContainer.innerHTML = '<div class="candidate-grid">' + sorted.map((candidate) => {
      const photo = candidate.photoUrl
        ? `<img src="${escapeHtml(candidate.photoUrl)}" alt="${escapeHtml(candidate.name)}" loading="lazy" onerror="this.onerror=null;this.src='images/photo-unavailable.svg';" />`
        : '<div class="photo-placeholder">無照片</div>';

      const button = hasVoted
        ? '<button type="button" class="btn btn-disabled" disabled>已達投票上限</button>'
        : `<button type="button" class="btn btn-primary btn-vote" data-id="${candidate.id}" data-name="${escapeAttr(candidate.name)}">投票（剩餘 ${remaining} 票）</button>`;

      return `
        <article class="candidate-card">
          <div class="candidate-photo">${photo}</div>
          <div class="candidate-body">
            <h2>${escapeHtml(candidate.name)}</h2>
            <p class="introduction">${escapeHtml(candidate.introduction || '')}</p>
            <div class="vote-meta"><span class="vote-count">${candidate.voteCount || 0} 票</span></div>
            ${button}
          </div>
        </article>`;
    }).join('') + '</div>';

    candidateContainer.querySelectorAll('.btn-vote').forEach((btn) => {
      btn.addEventListener('click', () => castVote(btn.dataset.id, btn.dataset.name));
    });
  }

  async function loadVotePage() {
    hideAlert();
    const employeeId = TtriSession.get();
    pageTitle.textContent = window.TTRI_CONFIG.title || '投票系統';
    pageSubtitle.textContent = `職編 ${employeeId}｜目前總票數：${stats.totalVotes}`;

    try {
      const [candidateResult, statsResult] = await Promise.all([
        TtriApi.getCandidates(),
        TtriApi.getStats(employeeId)
      ]);

      if (!candidateResult.success) {
        throw new Error(candidateResult.error || '讀取候選人失敗');
      }
      if (!statsResult.success) {
        throw new Error(statsResult.error || '讀取票數失敗');
      }

      candidates = candidateResult.candidates || [];
      stats = statsResult.stats || stats;

      const used = stats.voteCount || 0;
      const remaining = MAX_VOTES - used;
      connectionAlert.className = 'alert alert-success';
      connectionAlert.innerHTML = `<strong>Google 試算表串接成功</strong> — 已載入 ${candidates.length} 位候選人，您已投 ${used} 票，剩餘 ${remaining} 票`;
      connectionAlert.classList.remove('hidden');
      renderCandidates();
    } catch (error) {
      connectionAlert.className = 'alert alert-error';
      connectionAlert.textContent = 'Google 試算表串接失敗：' + error.message;
      connectionAlert.classList.remove('hidden');
      candidateContainer.innerHTML = '';
    }
  }

  async function castVote(candidateId, candidateName) {
    const employeeId = TtriSession.get();
    const message = `確定要投票給「${candidateName}」嗎？\n\n投票後無法更改。`;
    if (!confirm(message)) {
      return;
    }

    try {
      const result = await TtriApi.postVote({
        employeeId: TtriEmployeeId.normalize(employeeId),
        candidateId: Number(candidateId),
        candidateName
      });

      if (!result.success) {
        throw new Error(result.error || '投票失敗');
      }

      // 直接更新本地狀態，不重新打 API
      stats.voteCount = (stats.voteCount || 0) + 1;
      stats.totalVotes = (stats.totalVotes || 0) + 1;
      const id = Number(candidateId);
      stats.voteCounts[id] = (stats.voteCounts[id] || 0) + 1;
      const nameKey = candidateName.toUpperCase();
      stats.voteCountsByName[nameKey] = (stats.voteCountsByName[nameKey] || 0) + 1;

      pageSubtitle.textContent = `職編 ${employeeId}｜目前總票數：${stats.totalVotes}`;
      showAlert(`已成功投票給 ${candidateName}！`, 'success');
      renderCandidates();
    } catch (error) {
      const msg = error.message === '已投票' ? '您已經投過票了。' : ('投票失敗：' + error.message);
      showAlert(msg, 'error');
    }
  }

  function showLogin() {
    loginSection.classList.remove('hidden');
    voteSection.classList.add('hidden');
    navUser.classList.add('hidden');
    btnLogout.classList.add('hidden');
  }

  function showVote() {
    const employeeId = TtriSession.get();
    loginSection.classList.add('hidden');
    voteSection.classList.remove('hidden');
    navUser.textContent = employeeId + ' (' + employeeId + ')';
    navUser.classList.remove('hidden');
    btnLogout.classList.remove('hidden');
    loadVotePage();
  }

  function escapeHtml(text) {
    return String(text)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  function escapeAttr(text) {
    return escapeHtml(text).replace(/'/g, '&#39;');
  }

  document.getElementById('btn-login').addEventListener('click', () => {
    const input = document.getElementById('EmployeeId');
    if (!input.value.trim()) {
      alert('請輸入職編後再開始投票。');
      input.focus();
      return;
    }
    const normalized = TtriEmployeeId.normalize(input.value);
    if (window.TTRI_EMPLOYEES && !window.TTRI_EMPLOYEES.has(normalized.toUpperCase())) {
      showAlert('此職編不在職工名單中，無法投票。', 'error');
      input.focus();
      return;
    }
    TtriSession.set(input.value);
    showVote();
  });

  document.getElementById('EmployeeId').addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      document.getElementById('btn-login').click();
    }
  });

  btnLogout.addEventListener('click', () => {
    TtriSession.clear();
    showLogin();
  });

  document.getElementById('btn-refresh').addEventListener('click', loadVotePage);

  // 預熱 GAS，讓使用者輸入職編期間就完成冷啟動
  TtriApi.warmup();

  if (TtriSession.get()) {
    showVote();
  } else {
    showLogin();
  }
})();
