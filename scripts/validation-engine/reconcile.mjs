#!/usr/bin/env node
// Copyright (C) ChaptarrNG contributors.
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const REQUIRED_NODES = new Set(['linux-core', 'windows-core', 'macos-core', 'postgres-migrations']);

function fail(message) { throw new Error(message); }

function nodeKind(receipt) {
  const nodeId = receipt.executionContext?.nodeId ?? '';
  if (nodeId.startsWith('chaptarr-linux-')) return 'linux-core';
  if (nodeId.startsWith('chaptarr-win-')) return 'windows-core';
  if (nodeId.startsWith('chaptarr-osx-')) return 'macos-core';
  if (nodeId.startsWith('chaptarr-postgres-linux-')) return 'postgres-migrations';
  return null;
}

function expectedPlatform(kind) {
  return kind === 'windows-core' ? 'windows' : kind === 'macos-core' ? 'macos' : 'linux';
}

function taskResult(receipt, id) {
  return (receipt.tasks ?? []).find((task) => task.taskId === id);
}

export function reconcileReceipts(receipts, expected) {
  if (!Array.isArray(receipts) || receipts.length !== REQUIRED_NODES.size) {
    fail(`Expected ${REQUIRED_NODES.size} node receipts, received ${receipts?.length ?? 0}`);
  }
  const byNode = new Map();
  let baseline = null;
  for (const receipt of receipts) {
    if (receipt.schemaVersion !== 1 || receipt.repository !== 'chaptarrng') fail('Unknown validation receipt schema or repository');
    if (receipt.status !== 'passed' || receipt.sourceChanged) fail(`Receipt ${receipt.executionContext?.nodeId ?? 'unknown'} did not pass or changed source`);
    const context = receipt.executionContext ?? {};
    const kind = nodeKind(receipt);
    if (!kind) fail(`Unknown node identity: ${context.nodeId ?? 'missing'}`);
    if (byNode.has(kind)) fail(`Duplicate validation node: ${kind}`);
    byNode.set(kind, receipt);
    if (context.platform !== expectedPlatform(kind)) fail(`Platform mismatch for ${context.nodeId}`);
    const architectureSuffix = `-${String(context.architecture ?? '').toUpperCase()}`;
    if (!context.nodeId.toUpperCase().endsWith(architectureSuffix)) fail(`Architecture mismatch for ${context.nodeId}`);
    const github = context.github;
    if (!github || github.runId !== String(expected.runId) || github.runAttempt !== String(expected.runAttempt) || github.sha !== expected.sha) {
      fail(`Workflow run, attempt, or SHA mismatch for ${context.nodeId}`);
    }
    const contract = taskResult(receipt, 'engine-contract-tests');
    if (!contract || contract.status !== 'passed' || contract.activeCases < 1) fail(`Contract tests are missing or incomplete for ${context.nodeId}`);
    const requiredTask = kind === 'postgres-migrations' ? 'postgres-migrations-smoke' : 'dotnet-core-tests';
    const result = taskResult(receipt, requiredTask);
    if (!result || result.status !== 'passed') fail(`Required test task ${requiredTask} did not pass for ${context.nodeId}`);
    if (!Number.isInteger(result.activeCases) || result.activeCases < 1) fail(`Required test task ${requiredTask} did not record passing cases for ${context.nodeId}`);
    if (kind !== 'postgres-migrations' && (!result.artifactInputs?.length || result.artifactInputs.some((item) => !item.available || !/^[a-f0-9]{64}$/.test(item.sha256)))) {
      fail(`Compiled test artifact identity is missing for ${context.nodeId}`);
    }
    const identity = {
      commit: receipt.candidate?.commit,
      tree: receipt.candidate?.tree,
      diffSha256: receipt.candidate?.diffSha256,
      sourceSha256: receipt.candidate?.sourceSha256,
      inventorySha256: receipt.inventorySha256,
      inventoryLaneDigests: receipt.inventoryLaneDigests,
    };
    if (!identity.commit || !identity.sourceSha256 || !identity.inventorySha256) fail(`Receipt source identity is incomplete for ${context.nodeId}`);
    if (baseline && JSON.stringify(identity) !== JSON.stringify(baseline)) fail(`Source or inventory identity drift at ${context.nodeId}`);
    baseline ??= identity;
  }
  for (const kind of REQUIRED_NODES) if (!byNode.has(kind)) fail(`Missing required node: ${kind}`);
  return {
    status: 'passed',
    nodeCount: byNode.size,
    nodes: [...byNode.entries()].map(([kind, receipt]) => ({
      kind,
      nodeId: receipt.executionContext.nodeId,
      taskId: kind === 'postgres-migrations' ? 'postgres-migrations-smoke' : 'dotnet-core-tests',
      activeCases: taskResult(receipt, kind === 'postgres-migrations' ? 'postgres-migrations-smoke' : 'dotnet-core-tests').activeCases,
    })).sort((left, right) => left.kind.localeCompare(right.kind)),
    sourceSha256: baseline.sourceSha256,
    inventorySha256: baseline.inventorySha256,
    commit: baseline.commit,
  };
}

function main(argv) {
  const directory = path.resolve(ROOT, argv[0] ?? 'validation-receipts');
  const files = readdirSync(directory).filter((file) => file.endsWith('.json')).sort();
  const receipts = files.map((file) => JSON.parse(readFileSync(path.join(directory, file), 'utf8')));
  const result = reconcileReceipts(receipts, {
    runId: process.env.GITHUB_RUN_ID,
    runAttempt: process.env.GITHUB_RUN_ATTEMPT,
    sha: process.env.GITHUB_SHA,
  });
  process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { main(process.argv.slice(2)); } catch (error) {
    process.stderr.write(`${error.stack ?? error.message}\n`);
    process.exitCode = 1;
  }
}
