// Validation-only: load before Unity in a local WebGL build's index.html.
// No request is swallowed or redirected. A queued Escape request fails the assertion.
(() => {
  let gesture = null;
  const nativeRequest = Element.prototype.requestPointerLock;
  for (const type of ['pointerdown', 'keydown', 'keyup', 'click']) {
    document.addEventListener(type, event => {
      gesture = type;
      queueMicrotask(() => { gesture = null; });
      if (event.key === 'Escape') console.log('[PointerQA] ' + type + ' Escape');
    }, true);
  }
  Element.prototype.requestPointerLock = function (...args) {
    // Microtasks can run between DOM listeners, so use the browser's current event.
    const current = window.event ? window.event.type : gesture;
    console.assert(current === 'pointerdown', '[PointerQA] FAIL: lock outside gameplay pointerdown: ' + current);
    console.log('[PointerQA] request event=' + current + ' active=' + navigator.userActivation.isActive);
    return nativeRequest.apply(this, args);
  };
  document.addEventListener('pointerlockchange', () => {
    console.log('[PointerQA] locked=' + !!document.pointerLockElement);
  });
  window.addEventListener('unhandledrejection', event => {
    console.error('[PointerQA] unhandled ' + event.reason?.name + ': ' + event.reason?.message);
  });
})();
