window.TtriEmployeeId = {
  normalize(value) {
    let trimmed = String(value || '').trim().replace(/^['"]+/, '');
    if (!trimmed) return '';

    const slashIndex = trimmed.lastIndexOf('/');
    if (slashIndex >= 0 && slashIndex < trimmed.length - 1) {
      trimmed = trimmed.slice(slashIndex + 1).trim();
    }
    if (!trimmed) return '';

    if (/[A-Za-z]/.test(trimmed)) {
      // 去掉數字部分的前導零，例如 C0010 → C10、D0596 → D596
      return trimmed.replace(/^([A-Za-z]+)(0+)(\d+)$/, '$1$3');
    }

    const number = Number(trimmed);
    return Number.isFinite(number) ? String(Math.round(number)) : trimmed;
  },

  equals(left, right) {
    const a = this.normalize(left);
    const b = this.normalize(right);
    if (!a || !b) return false;
    return a.toUpperCase() === b.toUpperCase();
  }
};

window.TtriSession = {
  key: 'ttriticket_employeeId',

  get() {
    return sessionStorage.getItem(this.key) || '';
  },

  set(employeeId) {
    sessionStorage.setItem(this.key, String(employeeId).trim());
  },

  clear() {
    sessionStorage.removeItem(this.key);
  }
};
