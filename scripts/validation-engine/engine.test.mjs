// Copyright (C) ChaptarrNG contributors.
import test from 'node:test';
import assert from 'node:assert/strict';
import { createPlan, discoverInventory, formatValidationProgress, globRegex, parseNodeTestSummary, parseNUnitSummary, sha256GitText } from './engine.mjs';

function withEnvironmentVariable(name, value, action) {
  const previous = process.env[name];
  if (value === undefined) delete process.env[name];
  else process.env[name] = value;
  try { return action(); } finally {
    if (previous === undefined) delete process.env[name];
    else process.env[name] = previous;
  }
}

test('test source patterns include nested project files but exclude sibling projects', () => {
  const pattern = globRegex('src/Chaptarr.Core.Test/**/*.cs');
  assert.equal(pattern.test('src/Chaptarr.Core.Test/Download/DirectDownloadFixture.cs'), true);
  assert.equal(pattern.test('src/Chaptarr.Core/Download/DirectDownloadFixture.cs'), false);
  assert.equal(pattern.test('src-other/Chaptarr.Core.Test/Fake.cs'), false);
});

test('inventory assigns test, migration, and contract files to explicit owners', () => {
  const inventory = discoverInventory();
  assert.ok(inventory.counts['core-dotnet-test-sources'] > 50);
  assert.ok(inventory.counts['migration-sources'] > 0);
  assert.equal(inventory.counts['validation-contracts'], 1);
  assert.equal(inventory.counts['reconciliation-contracts'], 1);
  assert.equal(new Set(inventory.entries.map((entry) => entry.file)).size, inventory.entries.length);
  assert.ok(inventory.entries.every((entry) => /^[a-f0-9]{64}$/.test(entry.sha256)));
});

test('NUnit summary keeps native pass, failure, skip and inconclusive totals', () => {
  assert.deepEqual(parseNUnitSummary(
    '<test-run total="140" passed="128" failed="2" skipped="7" inconclusive="3" result="Failed"/>',
  ), { total: 140, passed: 128, failed: 2, skipped: 7, inconclusive: 3, result: 'Failed' });
});

test('Node TAP summary accounts for pass, fail, skip, todo, and cancelled cases', () => {
  assert.deepEqual(parseNodeTestSummary(
    '# tests 7\n# pass 3\n# fail 1\n# cancelled 1\n# skipped 1\n# todo 1\n',
  ), { total: 7, passed: 3, failed: 1, cancelled: 1, skipped: 1, todo: 1, inconclusive: 1 });
});

test('Node TAP summary refuses missing and inconsistent case totals', () => {
  assert.throws(() => parseNodeTestSummary('# pass 2\n# fail 0\n'), /missing, invalid, zero, or inconsistent/);
  assert.throws(() => parseNodeTestSummary('# tests 3\n# pass 2\n# fail 0\n'), /missing, invalid, zero, or inconsistent/);
});

test('NUnit summary refuses missing, malformed, and zero-case results', () => {
  assert.throws(() => parseNUnitSummary('<test-suite total="4"/>'), /missing its test-run/);
  assert.throws(() => parseNUnitSummary('<test-run total="bad" passed="0" failed="0"/>'), /invalid, or zero/);
  assert.throws(() => parseNUnitSummary('<test-run total="0" passed="0" failed="0"/>'), /invalid, or zero/);
});

test('live progress summary distinguishes completed, blocked, pending, and running tasks', () => {
  const tasks = [
    { id: 'passed', name: 'Passed task' },
    { id: 'blocked', name: 'Blocked task' },
    { id: 'running', name: 'Long task' },
  ];
  const results = new Map([
    ['passed', { status: 'passed', startedAt: '2026-10-12T00:00:00.000Z' }],
    ['blocked', { status: 'blocked' }],
  ]);
  assert.equal(
    formatValidationProgress('sample', tasks, results, tasks[2], 65_000),
    '[validation] sample: 1/3 selected tasks complete (1 passed, 0 failed, 1 blocked, 1 pending); running Long task (1m05s)',
  );
});

test('source fingerprints normalize Git text line endings across runner operating systems', () => {
  const lf = Buffer.from('first line\nsecond line\n');
  const crlf = Buffer.from('first line\r\nsecond line\r\n');
  assert.equal(sha256GitText(lf), sha256GitText(crlf));
  assert.notEqual(sha256GitText(lf), sha256GitText(Buffer.from('first line\nchanged line\n')));
});

test('test-only planning distinguishes local and hosted tasks regardless of ambient workflow environment', () => {
  const localPlan = withEnvironmentVariable('GITHUB_ACTIONS', undefined, () =>
    withEnvironmentVariable('VALIDATION_NODE_ID', undefined, () => createPlan({ testsOnly: true })));
  assert.deepEqual(localPlan.tasks.filter((task) => task.selected).map((task) => task.id), [
    'engine-contract-tests', 'dotnet-core-tests',
  ]);
  assert.ok(localPlan.missingArtifacts.length === 0 || localPlan.missingArtifacts.includes('_tests/net10.0/Chaptarr.Core.Test.dll'));
  const expectedPlatform = process.platform === 'win32' ? 'windows' : process.platform === 'darwin' ? 'macos' : 'linux';
  const expectedNodePlatform = expectedPlatform === 'macos' ? 'osx' : expectedPlatform === 'windows' ? 'win' : 'linux';
  assert.equal(localPlan.executionContext.platform, expectedPlatform);
  assert.equal(localPlan.executionContext.nodeId.toLowerCase(), `chaptarr-${expectedNodePlatform}-${process.arch}`.toLowerCase());
  assert.equal(localPlan.executionContext.github, null);
  assert.match(localPlan.candidate.sourceSha256, /^[a-f0-9]{64}$/);

  const hostedPlan = withEnvironmentVariable('GITHUB_ACTIONS', 'true', () =>
    withEnvironmentVariable('VALIDATION_NODE_ID', 'chaptarr-linux-X64', () => createPlan({ testsOnly: true })));
  assert.deepEqual(hostedPlan.tasks.filter((task) => task.selected).map((task) => task.id), [
    'engine-contract-tests', 'dotnet-core-tests',
  ]);
  assert.equal(hostedPlan.executionContext.executor, 'github-actions');

  const postgresPlan = withEnvironmentVariable('GITHUB_ACTIONS', 'true', () =>
    withEnvironmentVariable('VALIDATION_NODE_ID', 'chaptarr-postgres-linux-X64', () => createPlan({ testsOnly: true })));
  assert.deepEqual(postgresPlan.tasks.filter((task) => task.selected).map((task) => task.id), [
    'engine-contract-tests', 'postgres-migrations-smoke',
  ]);
  assert.match(postgresPlan.executionContext.nodeId, /postgres/);
});

test('explicit PostgreSQL task planning includes only its contract dependency', () => {
  const plan = withEnvironmentVariable('GITHUB_ACTIONS', 'true', () =>
    withEnvironmentVariable('VALIDATION_NODE_ID', 'chaptarr-postgres-linux-X64', () =>
      createPlan({ task: 'postgres-migrations-smoke' })));
  assert.deepEqual(plan.tasks.filter((task) => task.selected).map((task) => task.id), [
    'engine-contract-tests', 'postgres-migrations-smoke',
  ]);
  assert.throws(
    () => withEnvironmentVariable('VALIDATION_NODE_ID', 'chaptarr-linux-X64', () => createPlan({ task: 'postgres-migrations-smoke' })),
    /assigned to validation node/,
  );
});
