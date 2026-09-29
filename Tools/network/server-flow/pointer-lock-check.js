// Validation-only: load before Unity in a local WebGL build's index.html.
// No request is swallowed or redirected. Fullscreen keyboard resume is allowed.
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
    const fullscreenResume = !!document.fullscreenElement && navigator.userActivation.isActive;
    console.assert(current === 'pointerdown' || fullscreenResume, '[PointerQA] FAIL: lock without gameplay click or active fullscreen resume: ' + current);
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
