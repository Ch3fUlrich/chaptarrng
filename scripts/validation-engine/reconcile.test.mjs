// Copyright (C) ChaptarrNG contributors.
import test from 'node:test';
import assert from 'node:assert/strict';
import { reconcileReceipts } from './reconcile.mjs';

const expected = { runId: '7421', runAttempt: '2', sha: 'commit-abc' };
const nodes = [
  ['chaptarr-linux-X64', 'linux', 'x64', 'dotnet-core-tests'],
  ['chaptarr-win-X64', 'windows', 'x64', 'dotnet-core-tests'],
  ['chaptarr-osx-ARM64', 'macos', 'arm64', 'dotnet-core-tests'],
  ['chaptarr-postgres-linux-X64', 'linux', 'x64', 'postgres-migrations-smoke'],
];

function receipt([nodeId, platform, architecture, requiredTask], overrides = {}) {
  const tasks = [
    { taskId: 'engine-contract-tests', status: 'passed', activeCases: 8 },
    {
      taskId: requiredTask,
      status: 'passed',
      activeCases: requiredTask === 'postgres-migrations-smoke' ? 1 : 302,
      artifactInputs: requiredTask === 'postgres-migrations-smoke' ? [] : [
        { file: '_tests/net10.0/Chaptarr.Core.Test.dll', available: true, sha256: 'a'.repeat(64) },
      ],
    },
  ];
  return {
    schemaVersion: 1,
    repository: 'chaptarrng',
    status: 'passed',
    sourceChanged: false,
    candidate: { commit: expected.sha, tree: 'tree-1', diffSha256: 'diff-1', sourceSha256: 'source-1' },
    inventorySha256: 'inventory-1',
    inventoryLaneDigests: { core: 'core-1', migration: 'migration-1' },
    executionContext: {
      nodeId,
      platform,
      architecture,
      github: { runId: expected.runId, runAttempt: expected.runAttempt, sha: expected.sha },
    },
    tasks,
    ...overrides,
  };
}

function complete(overrides = {}) {
  return nodes.map((node, index) => receipt(node, overrides[index] ?? {}));
}

test('accepts all four passing nodes from one source and workflow attempt', () => {
  const result = reconcileReceipts(complete(), expected);
  assert.equal(result.status, 'passed');
  assert.equal(result.nodeCount, 4);
  assert.equal(result.nodes.find((node) => node.kind === 'postgres-migrations').activeCases, 1);
});

test('rejects missing, duplicate, and unrecognized runner nodes', () => {
  assert.throws(() => reconcileReceipts(complete().slice(0, 3), expected), /Expected 4 node receipts/);
  const duplicate = complete();
  duplicate[3] = receipt(nodes[0]);
  assert.throws(() => reconcileReceipts(duplicate, expected), /Duplicate validation node/);
  const unknown = complete();
  unknown[3].executionContext.nodeId = 'other-runner-X64';
  assert.throws(() => reconcileReceipts(unknown, expected), /Unknown node identity/);
});

test('rejects failed and zero-case tasks', () => {
  const failed = complete();
  failed[1].tasks.find((item) => item.taskId === 'dotnet-core-tests').status = 'failed';
  assert.throws(() => reconcileReceipts(failed, expected), /did not pass/);
  const empty = complete();
  empty[0].tasks.find((item) => item.taskId === 'dotnet-core-tests').activeCases = 0;
  assert.throws(() => reconcileReceipts(empty, expected), /did not record passing cases/);
});

test('rejects receipts from a different run, attempt, or commit', () => {
  for (const change of [
    (value) => { value.executionContext.github.runId = 'elsewhere'; },
    (value) => { value.executionContext.github.runAttempt = '1'; },
    (value) => { value.executionContext.github.sha = 'other-sha'; },
  ]) {
    const receipts = complete();
    change(receipts[0]);
    assert.throws(() => reconcileReceipts(receipts, expected), /run, attempt, or SHA mismatch/);
  }
});

test('rejects source or inventory drift and platform identity mismatches', () => {
  const changedSource = complete();
  changedSource[0].candidate.sourceSha256 = 'source-2';
  assert.throws(() => reconcileReceipts(changedSource, expected), /identity drift/);
  const changedInventory = complete();
  changedInventory[2].inventoryLaneDigests.core = 'changed';
  assert.throws(() => reconcileReceipts(changedInventory, expected), /identity drift/);
  const wrongPlatform = complete();
  wrongPlatform[1].executionContext.platform = 'linux';
  assert.throws(() => reconcileReceipts(wrongPlatform, expected), /Platform mismatch/);
});

test('requires a digest for the platform-specific compiled test assembly', () => {
  const receipts = complete();
  receipts[0].tasks.find((item) => item.taskId === 'dotnet-core-tests').artifactInputs[0].available = false;
  assert.throws(() => reconcileReceipts(receipts, expected), /artifact identity is missing/);
});
