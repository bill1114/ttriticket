window.TtriApi = {
  jsonp(action, params = {}) {
    const config = window.TTRI_CONFIG;
    if (!config || !config.webAppUrl || config.webAppUrl.includes('你的部署ID')) {
      return Promise.reject(new Error('請先在 js/config.js 設定 webAppUrl'));
    }

    return new Promise((resolve, reject) => {
      const callback = 'ttricb_' + Date.now();
      const url = new URL(config.webAppUrl);
      url.searchParams.set('action', action);
      Object.entries(params).forEach(([key, value]) => {
        if (value !== undefined && value !== null && value !== '') {
          url.searchParams.set(key, value);
        }
      });
      url.searchParams.set('callback', callback);

      window[callback] = (data) => {
        delete window[callback];
        script.remove();
        resolve(data);
      };

      const script = document.createElement('script');
      script.src = url.toString();
      script.onerror = () => {
        delete window[callback];
        script.remove();
        reject(new Error('無法連線 Apps Script，請確認已重新部署腳本'));
      };
      document.body.appendChild(script);
    });
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
    return this.jsonp('candidates');
  },

  getStats(employeeId) {
    return this.jsonp('stats', { employeeId: TtriEmployeeId.normalize(employeeId) });
  }
};
