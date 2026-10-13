// Copyright (C) ChaptarrNG contributors.
import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, readlinkSync, rmSync, statSync, writeFileSync } from 'node:fs';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';
import { createPlan, createShortTemporaryPathAlias, currentRepoFiles, discoverInventory, formatValidationProgress, globRegex, isolatedChildEnvironment, parseNodeTestSummary, parseNUnitSummary, runTask, sha256GitText } from './engine.mjs';
import { validation } from './config.mjs';

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

test('working-tree inventory omits deleted tracked files and de-duplicates renamed paths', (t) => {
  const root = mkdtempSync(path.join(os.tmpdir(), 'chaptarr-validation-inventory-'));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  writeFileSync(path.join(root, 'migration-115.cs'), 'existing migration');
  writeFileSync(path.join(root, 'migration-116.cs'), 'new migration');

  assert.deepEqual(
    currentRepoFiles(
      ['migration-108.cs', 'migration-115.cs'],
      ['migration-116.cs', 'migration-115.cs'],
      root,
    ),
    ['migration-115.cs', 'migration-116.cs'],
  );
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

test('validation task logs are size bounded and mark truncated output', async (t) => {
  const runRoot = mkdtempSync(path.join(os.tmpdir(), 'chaptarr-validation-log-'));
  mkdirSync(runRoot, { recursive: true });
  t.after(() => rmSync(runRoot, { recursive: true, force: true }));
  const outputBytes = 16 * 1024 * 1024 + 1;
  const spec = {
    id: 'log-limit-fixture',
    command: [
      process.execPath,
      '-e',
      'process.stdout.write(Buffer.alloc(' + outputBytes + ', 120));process.stdout.write("VALIDATION_TEST_CASES_PASSED=1")',
    ],
    timeoutSeconds: 30,
  };
  const result = await runTask({ id: spec.id }, runRoot, spec);
  const log = readFileSync(path.join(runRoot, result.logFile));
  assert.equal(result.status, 'passed');
  assert.equal(result.outputBytes, outputBytes + Buffer.byteLength('VALIDATION_TEST_CASES_PASSED=1'));
  assert.equal(log.length, 16 * 1024 * 1024);
  assert.match(log.toString('utf8'), /\[validation log truncated after 16 MiB\]\n$/u);
});

test('task environments use an allowlist and run-owned user and temporary directories', (t) => {
  const root = mkdtempSync(path.join(os.tmpdir(), 'chaptarr-validation-env-'));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  assert.throws(() => isolatedChildEnvironment({ id: 'fixture', passEnv: ['GITHUB_TOKEN'] }, root, {
    GITHUB_TOKEN: 'synthetic-token',
  }), /unapproved inherited environment variable/);
  assert.throws(() => isolatedChildEnvironment({ id: '../outside' }, root), /safe lowercase identifier/);
  const env = isolatedChildEnvironment({
    id: 'fixture',
    kind: 'test',
    env: {
      FIXTURE_SETTING: 'expected',
      PLAYWRIGHT_BROWSERS_PATH: '/operator/playwright',
    },
  }, root, {
    PATH: '/toolchain/bin',
    DOTNET_ROOT: '/toolchain/dotnet',
    HOME: '/operator/home',
    TMPDIR: '/operator/tmp',
    NODE_OPTIONS: '--require=/operator/injected.cjs',
    GITHUB_TOKEN: 'synthetic-token',
    RANDOM_PRIVATE_VALUE: 'synthetic-private-value',
    XDG_CONFIG_HOME: '/operator/config',
  });
  const environmentRoot = path.join(root, 'fixture-environment');
  assert.equal(env.PATH, '/toolchain/bin');
  assert.equal(env.DOTNET_ROOT, '/toolchain/dotnet');
  assert.equal(env.HOME, path.join(environmentRoot, 'home'));
  assert.equal(env.USERPROFILE, env.HOME);
  assert.equal(env.TMPDIR, path.join(environmentRoot, 'tmp'));
  assert.equal(env.XDG_CONFIG_HOME, path.join(environmentRoot, 'xdg-config'));
  assert.equal(env.XDG_CACHE_HOME, path.join(environmentRoot, 'xdg-cache'));
  assert.equal(env.NODE_ENV, 'test');
  assert.equal(env.CI, 'true');
  assert.equal(env.FIXTURE_SETTING, 'expected');
  assert.equal(env.PLAYWRIGHT_BROWSERS_PATH, '/operator/playwright');
  for (const key of ['NODE_OPTIONS', 'GITHUB_TOKEN', 'RANDOM_PRIVATE_VALUE']) assert.equal(env[key], undefined);
  if (process.platform !== 'win32') {
    assert.equal(statSync(env.HOME).mode & 0o777, 0o700);
    assert.equal(statSync(env.TMPDIR).mode & 0o777, 0o700);
  }
});

test('Linux browser tests get a short temporary path that targets the isolated task directory', async (t) => {
  if (process.platform !== 'linux') return t.skip('Linux Chromium uses UNIX sockets with a short path limit.');

  const target = mkdtempSync(path.join(os.tmpdir(), 'chaptarr-validation-browser-temp-'));
  t.after(() => rmSync(target, { recursive: true, force: true }));
  const alias = createShortTemporaryPathAlias(target);
  t.after(alias.cleanup);

  assert.equal(path.dirname(path.dirname(alias.directory)), '/dev/shm');
  assert.ok(alias.directory.length <= 32, `Expected a short temp path, received ${alias.directory}`);
  assert.equal(readlinkSync(alias.directory), target);
  assert.equal(statSync(alias.directory).isDirectory(), true);

  const server = net.createServer();
  const socketPath = path.join(alias.directory, 'chromium.sock');
  try {
    await new Promise((resolve, reject) => {
      server.once('error', reject);
      server.listen(socketPath, resolve);
    });
    assert.equal(statSync(socketPath).isSocket(), true);
  } finally {
    if (server.listening) await new Promise((resolve) => server.close(resolve));
  }
});

test('Linux browser temp aliases fall back when the primary path cannot be created', async (t) => {
  if (process.platform !== 'linux') return t.skip('The fallback uses the Linux shared-memory filesystem.');

  const target = mkdtempSync(path.join(os.tmpdir(), 'chaptarr-validation-browser-fallback-'));
  t.after(() => rmSync(target, { recursive: true, force: true }));
  const alias = createShortTemporaryPathAlias(target, path.join(target, 'missing-parent'));
  t.after(alias.cleanup);

  assert.match(alias.directory, /^\/dev\/shm\/cng-/);
  assert.ok(alias.directory.length <= 32, `Expected a short temp path, received ${alias.directory}`);

  const server = net.createServer();
  const socketPath = path.join(alias.directory, 'chromium.sock');
  try {
    await new Promise((resolve, reject) => {
      server.once('error', reject);
      server.listen(socketPath, resolve);
    });
    assert.equal(statSync(socketPath).isSocket(), true);
  } finally {
    if (server.listening) await new Promise((resolve) => server.close(resolve));
  }
});

test('only the NUnit browser task receives its explicit Playwright cache path', (t) => {
  const root = mkdtempSync(path.join(os.tmpdir(), 'chaptarr-validation-browser-'));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  const task = validation.tasks.find((candidate) => candidate.id === 'dotnet-core-tests');
  assert.ok(task);
  assert.deepEqual(task.passEnv, ['PLAYWRIGHT_BROWSERS_PATH']);
  const env = isolatedChildEnvironment(task, root, {
    PATH: '/toolchain/bin',
    HOME: '/operator/home',
    PLAYWRIGHT_BROWSERS_PATH: '/runner/temp/playwright-browsers',
    GITHUB_TOKEN: 'synthetic-token',
    NODE_OPTIONS: '--require=/operator/injected.cjs',
  });
  assert.equal(env.PLAYWRIGHT_BROWSERS_PATH, '/runner/temp/playwright-browsers');
  assert.equal(env.DIRECT_DOWNLOAD_REQUIRE_PLAYWRIGHT, process.platform === 'linux' ? 'true' : 'false');
  assert.equal(env.HOME, path.join(root, 'dotnet-core-tests-environment', 'home'));
  assert.equal(env.GITHUB_TOKEN, undefined);
  assert.equal(env.NODE_OPTIONS, undefined);
  assert.throws(() => isolatedChildEnvironment({
    id: 'engine-contract-tests',
    passEnv: ['PLAYWRIGHT_BROWSERS_PATH'],
  }, root, { PLAYWRIGHT_BROWSERS_PATH: '/runner/temp/playwright-browsers' }),
  /unapproved inherited environment variable/);
});

test('Chromium installation and tests share the private-runner cache path', () => {
  const workflow = readFileSync(new URL('../../.github/workflows/build.yml', import.meta.url), 'utf8');
  const cacheSetting = 'PLAYWRIGHT_BROWSERS_PATH: ${{ steps.private-runner-temp.outputs.path || runner.temp }}/playwright-browsers';
  assert.doesNotMatch(workflow, /^(<<<<<<<|=======|>>>>>>>)( .*)?$/m);
  const cacheSteps = [
    'Configure Chromium cache for direct-download browser tests',
    'Install Chromium on the self-hosted Linux runner',
    'Install Chromium and system dependencies on GitHub-hosted Linux',
    'Test',
  ];
  for (const name of cacheSteps) {
    const marker = `    - name: ${name}\n`;
    const start = workflow.indexOf(marker);
    assert.notEqual(start, -1, `missing workflow step: ${name}`);
    const next = workflow.indexOf('\n    - name:', start + marker.length);
    const step = workflow.slice(start, next === -1 ? workflow.length : next);
    assert.ok(step.split('\n').some((line) => line.trim() === cacheSetting), `${name} must use the private cache path`);
  }
  assert.match(workflow, /PLAYWRIGHT_BROWSERS_PATH=\$PLAYWRIGHT_BROWSERS_PATH.*\$GITHUB_ENV/);
});

test('test-only planning distinguishes local and hosted tasks regardless of ambient workflow environment', () => {
  const localPlan = withEnvironmentVariable('GITHUB_ACTIONS', undefined, () =>
    withEnvironmentVariable('VALIDATION_NODE_ID', undefined, () =>
      withEnvironmentVariable('GITHUB_TOKEN', 'synthetic-runtime-token', () =>
        withEnvironmentVariable('NODE_OPTIONS', '--require=/nonexistent-chaptarr-runtime-module.cjs', () =>
          createPlan({ testsOnly: true })))));
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
  assert.equal(localPlan.runtimes.find((runtime) => runtime.command === process.execPath).version, process.version);

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
