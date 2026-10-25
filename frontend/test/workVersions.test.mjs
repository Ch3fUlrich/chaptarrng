import assert from 'node:assert/strict';
import { test } from 'node:test';
import { versionLabel, versionState } from '../src/Author/Details/workVersions.js';

test('label combines medium and language', () => {
  assert.equal(versionLabel({ mediaType: 'audiobook', language: 'deu' }), 'AUDIO DEU');
  assert.equal(versionLabel({ mediaType: 'ebook', language: 'eng' }), 'EBOOK ENG');
  assert.equal(versionLabel({ mediaType: 'ebook', language: null }), 'EBOOK');
});

test('state: file wins over monitored', () => {
  assert.equal(versionState({ hasFiles: true, monitored: true }), 'have');
  assert.equal(versionState({ hasFiles: false, monitored: true }), 'wanted');
  assert.equal(versionState({ hasFiles: false, monitored: false }), 'none');
});
