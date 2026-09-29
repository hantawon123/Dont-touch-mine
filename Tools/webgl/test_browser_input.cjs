// Run with node Tools/webgl/test_browser_input.cjs. Synthetic composition verifies
// bridge ordering; actual Windows IME + Unity focus still needs a WebGL playtest.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const library = {}, messages = [];
function element(tag) {
  const events = {};
  return {tagName: tag.toUpperCase(), style: {}, value: '', selectionStart: 0, selectionEnd: 0,
    setAttribute() {}, appendChild() {}, focus() { document.activeElement = this; },
    remove() { if (document.activeElement === this) document.activeElement = null; this.fire('blur'); },
    setSelectionRange(a, b) { this.selectionStart = a; this.selectionEnd = b; },
    addEventListener(name, callback) { (events[name] ||= []).push(callback); },
    fire(name, data = {}) {
      const e = {stopPropagation() {}, preventDefault() {}, ...data};
      for (const cb of events[name] || []) cb(e);
    }};
}
const document = {body: element('body'), activeElement: null, createElement: element};
const canvas = element('canvas');
canvas.getBoundingClientRect = () => ({left: 10, top: 20, width: 960, height: 600});
let locks = 0;
canvas.requestPointerLock = () => { locks++; return Promise.resolve(); };
const context = {LibraryManager: {library}, mergeInto: Object.assign, document, Module: {canvas},
  UTF8ToString: value => value,
  SendMessage: (target, method, json) => messages.push(JSON.parse(json))};
vm.runInNewContext(fs.readFileSync(path.join(__dirname,
  '../../Assets/_Game/Client/Common/WebTextInput.jslib'), 'utf8'), context);
context.GameText = library.$GameText;
context.GamePointer = library.$GamePointer;
const open = id => library.GameTextOpen('Input', JSON.stringify({id, value: '', placeholder: '닉네임', limit: 20}));
open(1);
let input = context.GameText.state.input;
input.fire('compositionstart'); input.value = 'ㅎ'; input.fire('input', {isComposing: true});
library.GameTextValue(''); assert.equal(input.value, 'ㅎ');
input.fire('keydown', {key: 'Enter', isComposing: true}); assert.equal(messages.length, 0);
input.value = '한'; input.fire('compositionend'); assert.equal(messages.at(-1).value, '한');
input.fire('keydown', {key: 'Enter', keyCode: 229}); assert.equal(messages.length, 1);
input.fire('keydown', {key: 'Enter'}); assert.equal(messages.at(-1).action, 'submit');
library.GameTextLayout(.1, .2, .3, .1, .04); assert.equal(input.style.left, '106px');
assert.equal(input.style.fontSize, '24px');
input.value = '붙여넣기🙂'; input.fire('input'); assert.equal(messages.at(-1).value, '붙여넣기🙂');
input.setSelectionRange(7, 7); library.GameTextValue('붙여넣기'); assert.equal(input.selectionEnd, 4);
const count = messages.length; open(2); input.fire('compositionend'); input.fire('blur');
assert.equal(messages.length, count, 'Old field must not send edits after replacement');
input = context.GameText.state.input;
input.fire('keydown', {key: 'Tab', shiftKey: true}); assert.equal(messages.at(-1).action, 'backtab');
library.GamePointerArm(1); canvas.fire('pointerdown', {button: 0}); assert.equal(locks, 0, 'IME owns focus');
library.GameTextClose(); canvas.fire('pointerdown', {button: 0}); assert.equal(locks, 1);
library.GamePointerArm(0); canvas.fire('pointerdown', {button: 0}); assert.equal(locks, 1);
library.GameTextOpen('Input', JSON.stringify({id: 3, value: '기존 이름', selectAll: true}));
assert.equal(context.GameText.state.input.selectionStart, 0);
assert.equal(context.GameText.state.input.selectionEnd, '기존 이름'.length);
console.log('PASS: composition, submit, paste, validation, stale callbacks, layout, focus, pointer recovery');
