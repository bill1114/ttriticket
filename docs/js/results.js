(function () {
  const resultsContainer = document.getElementById('results-container');
  const totalVotesEl = document.getElementById('total-votes');
  const navUser = document.getElementById('nav-user');
  const btnLogout = document.getElementById('btn-logout');

  function applyVoteCounts(candidates, stats) {
    return candidates.map((candidate) => {
      let voteCount = stats.voteCounts[candidate.id] || 0;
      if (!voteCount && candidate.name) {
        voteCount = stats.voteCountsByName[candidate.name.toUpperCase()] || 0;
      }
      return { ...candidate, voteCount };
    }).sort((a, b) => b.voteCount - a.voteCount);
  }

  function escapeHtml(text) {
    return String(text)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  async function loadResults() {
    resultsContainer.innerHTML = '<p>載入中…</p>';
    const employeeId = TtriSession.get();

    try {
      const [candidateResult, statsResult] = await Promise.all([
        TtriApi.getCandidates(),
        TtriApi.getStats(employeeId)
      ]);

      if (!candidateResult.success || !statsResult.success) {
        throw new Error(candidateResult.error || statsResult.error || '讀取失敗');
      }

      const stats = statsResult.stats;
      const list = applyVoteCounts(candidateResult.candidates || [], stats);
      totalVotesEl.textContent = '總票數：' + (stats.totalVotes || 0);

      if (!list.length) {
        resultsContainer.innerHTML = '<div class="empty-state"><p>尚無投票資料。</p></div>';
        return;
      }

      const maxVotes = Math.max(...list.map((c) => c.voteCount), 0);
      resultsContainer.className = 'results-list';
      resultsContainer.innerHTML = list.map((candidate, index) => {
        const percentage = stats.totalVotes > 0
          ? (candidate.voteCount / stats.totalVotes * 100).toFixed(1)
          : '0.0';
        const barWidth = maxVotes > 0
          ? (candidate.voteCount / maxVotes * 100).toFixed(1)
          : '0.0';
        const photo = candidate.photoUrl
          ? `<img src="${escapeHtml(candidate.photoUrl)}" alt="${escapeHtml(candidate.name)}" loading="lazy" onerror="this.onerror=null;this.src='images/photo-unavailable.svg';" />`
          : '';

        return `
          <article class="result-item">
            <div class="result-rank">#${index + 1}</div>
            <div class="result-photo">${photo}</div>
            <div class="result-info">
              <div class="result-header">
                <h2>${escapeHtml(candidate.name)}</h2>
                <span class="result-votes">${candidate.voteCount} 票 (${percentage}%)</span>
              </div>
              <div class="progress-bar"><div class="progress-fill" style="width:${barWidth}%"></div></div>
              <p class="introduction">${escapeHtml(candidate.introduction || '')}</p>
            </div>
          </article>`;
      }).join('');
    } catch (error) {
      resultsContainer.className = 'empty-state';
      resultsContainer.innerHTML = '<p>載入失敗：' + escapeHtml(error.message) + '</p>';
    }
  }

  if (TtriSession.get()) {
    navUser.textContent = TtriSession.get();
    navUser.classList.remove('hidden');
    btnLogout.classList.remove('hidden');
  }

  btnLogout.addEventListener('click', () => {
    TtriSession.clear();
    window.location.href = 'index.html';
  });

  TtriApi.warmup();
  document.getElementById('btn-refresh').addEventListener('click', loadResults);
  loadResults();
})();
