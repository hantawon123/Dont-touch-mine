// Node.js standard-library check; does not start a browser or contact a server.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

(async () => {
  const current = 'a'.repeat(40);
  let latest = current, offline = false, confirmed = false, poll;
  const notice = {style: {}};
  const location = {pathname: '/play/releases/' + current + '/web/', href: 'unchanged'};
  vm.runInNewContext(fs.readFileSync(__dirname + '/release-info.js', 'utf8'), {
    location,
    document: {createElement: () => notice, body: {appendChild() {}}},
    confirm: () => confirmed,
    setInterval: (callback, ms) => {assert.equal(ms, 60000); poll = callback;},
    fetch: async (url, options) => {
      assert.equal(url, '/play/current.json');
      assert.equal(options.cache, 'no-store');
      if (offline) throw new Error('offline');
      return {ok: true, json: async () => ({revision: latest})};
    },
  });
  await new Promise(setImmediate);
  assert.equal(notice.style.display, 'none');
  latest = 'b'.repeat(40);
  await poll();
  assert.equal(notice.style.display, 'block');
  assert.equal(location.href, 'unchanged');
  notice.onclick();
  assert.equal(location.href, 'unchanged');
  offline = true;
  await poll();
  assert.equal(location.href, 'unchanged');
  offline = false;
  latest = '../invalid';
  await poll();
  assert.equal(notice.style.display, 'none');
  latest = 'b'.repeat(40);
  await poll();
  confirmed = true;
  notice.onclick();
  assert.equal(location.href, '/play/');
  console.log('Version notice, offline preservation and explicit navigation passed');
})().catch(error => {console.error(error); process.exitCode = 1;});
