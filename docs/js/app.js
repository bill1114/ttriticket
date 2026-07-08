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

  const confirmModal = document.getElementById('confirm-modal');
  const modalBodyText = document.getElementById('modal-body-text');
  const modalConfirm = document.getElementById('modal-confirm');
  const modalCancel = document.getElementById('modal-cancel');

  const MAX_VOTES = 3;
  let candidates = [];
  let stats = { totalVotes: 0, voteCounts: {}, voteCountsByName: {}, voteCount: 0, votedCandidateIds: [] };
  let selectedIds = new Set(); // 當次選取的候選人 id

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

  function updateSelectCounter() {
    const counter = document.getElementById('select-counter');
    const submitBtn = document.getElementById('btn-submit-votes');
    if (!counter || !submitBtn) return;
    const remaining = MAX_VOTES - (stats.voteCount || 0);
    const canSelect = Math.min(remaining, 3);
    counter.textContent = `已選 ${selectedIds.size} / ${canSelect} 人（本次最多可選 ${canSelect} 人）`;
    submitBtn.disabled = selectedIds.size === 0;
  }

  function renderCandidates() {
    const hasReachedLimit = (stats.voteCount || 0) >= MAX_VOTES;
    const remaining = MAX_VOTES - (stats.voteCount || 0);
    const votedIds = new Set((stats.votedCandidateIds || []).map(Number));
    const sorted = applyVoteCounts([...candidates], stats);

    if (!sorted.length) {
      candidateContainer.innerHTML = '<div class="empty-state"><p>目前尚無候選人資料。</p></div>';
      return;
    }

    const canSelect = Math.min(remaining, 3);

    let html = `<p id="select-counter" class="select-counter"></p><div class="candidate-grid">`;

    sorted.forEach((candidate, index) => {
      const photo = candidate.photoUrl
        ? `<img src="${escapeHtml(candidate.photoUrl)}" alt="候選人 ${index + 1}" loading="lazy" onerror="this.onerror=null;this.src='images/photo-unavailable.svg';" />`
        : '<div class="photo-placeholder">無照片</div>';

      const alreadyVotedThis = votedIds.has(Number(candidate.id));
      const isSelected = selectedIds.has(Number(candidate.id));

      let buttonHtml;
      if (hasReachedLimit) {
        buttonHtml = '<button type="button" class="btn btn-disabled" disabled>已達投票上限</button>';
      } else if (alreadyVotedThis) {
        buttonHtml = '<button type="button" class="btn btn-voted-this" disabled>已投過此人</button>';
      } else {
        const selectedClass = isSelected ? ' btn-selected' : '';
        buttonHtml = `<button type="button" class="btn btn-primary btn-select${selectedClass}" data-id="${candidate.id}">
          ${isSelected ? '✓ 已選取' : '選取'}
        </button>`;
      }

      const selectedClass = isSelected ? ' selected' : '';
      html += `
        <article class="candidate-card${selectedClass}" data-cid="${candidate.id}">
          <div class="candidate-photo">${photo}</div>
          <div class="candidate-body">
            <h2 class="candidate-label">候選人 ${candidate.id}</h2>
            <div class="vote-meta"><span class="vote-count">${candidate.voteCount || 0} 票</span></div>
            <p class="introduction">${escapeHtml(candidate.introduction || '')}</p>
            ${buttonHtml}
          </div>
        </article>`;
    });

    if (!hasReachedLimit) {
      html += `</div><button type="button" id="btn-submit-votes" class="btn btn-primary btn-submit-votes" disabled>
        確認投票（${selectedIds.size} 人）
      </button>`;
    } else {
      html += '</div>';
    }

    candidateContainer.innerHTML = html;
    updateSelectCounter();

    // 選取按鈕事件
    candidateContainer.querySelectorAll('.btn-select').forEach((btn) => {
      btn.addEventListener('click', () => {
        const id = Number(btn.dataset.id);
        if (selectedIds.has(id)) {
          selectedIds.delete(id);
        } else {
          if (selectedIds.size >= canSelect) {
            showAlert(`本次最多只能選 ${canSelect} 人。`, 'error');
            return;
          }
          selectedIds.add(id);
        }
        renderCandidates();
      });
    });

    // 確認投票按鈕事件
    const submitBtn = document.getElementById('btn-submit-votes');
    if (submitBtn) {
      submitBtn.addEventListener('click', () => castVotes());
    }
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

      if (!candidateResult.success) throw new Error(candidateResult.error || '讀取候選人失敗');
      if (!statsResult.success) throw new Error(statsResult.error || '讀取票數失敗');

      candidates = candidateResult.candidates || [];
      stats = statsResult.stats || stats;
      selectedIds.clear();

      const used = stats.voteCount || 0;
      const remaining = MAX_VOTES - used;
      connectionAlert.className = 'alert alert-success';
      connectionAlert.innerHTML = `<strong>載入成功</strong> — ${candidates.length} 位候選人，您已投 ${used} 票，剩餘 ${remaining} 票`;
      connectionAlert.classList.remove('hidden');
      renderCandidates();
    } catch (error) {
      connectionAlert.className = 'alert alert-error';
      connectionAlert.textContent = 'Google 試算表串接失敗：' + error.message;
      connectionAlert.classList.remove('hidden');
      candidateContainer.innerHTML = '';
    }
  }

  async function castVotes() {
    if (selectedIds.size === 0) return;
    const employeeId = TtriSession.get();
    const names = [...selectedIds].map(id => {
      const c = candidates.find(c => Number(c.id) === id);
      return c ? `候選人 #${id}` : `#${id}`;
    });
    if (!confirm(`確定要投票給以下 ${selectedIds.size} 人嗎？\n${names.join('\n')}\n\n投票後無法更改。`)) return;

    const ids = [...selectedIds];
    let successCount = 0;

    for (const candidateId of ids) {
      const candidate = candidates.find(c => Number(c.id) === candidateId);
      if (!candidate) continue;
      try {
        const result = await TtriApi.postVote({
          employeeId: TtriEmployeeId.normalize(employeeId),
          candidateId: Number(candidateId),
          candidateName: candidate.name
        });
        if (!result.success) {
          showAlert(`投票失敗（候選人 #${candidateId}）：${result.error}`, 'error');
          break;
        }
        // 樂觀更新本地狀態
        stats.voteCount = (stats.voteCount || 0) + 1;
        stats.totalVotes = (stats.totalVotes || 0) + 1;
        stats.voteCounts[candidateId] = (stats.voteCounts[candidateId] || 0) + 1;
        const nameKey = candidate.name.toUpperCase();
        stats.voteCountsByName[nameKey] = (stats.voteCountsByName[nameKey] || 0) + 1;
        if (!stats.votedCandidateIds) stats.votedCandidateIds = [];
        stats.votedCandidateIds.push(candidateId);
        successCount++;
      } catch (error) {
        showAlert('投票失敗：' + error.message, 'error');
        break;
      }
    }

    selectedIds.clear();
    if (successCount > 0) {
      showAlert(`已成功投出 ${successCount} 票！`, 'success');
      pageSubtitle.textContent = `職編 ${employeeId}｜目前總票數：${stats.totalVotes}`;
    }
    renderCandidates();
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
    navUser.textContent = employeeId;
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

  // 身分確認 Modal
  function showConfirmModal(employeeId, onConfirm) {
    const info = window.TTRI_EMPLOYEE_MAP && window.TTRI_EMPLOYEE_MAP[employeeId.toUpperCase()];
    if (info) {
      modalBodyText.textContent = `部門：${info.dept}　姓名：${info.name}`;
    } else {
      modalBodyText.textContent = `職編：${employeeId}`;
    }
    confirmModal.classList.remove('hidden');

    const doConfirm = () => {
      confirmModal.classList.add('hidden');
      modalConfirm.removeEventListener('click', doConfirm);
      modalCancel.removeEventListener('click', doCancel);
      onConfirm();
    };
    const doCancel = () => {
      confirmModal.classList.add('hidden');
      modalConfirm.removeEventListener('click', doConfirm);
      modalCancel.removeEventListener('click', doCancel);
    };
    modalConfirm.addEventListener('click', doConfirm);
    modalCancel.addEventListener('click', doCancel);
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
    showConfirmModal(normalized, () => {
      TtriSession.set(input.value);
      showVote();
    });
  });

  document.getElementById('EmployeeId').addEventListener('keydown', (event) => {
    if (event.key === 'Enter') document.getElementById('btn-login').click();
  });

  btnLogout.addEventListener('click', () => {
    TtriSession.clear();
    selectedIds.clear();
    showLogin();
  });

  document.getElementById('btn-refresh').addEventListener('click', loadVotePage);

  TtriApi.warmup();

  if (TtriSession.get()) {
    showVote();
  } else {
    showLogin();
  }
})();
