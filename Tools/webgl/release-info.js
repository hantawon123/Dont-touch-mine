(() => {
  const parts = location.pathname.split('/').filter(Boolean);
  const revision = parts[parts.indexOf('releases') + 1];
  const notice = document.createElement('button');
  notice.textContent = '새 버전이 있습니다. 게임을 나가고 업데이트';
  notice.style.cssText = 'position:fixed;top:8px;left:8px;z-index:2147483647;padding:12px;display:none';
  notice.onclick = () => {
    if (confirm('현재 게임에서 나가고 최신 버전을 실행할까요?')) location.href = '/play/';
  };
  document.body.appendChild(notice);
  async function check() {
    try {
      const response = await fetch('/play/current.json', {cache: 'no-store'});
      if (!response.ok) return;
      const latest = await response.json();
      notice.style.display = /^[a-f0-9]{40}$/.test(latest.revision) && latest.revision !== revision ? 'block' : 'none';
    } catch { /* A failed version check must not interrupt an active game. */ }
  }
  setInterval(check, 60000);
  check();
})();
