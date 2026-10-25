// Run: node --test frontend/test/*.test.mjs
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { resolveMonitorScope } from '../src/Utilities/Author/monitorScope.js';

test('all covers both media regardless of the page medium', () => {
  assert.deepEqual(resolveMonitorScope('all', 'ebook'), { monitor: 'all', mediaType: null });
});

test('all ebooks / all audiobooks restrict to one medium', () => {
  assert.deepEqual(resolveMonitorScope('allEbooks', 'audiobook'), { monitor: 'all', mediaType: 'ebook' });
  assert.deepEqual(resolveMonitorScope('allAudiobooks', 'ebook'), { monitor: 'all', mediaType: 'audiobook' });
});

test('other options keep the page medium', () => {
  assert.deepEqual(resolveMonitorScope('missing', 'ebook'), { monitor: 'missing', mediaType: 'ebook' });
});
