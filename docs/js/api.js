window.TtriApi = {
  _candidateCache: null,
  _candidateCacheTime: 0,
  CANDIDATE_TTL: 5 * 60 * 1000, // 5 分鐘

  jsonp(action, params = {}) {
    const config = window.TTRI_CONFIG;
    if (!config || !config.webAppUrl || config.webAppUrl.includes('你的部署ID')) {
      return Promise.reject(new Error('請先在 js/config.js 設定 webAppUrl'));
    }

    return new Promise((resolve, reject) => {
      const callback = 'ttricb_' + Date.now() + '_' + Math.random().toString(36).slice(2);
      const url = new URL(config.webAppUrl);
      url.searchParams.set('action', action);
      Object.entries(params).forEach(([key, value]) => {
        if (value !== undefined && value !== null && value !== '') {
          url.searchParams.set(key, value);
        }
      });
      url.searchParams.set('callback', callback);

      const timer = setTimeout(() => {
        delete window[callback];
        script.remove();
        reject(new Error('連線逾時，請稍後再試'));
      }, 15000);

      window[callback] = (data) => {
        clearTimeout(timer);
        delete window[callback];
        script.remove();
        resolve(data);
      };

      const script = document.createElement('script');
      script.src = url.toString();
      script.onerror = () => {
        clearTimeout(timer);
        delete window[callback];
        script.remove();
        reject(new Error('無法連線 Apps Script，請確認已重新部署腳本'));
      };
      document.body.appendChild(script);
    });
  },

  // 頁面載入時預熱 GAS（不等結果）
  warmup() {
    this.jsonp('ping').catch(() => {});
  },

  async postVote(payload) {
    const config = window.TTRI_CONFIG;
    const response = await fetch(config.webAppUrl, {
      method: 'POST',
      redirect: 'follow',
      headers: { 'Content-Type': 'text/plain;charset=utf-8' },
      body: JSON.stringify(payload)
    });

    const text = await response.text();
    try {
      return JSON.parse(text);
    } catch {
      throw new Error(text || '投票失敗');
    }
  },

  getCandidates() {
    // 快取候選人資料，避免重複呼叫
    const now = Date.now();
    if (this._candidateCache && (now - this._candidateCacheTime) < this.CANDIDATE_TTL) {
      return Promise.resolve(this._candidateCache);
    }
    return this.jsonp('candidates').then((result) => {
      if (result.success) {
        this._candidateCache = result;
        this._candidateCacheTime = now;
      }
      return result;
    });
  },

  getStats(employeeId) {
    return this.jsonp('stats', { employeeId: TtriEmployeeId.normalize(employeeId) });
  }
};
