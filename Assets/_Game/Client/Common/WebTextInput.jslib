mergeInto(LibraryManager.library, {
  $GameText: {
    state: null,
    close: function () {
      var s = GameText.state;
      GameText.state = null;
      if (!s) return;
      s.input.remove();
    },
    send: function (s, action, composing) {
      if (GameText.state !== s) return;
      SendMessage(s.target, 'OnBrowserEdit', JSON.stringify({
        id: s.id, value: s.input.value, action: action, composing: composing || ''}));
    },
    // maxLength does not stop an IME: a syllable composed past the limit stays on the end
    // until the field is left. Cut the overflow where it was inserted (just before the caret)
    // as soon as it appears; assigning value also ends the composition that produced it.
    clamp: function (s, limit) {
      var input = s.input, value = input.value;
      if (limit <= 0 || value.length <= limit) return false;
      var excess = value.length - limit, cut = input.selectionStart;
      var caret = cut >= excess ? cut - excess : limit;
      input.value = cut >= excess ? value.slice(0, cut - excess) + value.slice(cut) : value.slice(0, limit);
      input.setSelectionRange(caret, caret);
      return true;
    }
  },
  GameTextOpen__deps: ['$GameText'],
  GameTextOpen: function (target, options) {
    GameText.close();
    var o = JSON.parse(UTF8ToString(options));
    var input = document.createElement(o.multiline ? 'textarea' : 'input');
    var s = {input: input, target: UTF8ToString(target), id: o.id, composing: false};
    GameText.state = s;
    if (document.pointerLockElement) document.exitPointerLock();
    input.value = o.value;
    input.type = o.password ? 'password' : 'text';
    input.placeholder = o.placeholder;
    input.setAttribute('aria-label', o.placeholder || 'Text input');
    input.autocomplete = 'off';
    input.spellcheck = false;
    if (o.limit > 0) input.maxLength = o.limit;
    Object.assign(input.style, {position: 'fixed', zIndex: '2147483647', boxSizing: 'border-box',
      margin: '0', padding: '0', border: '0', outline: 'none', background: 'transparent',
      color: o.color, fontFamily: 'sans-serif', resize: 'none'});
    input.addEventListener('compositionstart', function () { s.composing = true; });
    input.addEventListener('compositionupdate', function (event) {
      // The counter beside the field follows the syllable as it is built.
      if (s.composing) GameText.send(s, 'compose', event.data);
    });
    input.addEventListener('compositionend', function () {
      s.composing = false;
      GameText.clamp(s, o.limit);
      GameText.send(s, 'input');
    });
    input.addEventListener('input', function (event) {
      if (GameText.clamp(s, o.limit)) {
        // Not every browser reports the composition it just lost; do not wait for it.
        s.composing = false;
        GameText.send(s, 'input');
        return;
      }
      if (!s.composing && !event.isComposing) GameText.send(s, 'input');
    });
    input.addEventListener('keydown', function (event) {
      event.stopPropagation();
      // Enter committing a Korean syllable must never submit a message.
      if (s.composing || event.isComposing || event.keyCode === 229) return;
      var action = event.key === 'Enter' && !o.multiline ? 'submit'
        : event.key === 'Escape' ? 'cancel'
        : event.key === 'Tab' ? (event.shiftKey ? 'backtab' : 'tab') : null;
      if (action) { event.preventDefault(); GameText.send(s, action); }
    });
    input.addEventListener('keyup', function (event) { event.stopPropagation(); });
    input.addEventListener('blur', function () { GameText.send(s, 'blur'); });
    // A canvas itself cannot contain visible HTML controls in fullscreen.
    // The template fullscreen container is handled by the pointer/fullscreen bridge.
    (document.fullscreenElement && document.fullscreenElement.tagName !== 'CANVAS'
      ? document.fullscreenElement : document.body).appendChild(input);
    input.focus({preventScroll: true});
    input.setSelectionRange(o.selectAll ? 0 : input.value.length, input.value.length);
  },
  GameTextLayout__deps: ['$GameText'],
  GameTextLayout: function (x, y, width, height, fontSize) {
    if (!GameText.state) return;
    var rect = Module.canvas.getBoundingClientRect();
    Object.assign(GameText.state.input.style, {
      left: rect.left + x * rect.width + 'px', top: rect.top + y * rect.height + 'px',
      width: width * rect.width + 'px', height: height * rect.height + 'px',
      fontSize: fontSize * rect.height + 'px'
    });
  },
  GameTextValue__deps: ['$GameText'],
  GameTextValue: function (value) {
    var s = GameText.state;
    if (!s || s.composing) return;
    var text = UTF8ToString(value);
    if (s.input.value === text) return;
    var start = s.input.selectionStart, end = s.input.selectionEnd;
    s.input.value = text;
    s.input.setSelectionRange(Math.min(start, text.length), Math.min(end, text.length));
  },
  GameTextClose__deps: ['$GameText'],
  GameTextClose: function () { GameText.close(); },
  GamePerfInstall: function (target) {
    if (new URLSearchParams(location.search).get('perf') !== '1') return;
    var name = UTF8ToString(target);
    var panel = document.createElement('details');
    panel.id = 'game-performance';
    panel.style.cssText = 'position:fixed;right:0;top:0;z-index:2147483646;background:#111;color:white;padding:8px;max-width:460px;max-height:85vh;overflow:auto';
    var summary = document.createElement('summary'); summary.textContent = '성능 측정'; panel.appendChild(summary);
    var button = document.createElement('button'); button.textContent = '현재 구간 30초 측정'; panel.appendChild(button);
    var result = document.createElement('pre'); result.id = 'game-performance-result'; panel.appendChild(result);
    button.onclick = function () {
      result.textContent = '측정 중: 30초 동안 이 탭을 유지해 주세요.';
      SendMessage(name, 'BeginCapture');
    };
    document.body.appendChild(panel);
  },
  GamePerfReport: function (json) {
    var result = document.getElementById('game-performance-result');
    if (result) result.textContent = UTF8ToString(json);
  },
  GamePointerIsLocked: function () {
    return document.pointerLockElement === Module.canvas ? 1 : 0;
  },
  GamePointerRelease__deps: ['$GamePointer'],
  GamePointerRelease: function (allowResume) {
    if (GamePointer.releaseKeys) {
      GamePointer.releaseKeys();
      GamePointer.keysReleasedForUI = true;
    }
    if (!allowResume) GamePointer.resumePending = false;
    GamePointer.browserReleased = false;
    if (document.pointerLockElement === Module.canvas && document.exitPointerLock) {
      GamePointer.releasing = true;
      GamePointer.resumePending = !!allowResume && GamePointer.keyboardLocked;
      document.exitPointerLock();
    }
  },
  GamePointerConsumeBrowserRelease__deps: ['$GamePointer'],
  GamePointerConsumeBrowserRelease: function () {
    var released = GamePointer.browserReleased;
    GamePointer.browserReleased = false;
    return released ? 1 : 0;
  },
  $GamePointer: {armed: false, installed: false, locked: false,
    releasing: false, browserReleased: false, keyboardLocked: false,
    keyboardPending: false, resumePending: false},
  GamePointerArm__deps: ['$GamePointer'],
  GamePointerArm: function (enabled) {
    GamePointer.armed = !!enabled;
    if (GamePointer.installed) {
      if (GamePointer.armed && GamePointer.keysReleasedForUI) {
        GamePointer.keysReleasedForUI = false;
        GamePointer.releaseKeys();
      }
      if (GamePointer.armed && GamePointer.resumePending && GamePointer.keyboardLocked &&
          !document.pointerLockElement && document.hasFocus() && !document.hidden) {
        GamePointer.resumePending = false;
        GamePointer.requestCapture();
      }
      return;
    }
    GamePointer.installed = true;
    GamePointer.heldKeys = new Map();
    GamePointer.releasedKeys = new Set();
    GamePointer.releaseKeys = function () {
      var held = Array.from(GamePointer.heldKeys.values());
      GamePointer.heldKeys.clear();
      GamePointer.releasingKeys = true;
      try {
        held.forEach(function (key) {
          GamePointer.releasedKeys.add(key.code);
          // Chrome can swallow physical key-up after its Escape unlock gesture.
          // Clear Unity's browser input backend too, not only InputSystem state.
          Module.canvas.dispatchEvent(new KeyboardEvent('keyup', Object.assign({
            bubbles: true, cancelable: true
          }, key)));
        });
      } finally { GamePointer.releasingKeys = false; }
    };
    document.addEventListener('keydown', function (event) {
      if (event.target !== Module.canvas || event.code === 'Escape') return;
      if (GamePointer.releasedKeys.has(event.code) && event.repeat) {
        event.preventDefault();
        event.stopImmediatePropagation();
        return;
      }
      GamePointer.releasedKeys.delete(event.code);
      GamePointer.heldKeys.set(event.code, {
        key: event.key, code: event.code, keyCode: event.keyCode,
        which: event.which, location: event.location
      });
    }, true);
    document.addEventListener('keyup', function (event) {
      GamePointer.heldKeys.delete(event.code);
      if (!GamePointer.releasingKeys) GamePointer.releasedKeys.delete(event.code);
    }, true);
    GamePointer.requestCapture = function () {
      try {
        var request = Module.canvas.requestPointerLock();
        if (request && request.catch) request.catch(function () {});
      } catch (_) { /* A deliberate click can retry if the browser denies capture. */ }
    };
    var syncKeyboardLock = function () {
      var fullscreen = document.fullscreenElement;
      if (!fullscreen || !fullscreen.contains(Module.canvas)) {
        GamePointer.keyboardLocked = false;
        GamePointer.resumePending = false;
        if (navigator.keyboard && navigator.keyboard.unlock) navigator.keyboard.unlock();
        return;
      }
      if (!navigator.keyboard || !navigator.keyboard.lock ||
          GamePointer.keyboardLocked || GamePointer.keyboardPending) return;
      GamePointer.keyboardPending = true;
      navigator.keyboard.lock(['Escape']).then(function () {
        GamePointer.keyboardPending = false;
        GamePointer.keyboardLocked = document.fullscreenElement === fullscreen;
        if (!GamePointer.keyboardLocked) navigator.keyboard.unlock();
      }).catch(function () {
        GamePointer.keyboardPending = false;
        GamePointer.keyboardLocked = false;
      });
    };
    document.addEventListener('fullscreenchange', syncKeyboardLock);
    window.addEventListener('blur', function () {
      GamePointer.resumePending = false;
      GamePointer.releaseKeys();
    });
    syncKeyboardLock();
    document.addEventListener('pointerlockchange', function () {
      var locked = document.pointerLockElement === Module.canvas;
      if (GamePointer.locked && !locked) GamePointer.releaseKeys();
      if (GamePointer.locked && !locked && !GamePointer.releasing &&
          document.hasFocus() && !document.hidden) {
        // Chrome can consume Escape before Unity receives its keydown.
        GamePointer.browserReleased = true;
      }
      GamePointer.locked = locked;
      GamePointer.releasing = false;
    });
    Module.canvas.addEventListener('pointerdown', function (event) {
      var active = document.activeElement;
      if (!GamePointer.armed || event.button !== 0 || document.pointerLockElement ||
          (active && /^(INPUT|TEXTAREA)$/.test(active.tagName))) return;
      // Fullscreen is explicitly requested by the player; only Esc is captured.
      syncKeyboardLock();
      GamePointer.resumePending = false;
      GamePointer.requestCapture();
    });
  }
});
