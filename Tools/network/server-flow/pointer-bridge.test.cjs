// Contract tests for the JS bridge; real Chrome validation is still required.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const callbacks = {};
let focused = true;
let exits = 0;
let requests = 0;
let keyboardUnlocks = 0;
const windowCallbacks = {};
const canvas = { addEventListener() {}, requestPointerLock() { requests++; return Promise.resolve(); } };
const document = {
  pointerLockElement: null, hidden: false,
  hasFocus: () => focused,
  addEventListener: (name, fn) => { callbacks[name] = fn; },
  exitPointerLock: () => { exits++; document.pointerLockElement = null; }
};
const context = {
  document, Module: { canvas }, LibraryManager: { library: {} },
  navigator: { keyboard: { lock: () => Promise.resolve(), unlock: () => keyboardUnlocks++ } },
  window: { addEventListener: (name, fn) => { windowCallbacks[name] = fn; } },
  mergeInto: (target, source) => Object.assign(target, source)
};
vm.createContext(context);
vm.runInContext(fs.readFileSync(process.argv[2], 'utf8'), context);
const lib = context.LibraryManager.library;
context.GamePointer = lib.$GamePointer;
lib.GamePointerArm(1);
function lock() { document.pointerLockElement = canvas; callbacks.pointerlockchange(); }
function unlock() { document.pointerLockElement = null; callbacks.pointerlockchange(); }
lock();
assert.equal(lib.GamePointerIsLocked(), 1);
unlock(); // Escape can unlock without a delivered keydown.
assert.equal(lib.GamePointerConsumeBrowserRelease(), 1);
assert.equal(lib.GamePointerConsumeBrowserRelease(), 0);
lock();
lib.GamePointerRelease();
lib.GamePointerRelease(); // A second Unity tick before the async event.
callbacks.pointerlockchange();
assert.equal(exits, 1);
assert.equal(lib.GamePointerConsumeBrowserRelease(), 0);
lock(); focused = false; unlock();
assert.equal(lib.GamePointerConsumeBrowserRelease(), 0);
focused = true; lock(); unlock();
lib.GamePointerRelease(); // Scene/UI transition discards a stale escape.
assert.equal(lib.GamePointerConsumeBrowserRelease(), 0);
assert.equal(lib.GamePointerIsLocked(), 0);
console.log('PASS: actual capture, browser escape, consume-once, repeated release, focus loss, stale event');
(async () => {
  document.fullscreenElement = { contains: target => target === canvas };
  callbacks.fullscreenchange();
  await new Promise(setImmediate);
  assert.equal(context.GamePointer.keyboardLocked, true);
  lock(); lib.GamePointerRelease(1); callbacks.pointerlockchange();
  lib.GamePointerArm(0);
  assert.equal(requests, 0);
  lib.GamePointerArm(1); lib.GamePointerArm(1);
  assert.equal(requests, 1); // Resume once, not a request every frame.
  lock(); lib.GamePointerRelease(1); callbacks.pointerlockchange();
  windowCallbacks.blur(); lib.GamePointerArm(1);
  assert.equal(requests, 1);
  document.fullscreenElement = null; callbacks.fullscreenchange();
  assert.equal(context.GamePointer.keyboardLocked, false);
  assert.ok(keyboardUnlocks > 0);
  lock(); lib.GamePointerRelease(1); callbacks.pointerlockchange(); lib.GamePointerArm(1);
  assert.equal(requests, 1); // Windowed mode keeps explicit-click acquisition.
  context.GamePointer.keyboardLocked = true;
  lock(); lib.GamePointerRelease(1); callbacks.pointerlockchange();
  lib.GamePointerRelease(0); lib.GamePointerArm(1);
  assert.equal(requests, 1); // Disconnection or camera teardown cancels UI resume.
  context.GamePointer.keyboardLocked = false;
  context.navigator.keyboard.lock = () => Promise.reject(new Error('permission denied'));
  document.fullscreenElement = { contains: target => target === canvas };
  callbacks.fullscreenchange();
  await new Promise(setImmediate);
  assert.equal(context.GamePointer.keyboardLocked, false);
  assert.equal(context.GamePointer.keyboardPending, false);
  console.log('PASS: fullscreen resume once, blur cancellation, fullscreen exit, permission-denied fallback');
})().catch(error => { console.error(error); process.exitCode = 1; });
