mergeInto(LibraryManager.library, {
  $GameLoading: {
    state: null,
    hide: function () {
      var s = GameLoading.state;
      if (!s) return;
      GameLoading.state = null;
      s.animations.forEach(function (animation) { animation.cancel(); });
      s.root.remove();
      window.removeEventListener('resize', s.layout);
      window.removeEventListener('scroll', s.layout, true);
      document.removeEventListener('fullscreenchange', s.layout);
      if (s.observer) s.observer.disconnect();
      if (s.frame) cancelAnimationFrame(s.frame);
    }
  },
  GameLoadingShow__deps: ['$GameLoading'],
  GameLoadingShow: function (text, x, y, width, height, fontSize, bounce, letterSeconds) {
    // Transform-only Web Animations can run on the compositor while WASM blocks JS.
    // The static art stays on Unity's canvas; this opaque strip covers its TMP letters.
    var canvas = Module.canvas;
    if (!canvas || !Element.prototype.animate) return 0;
    try {
      if (GameLoading.state) return 1; // Countdown ticks must not restart the animation.
      var root = document.createElement('div');
      root.id = 'game-loading-letters';
      root.setAttribute('role', 'status');
      root.setAttribute('aria-label', UTF8ToString(text));
      Object.assign(root.style, {position: 'fixed', zIndex: '2147483647', background: '#000',
        color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
        boxSizing: 'border-box', overflow: 'hidden', pointerEvents: 'none',
        fontFamily: 'sans-serif', fontWeight: '500', lineHeight: '1', contain: 'layout paint',
        isolation: 'isolate'});
      var s = {root: root, animations: [], observer: null, layout: null, ready: false, frame: 0};
      GameLoading.state = s;
      s.layout = function () {
        var fullscreen = document.fullscreenElement;
        var parent = fullscreen || document.body;
        // A popover is in the top layer, so it also works over a fullscreen canvas.
        if (fullscreen && fullscreen.tagName === 'CANVAS') {
          parent = document.body;
          if (typeof root.showPopover !== 'function') {
            // Older browsers retain Unity's animated label instead of losing it.
            root.style.display = 'none';
            return;
          }
          root.setAttribute('popover', 'manual');
        }
        if (root.parentNode !== parent) parent.appendChild(root);
        if (root.hasAttribute('popover') && typeof root.showPopover === 'function' &&
            !root.matches(':popover-open')) root.showPopover();
        var rect = canvas.getBoundingClientRect();
        var padding = (bounce + fontSize * 0.08) * rect.height;
        Object.assign(root.style, {display: 'flex', inset: 'auto', margin: '0', border: '0',
          padding: padding + 'px 0', left: (rect.left + x * rect.width) + 'px',
          top: (rect.top + y * rect.height - padding) + 'px',
          width: (width * rect.width) + 'px', height: (height * rect.height + 2 * padding) + 'px',
          fontSize: (fontSize * rect.height) + 'px'});
      };
      var letters = Array.from(UTF8ToString(text));
      var step = 1 / Math.max(1, letters.length);
      letters.forEach(function (letter, index) {
        var span = document.createElement('span');
        span.textContent = letter;
        span.setAttribute('aria-hidden', 'true');
        Object.assign(span.style, {display: 'inline-block', whiteSpace: 'pre', willChange: 'transform'});
        root.appendChild(span);
        var frames = [{transform: 'translateY(0)', offset: 0}];
        if (index > 0) frames.push({transform: 'translateY(0)', offset: index * step});
        frames.push({transform: 'translateY(-' + (bounce / fontSize) + 'em)', offset: (index + 0.5) * step});
        frames.push({transform: 'translateY(0)', offset: (index + 1) * step});
        if (index < letters.length - 1) frames.push({transform: 'translateY(0)', offset: 1});
        s.animations.push(span.animate(frames, {duration: letters.length * letterSeconds * 1000,
          iterations: Infinity, easing: 'linear'}));
      });
      s.layout();
      // Submit the compositor animation before starting a blocking Unity load.
      s.frame = requestAnimationFrame(function () {
        s.frame = requestAnimationFrame(function () { s.ready = true; });
      });
      window.addEventListener('resize', s.layout);
      window.addEventListener('scroll', s.layout, true);
      document.addEventListener('fullscreenchange', s.layout);
      if (typeof ResizeObserver !== 'undefined') {
        s.observer = new ResizeObserver(s.layout);
        s.observer.observe(canvas);
      }
      return 1;
    } catch (error) {
      GameLoading.hide();
      console.warn('[Loading] Browser animation unavailable; keeping Unity fallback.', error);
      return 0;
    }
  },
  GameLoadingReady__deps: ['$GameLoading'],
  GameLoadingReady: function () { return !GameLoading.state || GameLoading.state.ready ? 1 : 0; },
  GameLoadingHide__deps: ['$GameLoading'],
  GameLoadingHide: function () { GameLoading.hide(); }
});
