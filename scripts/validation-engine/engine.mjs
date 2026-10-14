#!/usr/bin/env node
// Copyright (C) ChaptarrNG contributors.
import { createHash, randomUUID } from 'node:crypto';
import { spawn, spawnSync } from 'node:child_process';
import {
  closeSync,
  existsSync,
  fsyncSync,
  lstatSync,
  mkdtempSync,
  mkdirSync,
  openSync,
  readFileSync,
  readlinkSync,
  rmSync,
  symlinkSync,
  unlinkSync,
  writeSync,
} from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { validation } from './config.mjs';
import { requireTrustedLocalTestSandbox } from './trusted-sandbox.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const INHERITED_ENVIRONMENT_KEYS = new Set([
  'ASDF_DATA_DIR', 'ASDF_DIR', 'COMSPEC', 'DOTNET_ROOT', 'DOTNET_ROOT_X64',
  'DOTNET_ROOT_X86', 'LANG', 'LC_ALL', 'LC_CTYPE', 'MISE_DATA_DIR',
  'MISE_HOME', 'NVM_DIR', 'PATH', 'PATHEXT', 'VOLTA_HOME', 'SYSTEMROOT',
  'TZ', 'WINDIR',
]);
const APPROVED_TASK_PASS_ENVIRONMENT = new Map([
  ['dotnet-core-tests', new Set(['PLAYWRIGHT_BROWSERS_PATH'])],
]);
const MAX_OUTPUT = 1024 * 1024;
const MAX_LOG_BYTES = 16 * 1024 * 1024;
const LOG_TRUNCATION_MARKER = Buffer.from('\n[validation log truncated after 16 MiB]\n');
const PROGRESS_INTERVAL_MS = 60_000;

function hash(value) { return createHash('sha256').update(value).digest('hex'); }

export function sha256GitText(content) {
  const text = Buffer.isBuffer(content) ? content.toString('utf8') : content;
  return hash(text.replace(/\r\n/g, '\n'));
}

function stable(value) {
  if (Array.isArray(value)) return `[${value.map(stable).join(',')}]`;
  if (value && typeof value === 'object') {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${stable(value[key])}`).join(',')}}`;
  }
  return JSON.stringify(value);
}

function git(args) {
  const result = spawnSync('git', args, { cwd: ROOT, encoding: 'utf8', maxBuffer: 16 * 1024 * 1024 });
  if (result.error || result.status !== 0) throw new Error(`git ${args.join(' ')} failed: ${result.error?.message ?? result.stderr?.trim()}`);
  return result.stdout;
}

function repoFiles() {
  const tracked = git(['ls-files', '-z']).split('\0').filter(Boolean);
  const untracked = git(['ls-files', '--others', '--exclude-standard', '-z']).split('\0').filter(Boolean);
  return currentRepoFiles(tracked, untracked);
}

export function currentRepoFiles(tracked, untracked, root = ROOT) {
  return [...new Set([...tracked, ...untracked])].filter((file) => {
    try {
      lstatSync(path.join(root, file));
      return true;
    } catch (error) {
      if (error.code === 'ENOENT') return false;
      throw error;
    }
  }).sort();
}

function digestFile(relative, { text = false } = {}) {
  const full = path.join(ROOT, relative);
  const info = lstatSync(full);
  if (info.isSymbolicLink()) return hash(`symlink:${readlinkSync(full)}`);
  if (!info.isFile()) return hash(`not-a-file:${relative}`);
  const content = readFileSync(full);
  return text ? sha256GitText(content) : hash(content);
}

export function globRegex(pattern) {
  let expression = '^';
  for (let index = 0; index < pattern.length; index += 1) {
    const char = pattern[index];
    if (char === '*' && pattern[index + 1] === '*') {
      index += 1;
      if (pattern[index + 1] === '/') { index += 1; expression += '(?:.*/)?'; }
      else expression += '.*';
    } else if (char === '*') expression += '[^/]*';
    else expression += char.replace(/[|\\{}()[\]^$+?.]/g, '\\$&');
  }
  return new RegExp(`${expression}$`);
}

export function discoverInventory(files = repoFiles()) {
  const entries = [];
  const counts = {};
  const laneDigests = {};
  const ownership = new Set();
  for (const rule of validation.inventory) {
    const pattern = globRegex(rule.pattern);
    const matches = files.filter((file) => pattern.test(file));
    if (rule.required !== false && matches.length === 0) throw new Error(`Required inventory lane is empty: ${rule.id}`);
    counts[rule.id] = matches.length;
    const laneEntries = [];
    for (const file of matches) {
      if (ownership.has(file)) throw new Error(`Test inventory has duplicate ownership: ${file}`);
      ownership.add(file);
      if (lstatSync(path.join(ROOT, file)).isSymbolicLink()) throw new Error(`Test inventory contains a symbolic link: ${file}`);
      const entry = { file, lane: rule.id, framework: rule.framework, owner: rule.owner, sha256: digestFile(file, { text: true }) };
      entries.push(entry);
      laneEntries.push(entry);
    }
    laneDigests[rule.id] = hash(stable(laneEntries));
  }
  entries.sort((left, right) => left.file.localeCompare(right.file));
  return { entries, counts, laneDigests, sha256: hash(stable(entries)) };
}

function captureCandidate(inventory) {
  const commit = git(['rev-parse', 'HEAD']).trim();
  const tree = git(['rev-parse', 'HEAD^{tree}']).trim();
  const diffSha256 = hash(git(['diff', '--binary', 'HEAD']));
  const status = git(['status', '--porcelain=v1', '--untracked-files=all']);
  const inputs = validation.inputs.map((file) => ({ file, sha256: digestFile(file, { text: true }) }));
  const sourceSha256 = hash(stable({ commit, tree, diffSha256, inputs, inventorySha256: inventory.sha256 }));
  return {
    commit,
    tree,
    diffSha256,
    sourceSha256,
    inputDigests: inputs,
    inventorySha256: inventory.sha256,
    dirty: status.length > 0,
    untrackedCount: git(['ls-files', '--others', '--exclude-standard', '-z']).split('\0').filter(Boolean).length,
  };
}

function inspectRuntime(runtime) {
  const result = spawnSync(runtime.command, runtime.args ?? ['--version'], {
    cwd: ROOT, encoding: 'utf8', timeout: 10000, maxBuffer: 1024 * 1024,
    env: { ...inheritedToolchainEnvironment(), NO_COLOR: '1' },
  });
  const output = `${result.stdout ?? ''}${result.stderr ?? ''}`.trim();
  const version = output.split(/\r?\n/, 1)[0] ?? '';
  const available = !result.error && result.status === 0;
  const compatible = available && new RegExp(runtime.versionPattern).test(version);
  return { command: runtime.command, expected: runtime.versionPattern, version, available, compatible, exitCode: result.status };
}

function inheritedToolchainEnvironment(inherited = process.env) {
  const values = new Map();
  for (const [key, value] of Object.entries(inherited)) {
    const normalized = key.toUpperCase();
    if (!INHERITED_ENVIRONMENT_KEYS.has(normalized) || typeof value !== 'string') continue;
    if (!values.has(normalized) || key === normalized) values.set(normalized, value);
  }
  return Object.fromEntries(values);
}

function runnerContext(candidate) {
  const platform = process.platform === 'win32' ? 'windows' : process.platform === 'darwin' ? 'macos' : 'linux';
  const nodePlatform = platform === 'macos' ? 'osx' : platform === 'windows' ? 'win' : 'linux';
  const github = process.env.GITHUB_ACTIONS === 'true';
  return {
    executor: github ? 'github-actions' : 'local',
    platform,
    architecture: process.arch,
    nodeVersion: process.version,
    runner: process.env.RUNNER_NAME ?? os.hostname(),
    nodeId: process.env.VALIDATION_NODE_ID ?? `chaptarr-${nodePlatform}-${process.arch}`,
    github: github ? {
      runId: process.env.GITHUB_RUN_ID ?? null,
      runAttempt: process.env.GITHUB_RUN_ATTEMPT ?? null,
      sha: process.env.GITHUB_SHA ?? null,
    } : null,
    candidateCommit: candidate.commit,
  };
}

export function createPlan(options = {}) {
  const maxConcurrentTasks = validation.maxConcurrentTasks ?? 1;
  if (!Number.isInteger(maxConcurrentTasks) || maxConcurrentTasks < 1 || maxConcurrentTasks > 8) {
    throw new Error('Validation maxConcurrentTasks must be an integer from 1 through 8.');
  }
  const taskById = new Map();
  for (const task of validation.tasks) {
    if (typeof task.id !== 'string' || !/^[a-z0-9][a-z0-9-]*$/.test(task.id)) {
      throw new Error('Validation task ID must be a safe lowercase identifier.');
    }
    if (taskById.has(task.id)) throw new Error(`Duplicate validation task ID: ${task.id}`);
    taskById.set(task.id, task);
    const stage = task.stage ?? 'default';
    const concurrencyGroup = task.concurrencyGroup ?? task.id;
    if (typeof stage !== 'string' || !stage.trim() ||
        typeof concurrencyGroup !== 'string' || !concurrencyGroup.trim()) {
      throw new Error(`Validation task stage and concurrency group must be nonempty strings: ${task.id}`);
    }
    const passEnv = task.passEnv ?? [];
    if (!Array.isArray(passEnv) || passEnv.some((name) =>
      typeof name !== 'string' || !APPROVED_TASK_PASS_ENVIRONMENT.get(task.id)?.has(name))) {
      throw new Error('Task requests an unapproved inherited environment variable: ' + task.id);
    }
  }
  const stageOrder = new Map();
  let previousStageIndex = -1;
  for (const task of validation.tasks) {
    const stage = task.stage ?? 'default';
    if (!stageOrder.has(stage)) stageOrder.set(stage, stageOrder.size);
    const index = stageOrder.get(stage);
    if (index < previousStageIndex) throw new Error(`Validation tasks must be ordered by stage: ${task.id}`);
    previousStageIndex = index;
    if (!Array.isArray(task.dependsOn ?? []) || (task.dependsOn ?? []).some(
      (dependency) => typeof dependency !== 'string' || !taskById.has(dependency),
    )) {
      throw new Error(`Validation task has an invalid dependency list: ${task.id}`);
    }
  }
  const visiting = new Set();
  const visited = new Set();
  const visitTask = (taskId) => {
    if (visiting.has(taskId)) throw new Error(`Validation task dependency cycle includes ${taskId}`);
    if (visited.has(taskId)) return;
    visiting.add(taskId);
    const task = taskById.get(taskId);
    for (const dependency of task.dependsOn ?? []) {
      if (stageOrder.get(taskById.get(dependency).stage ?? 'default') > stageOrder.get(task.stage ?? 'default')) {
        throw new Error(`Validation task depends on a later stage: ${taskId} -> ${dependency}`);
      }
      visitTask(dependency);
    }
    visiting.delete(taskId);
    visited.add(taskId);
  };
  for (const task of validation.tasks) visitTask(task.id);

  const inventory = discoverInventory();
  const candidate = captureCandidate(inventory);
  const github = process.env.GITHUB_ACTIONS === 'true';
  const executionContext = runnerContext(candidate);
  const taskRunsOnNode = (task) => !task.nodeIds || task.nodeIds.some(
    (nodeId) => nodeId.toLowerCase() === executionContext.nodeId.toLowerCase(),
  );
  const selected = new Set(validation.tasks.filter((task) => {
    if (options.task) return task.id === options.task && taskRunsOnNode(task);
    return task.kind === 'test' && (!task.githubOnly || github) && taskRunsOnNode(task) && !task.optIn;
  }).map((task) => task.id));
  if (options.task && !selected.has(options.task)) {
    const task = validation.tasks.find((item) => item.id === options.task);
    throw new Error(`Task ${options.task} is assigned to validation node(s): ${task.nodeIds.join(', ')}`);
  }
  const queue = [...selected];
  while (queue.length) {
    const id = queue.pop();
    const task = validation.tasks.find((item) => item.id === id);
    for (const dependency of task?.dependsOn ?? []) {
      if (!selected.has(dependency)) { selected.add(dependency); queue.push(dependency); }
    }
  }
  const runtimes = new Map(validation.runtimes.map((runtime) => [`${runtime.command} ${stable(runtime.args ?? [])}`, runtime]));
  for (const task of validation.tasks.filter((item) => selected.has(item.id))) {
    for (const runtime of task.runtimes ?? []) runtimes.set(`${runtime.command} ${stable(runtime.args ?? [])}`, runtime);
  }
  const runtimeResults = [...runtimes.values()].map(inspectRuntime);
  const missingArtifacts = validation.tasks.filter((task) => selected.has(task.id))
    .flatMap((task) => task.artifacts ?? []).filter((file) => !existsSync(path.join(ROOT, file)));
  const tasks = validation.tasks.map((task) => ({
    id: task.id,
    name: task.name,
    kind: task.kind,
    stage: task.stage ?? 'default',
    concurrencyGroup: task.concurrencyGroup ?? task.id,
    selected: selected.has(task.id),
    reason: selected.has(task.id) ? null : task.githubOnly && !github
      ? 'requires the ephemeral GitHub PostgreSQL service'
      : !taskRunsOnNode(task)
        ? `assigned to validation node: ${task.nodeIds.join(', ')}`
        : 'not selected for this run',
    nodeIds: task.nodeIds ?? [],
    dependsOn: task.dependsOn ?? [],
    inventory: (task.inventory ?? []).map((lane) => ({ lane, count: inventory.counts[lane], sha256: inventory.laneDigests[lane] })),
    artifacts: task.artifacts ?? [],
  }));
  const body = {
    schemaVersion: 1,
    repository: validation.repository,
    selection: options.task ?? (options.testsOnly ? 'tests-only' : 'complete-local'),
    candidate,
    inventoryCounts: inventory.counts,
    inventoryLaneDigests: inventory.laneDigests,
    inventorySha256: inventory.sha256,
    runtimes: runtimeResults,
    missingArtifacts,
    maxConcurrentTasks,
    executionContext,
    tasks,
  };
  return { ...body, planSha256: hash(stable(body)) };
}

export function isolatedChildEnvironment(task, runRoot, inherited = process.env) {
  if (typeof task?.id !== 'string' || !/^[a-z0-9][a-z0-9-]*$/.test(task.id)) {
    throw new Error('Validation task ID must be a safe lowercase identifier.');
  }
  const env = inheritedToolchainEnvironment(inherited);
  const approvedPassEnv = APPROVED_TASK_PASS_ENVIRONMENT.get(task.id) ?? new Set();
  const passEnv = task.passEnv ?? [];
  if (!Array.isArray(passEnv) || passEnv.some((name) =>
    typeof name !== 'string' || !approvedPassEnv.has(name))) {
    throw new Error('Task requests an unapproved inherited environment variable: ' + task.id);
  }
  for (const name of passEnv) {
    const match = Object.entries(inherited).find(([key]) => key.toUpperCase() === name.toUpperCase());
    if (match && typeof match[1] === 'string') env[name] = match[1];
  }
  for (const [key, value] of Object.entries(task.env ?? {})) {
    if (typeof value === 'string') env[key] = value;
  }

  const environmentRoot = path.join(runRoot, `${task.id}-environment`);
  const directories = {
    HOME: path.join(environmentRoot, 'home'),
    USERPROFILE: path.join(environmentRoot, 'home'),
    TMPDIR: path.join(environmentRoot, 'tmp'),
    TMP: path.join(environmentRoot, 'tmp'),
    TEMP: path.join(environmentRoot, 'tmp'),
    XDG_CONFIG_HOME: path.join(environmentRoot, 'xdg-config'),
    XDG_CACHE_HOME: path.join(environmentRoot, 'xdg-cache'),
    APPDATA: path.join(environmentRoot, 'AppData', 'Roaming'),
    LOCALAPPDATA: path.join(environmentRoot, 'AppData', 'Local'),
    DOTNET_CLI_HOME: path.join(environmentRoot, 'dotnet-home'),
    NUGET_PACKAGES: path.join(runRoot, 'nuget-packages'),
  };
  for (const directory of new Set(Object.values(directories))) {
    mkdirSync(directory, { recursive: true, mode: 0o700 });
  }
  Object.assign(env, directories, {
    CI: 'true',
    NO_COLOR: '1',
    DOTNET_CLI_TELEMETRY_OPTOUT: '1',
    DOTNET_NOLOGO: '1',
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1',
  });
  if (task.kind === 'test') env.NODE_ENV = 'test';
  return env;
}

export function createShortTemporaryPathAlias(
  targetDirectory,
  aliasParent = process.platform === 'linux' ? '/dev/shm' : '/tmp',
) {
  const aliasParents = [...new Set([aliasParent, '/dev/shm', '/tmp'])];
  let lastError;

  for (const parent of aliasParents) {
    let aliasRoot;
    try {
      aliasRoot = mkdtempSync(path.join(parent, 'cng-'));
      const directory = path.join(aliasRoot, 't');
      symlinkSync(targetDirectory, directory, 'dir');
      return {
        directory,
        cleanup: () => rmSync(aliasRoot, { recursive: true, force: true }),
      };
    } catch (error) {
      if (aliasRoot) rmSync(aliasRoot, { recursive: true, force: true });
      lastError = error;
    }
  }

  throw lastError;
}

function terminate(child) {
  if (!child.pid) return;
  if (process.platform === 'win32') {
    spawn('taskkill', ['/pid', String(child.pid), '/T', '/F'], { stdio: 'ignore', windowsHide: true });
    return;
  }
  try { process.kill(-child.pid, 'SIGTERM'); } catch { child.kill('SIGTERM'); }
  const timer = setTimeout(() => {
    try { process.kill(-child.pid, 'SIGKILL'); } catch { child.kill('SIGKILL'); }
  }, 2000);
  timer.unref();
}

export function parseNodeTestSummary(output) {
  const read = (name) => {
    const match = [...output.matchAll(new RegExp(`^# ${name} (\\d+)\\s*$`, 'gm'))].at(-1);
    return match ? Number(match[1]) : null;
  };
  const summary = {
    total: read('tests'),
    passed: read('pass'),
    failed: read('fail'),
    cancelled: read('cancelled') ?? 0,
    skipped: read('skipped') ?? 0,
    todo: read('todo') ?? 0,
  };
  const measured = [summary.total, summary.passed, summary.failed, summary.cancelled, summary.skipped, summary.todo];
  if (!measured.every(Number.isFinite) || summary.total < 1 ||
      summary.passed + summary.failed + summary.cancelled + summary.skipped + summary.todo !== summary.total) {
    throw new Error('Node TAP summary has missing, invalid, zero, or inconsistent case counts');
  }
  return { ...summary, inconclusive: summary.todo };
}

function decodeNUnitXmlText(value) {
  return value
    .replace(/<!\[CDATA\[([\s\S]*?)\]\]>/g, '$1')
    .replace(/&#x([0-9a-f]+);/gi, (_, hex) => String.fromCodePoint(Number.parseInt(hex, 16)))
    .replace(/&#([0-9]+);/g, (_, decimal) => String.fromCodePoint(Number.parseInt(decimal, 10)))
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, '&');
}

function nunitFailureType(body) {
  const failure = body.match(/<failure\b([^>]*)>([\s\S]*?)<\/failure>/i);
  if (!failure) return null;
  const exceptionAttribute = failure[1].match(/\bexception-type="([^"]+)"/i)?.[1];
  if (exceptionAttribute) return decodeNUnitXmlText(exceptionAttribute);

  const message = failure[2].match(/<message\b[^>]*>([\s\S]*?)<\/message>/i)?.[1];
  const decodedMessage = message ? decodeNUnitXmlText(message) : null;
  const messageExceptionType = decodedMessage?.split(/\r?\n/).map((line) => line.trim()).find(Boolean)
    ?.match(/^([A-Za-z_][\w.+`]*(?:Exception|Error))\b/)?.[1];
  if (messageExceptionType) return messageExceptionType;

  const stackTrace = failure[2].match(/<stack-trace\b[^>]*>([\s\S]*?)<\/stack-trace>/i)?.[1];
  const firstStackLine = stackTrace ? decodeNUnitXmlText(stackTrace).split(/\r?\n/).map((line) => line.trim()).find(Boolean) : null;
  const stackExceptionType = firstStackLine?.match(/^([A-Za-z_][\w.+`]*(?:Exception|Error))\b/)?.[1];
  if (stackExceptionType) return stackExceptionType;

  if (decodedMessage && /^\s*Expected:/m.test(decodedMessage)) return 'NUnit.Framework.AssertionException';
  return null;
}

function nunitFailureFrame(body) {
  const failure = body.match(/<failure\b[^>]*>([\s\S]*?)<\/failure>/i);
  if (!failure) return null;
  const stackTrace = failure[1].match(/<stack-trace\b[^>]*>([\s\S]*?)<\/stack-trace>/i)?.[1];
  if (!stackTrace) return null;
  const methods = decodeNUnitXmlText(stackTrace).split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => /^at\s+/.test(line))
    .map((line) => line.match(/^at\s+([^ (]+)\s*\(/)?.[1])
    .filter(Boolean);
  const method = methods.find((candidate) => candidate.startsWith('NzbDrone.')) ?? methods[0];
  return method && /^[A-Za-z0-9_.$+`<>-]+$/.test(method) ? method : null;
}

function nunitFailureSqliteErrorCode(body) {
  const failure = body.match(/<failure\b[^>]*>([\s\S]*?)<\/failure>/i);
  if (!failure) return null;
  const message = failure[1].match(/<message\b[^>]*>([\s\S]*?)<\/message>/i)?.[1];
  if (!message) return null;
  const code = decodeNUnitXmlText(message).match(/\bSQLite Error\s+(\d+):/i)?.[1];
  return code === undefined ? null : Number(code);
}

export function parseNUnitSummary(xml) {
  const root = xml.match(/<test-run\b[^>]*>/)?.[0];
  if (!root) throw new Error('NUnit XML is missing its test-run summary');
  const attrs = Object.fromEntries([...root.matchAll(/([A-Za-z][A-Za-z0-9_-]*)="([^"]*)"/g)].map((match) => [match[1], match[2]]));
  const failedTestNames = [];
  const failedTestDetails = [];
  for (const match of xml.matchAll(/<test-case\b[^>]*>/g)) {
    const tag = match[0];
    const result = tag.match(/\bresult="([^"]+)"/)?.[1];
    const name = tag.match(/\bname="([^"]+)"/)?.[1];
    if (result !== 'Failed' || !name) continue;
    failedTestNames.push(name);
    const bodyStart = match.index + tag.length;
    const bodyEnd = xml.indexOf('</test-case>', bodyStart);
    const body = tag.endsWith('/>') || bodyEnd < 0 ? '' : xml.slice(bodyStart, bodyEnd);
    const detail = { name, exceptionType: nunitFailureType(body), stackFrame: nunitFailureFrame(body) };
    const sqliteErrorCode = nunitFailureSqliteErrorCode(body);
    if (sqliteErrorCode !== null) detail.sqliteErrorCode = sqliteErrorCode;
    failedTestDetails.push(detail);
  }
  const summary = {
    total: Number(attrs.total ?? attrs.testcasecount),
    passed: Number(attrs.passed),
    failed: Number(attrs.failed),
    skipped: Number(attrs.skipped ?? 0),
    inconclusive: Number(attrs.inconclusive ?? 0),
    result: attrs.result ?? null,
  };
  if (![summary.total, summary.passed, summary.failed, summary.skipped, summary.inconclusive].every(Number.isFinite) || summary.total < 1) {
    throw new Error('NUnit XML has missing, invalid, or zero case counts');
  }
  return {
    ...summary,
    failedTestNames: failedTestNames.slice(0, 100),
    failedTestNamesTruncated: failedTestNames.length > 100,
    failedTestDetails: failedTestDetails.slice(0, 100),
    failedTestDetailsTruncated: failedTestDetails.length > 100,
  };
}

function formatElapsed(milliseconds) {
  const seconds = Math.floor(milliseconds / 1000);
  return `${Math.floor(seconds / 60)}m${String(seconds % 60).padStart(2, '0')}s`;
}

export function formatValidationProgress(repository, tasks, results, currentTask = null, elapsedMs = 0) {
  const receipts = tasks.map((task) => results.get(task.id)).filter(Boolean);
  const passed = receipts.filter((receipt) => receipt.status === 'passed').length;
  const failed = receipts.filter((receipt) => ['failed', 'incomplete'].includes(receipt.status)).length;
  const blocked = receipts.filter((receipt) => ['blocked', 'not-run'].includes(receipt.status)).length;
  const completed = passed + failed;
  const pending = Math.max(0, tasks.length - completed - blocked);
  const activeTasks = currentTask instanceof Map
    ? [...currentTask].map(([taskId, startedAt]) => {
      const task = tasks.find((item) => item.id === taskId);
      return `${task?.name ?? taskId} (${formatElapsed(Date.now() - startedAt)})`;
    })
    : currentTask ? [`${currentTask.name ?? currentTask.id} (${formatElapsed(elapsedMs)})`] : [];
  const active = activeTasks.length ? `; running ${activeTasks.join(', ')}` : '';
  return `[validation] ${repository}: ${completed}/${tasks.length} selected tasks complete (${passed} passed, ${failed} failed, ${blocked} blocked, ${pending} pending)${active}`;
}

function writeValidationProgress(tasks, results, runningTasks = new Map(), state = null) {
  const summary = formatValidationProgress(
    validation.repository,
    tasks,
    results,
    runningTasks,
  );
  if (state?.last === summary) return;
  if (state) state.last = summary;
  process.stderr.write(`${summary}\n`);
}

export async function runTaskPool(tasks, {
  maxConcurrentTasks = 1,
  results = new Map(),
  runTask: executeTask,
  checkSource = () => true,
  onTaskStart = () => {},
  onTaskFinish = () => {},
  onProgress = () => {},
} = {}) {
  if (!Number.isInteger(maxConcurrentTasks) || maxConcurrentTasks < 1 || maxConcurrentTasks > 8) {
    throw new Error('maxConcurrentTasks must be an integer from 1 through 8.');
  }
  if (typeof executeTask !== 'function' || typeof checkSource !== 'function') {
    throw new Error('runTask and checkSource must be functions.');
  }

  const pending = [...tasks];
  const selectedIds = new Set(pending.map((task) => task.id));
  const metadata = (task) => ({
    stage: task.stage ?? 'default',
    concurrencyGroup: task.concurrencyGroup ?? task.id,
    dependsOn: task.dependsOn ?? [],
  });
  const block = (task, reason) => results.set(task.id, {
    taskId: task.id,
    ...metadata(task),
    status: 'blocked',
    reason,
    required: task.required !== false,
  });
  let sourceChanged = false;

  while (pending.length) {
    if (!checkSource()) {
      sourceChanged = true;
      for (const [taskId, receipt] of results) {
        if (receipt.status === 'passed') results.set(taskId, {
          ...receipt,
          status: 'blocked',
          reason: 'candidate source or compiled test artifact changed during task execution',
        });
      }
      for (const task of pending.splice(0)) block(task, 'candidate changed before the next task batch started');
      onProgress();
      break;
    }

    for (let index = pending.length - 1; index >= 0; index -= 1) {
      const task = pending[index];
      const unavailableDependency = (task.dependsOn ?? []).find((dependency) => {
        const receipt = results.get(dependency);
        return receipt ? receipt.status !== 'passed' : !selectedIds.has(dependency);
      });
      if (!unavailableDependency) continue;
      block(task, 'required dependency did not pass');
      pending.splice(index, 1);
      onProgress();
    }

    const stage = pending[0]?.stage ?? 'default';
    const groups = new Set();
    const batch = [];
    for (const task of pending) {
      if ((task.stage ?? 'default') !== stage) continue;
      if ((task.dependsOn ?? []).some((dependency) => results.get(dependency)?.status !== 'passed')) continue;
      const group = task.concurrencyGroup ?? task.id;
      if (groups.has(group)) continue;
      groups.add(group);
      batch.push(task);
      if (batch.length >= maxConcurrentTasks) break;
    }
    if (!batch.length) {
      for (const task of pending.splice(0)) block(task, 'required dependency did not pass or no task is runnable');
      onProgress();
      break;
    }

    for (const task of batch) pending.splice(pending.indexOf(task), 1);
    const startedAt = new Map(batch.map((task) => [task.id, Date.now()]));
    for (const task of batch) onTaskStart(task, startedAt.get(task.id));
    onProgress();
    await Promise.all(batch.map(async (task) => {
      let receipt;
      try {
        receipt = await executeTask(task);
        if (!receipt || receipt.taskId !== task.id ||
            !['passed', 'failed', 'incomplete', 'blocked', 'not-run'].includes(receipt.status)) {
          throw new Error('task runner returned an invalid receipt');
        }
      } catch (error) {
        receipt = {
          taskId: task.id,
          status: 'failed',
          error: error.message,
          required: task.required !== false,
        };
      }
      receipt = { ...receipt, ...metadata(task) };
      results.set(task.id, receipt);
      onTaskFinish(task, receipt);
      onProgress();
    }));

    if (!checkSource()) {
      sourceChanged = true;
      for (const [taskId, receipt] of results) {
        if (receipt.status === 'passed') results.set(taskId, {
          ...receipt,
          status: 'blocked',
          reason: 'candidate source or compiled test artifact changed during concurrent execution',
        });
      }
      for (const task of pending.splice(0)) block(task, 'candidate changed during task batch');
      onProgress();
    }
  }
  return { results, sourceChanged };
}

function hashArtifacts(task) {
  return (task.artifacts ?? []).map((file) => {
    const full = path.join(ROOT, file);
    if (!existsSync(full) || !lstatSync(full).isFile()) return { file, available: false, sha256: null };
    return { file, available: true, sha256: digestFile(file) };
  });
}

export async function runTask(task, runRoot, testSpec = null) {
  const spec = testSpec ?? validation.tasks.find((item) => item.id === task.id);
  const resultXml = path.join(runRoot, `${task.id}.nunit.xml`);
  const args = spec.command.slice(1).map((arg) => arg.replaceAll('{RESULTS}', resultXml));
  const logPath = path.join(runRoot, `${task.id}.log`);
  const logFd = openSync(logPath, 'wx', 0o600);
  const logContentLimit = MAX_LOG_BYTES - LOG_TRUNCATION_MARKER.length;
  let logBytesWritten = 0;
  let logTruncated = false;
  let logError = null;
  const stdout = [];
  const stderr = [];
  let outputBytes = 0;
  const persist = (chunk) => {
    if (logError) return;
    const length = Math.min(chunk.length, logContentLimit - logBytesWritten);
    let offset = 0;
    try {
      while (offset < length)
        offset += writeSync(logFd, chunk, offset, length - offset);
    } catch (error) {
      logError = error.message;
      return;
    }
    logBytesWritten += length;
    if (length < chunk.length) logTruncated = true;
  };
  const capture = (parts, chunk) => {
    outputBytes += chunk.length;
    persist(chunk);
    parts.push(chunk.toString('utf8'));
    while (parts.reduce((sum, part) => sum + part.length, 0) > MAX_OUTPUT) parts.shift();
  };
  const artifactsBefore = hashArtifacts(spec);
  const startedAt = new Date().toISOString();
  const started = Date.now();
  const childEnvironment = isolatedChildEnvironment(spec, runRoot);
  const needsShortTemporaryPath = process.platform === 'linux' && (
    spec.shortTemporaryPath === true ||
    childEnvironment.DIRECT_DOWNLOAD_REQUIRE_PLAYWRIGHT === 'true'
  );
  let temporaryAlias = null;
  let timedOut = false;
  let child;
  let exit;
  try {
    if (needsShortTemporaryPath) {
      temporaryAlias = createShortTemporaryPathAlias(childEnvironment.TMPDIR);
      childEnvironment.TMPDIR = temporaryAlias.directory;
      childEnvironment.TMP = temporaryAlias.directory;
      childEnvironment.TEMP = temporaryAlias.directory;
    }

    child = spawn(spec.command[0], args, {
      cwd: ROOT,
      env: childEnvironment,
      stdio: ['ignore', 'pipe', 'pipe'],
      detached: process.platform !== 'win32',
      windowsHide: true,
    });
    child.stdout.on('data', (chunk) => capture(stdout, chunk));
    child.stderr.on('data', (chunk) => capture(stderr, chunk));
    const timer = setTimeout(() => { timedOut = true; terminate(child); }, (spec.timeoutSeconds ?? 3600) * 1000);
    exit = await new Promise((resolve) => {
      child.once('error', (error) => resolve({ code: null, signal: null, error }));
      child.once('close', (code, signal) => resolve({ code, signal, error: null }));
    }).finally(() => clearTimeout(timer));
  } finally {
    try {
      if (logTruncated && !logError)
        writeSync(logFd, LOG_TRUNCATION_MARKER);
      fsyncSync(logFd);
    } catch (error) {
      logError ??= error.message;
    } finally {
      try {
        closeSync(logFd);
      } finally {
        temporaryAlias?.cleanup();
      }
    }
  }

  const output = `${stdout.join('')}\n${stderr.join('')}`;
  let activeCases = null;
  let passedCases = null;
  let failedCases = null;
  let skippedCases = null;
  let inconclusiveCases = null;
  let failedTestNames = [];
  let failedTestNamesTruncated = false;
  let failedTestDetails = [];
  let failedTestDetailsTruncated = false;
  let summaryError = null;
  if (task.id === 'dotnet-core-tests') {
    try {
      const summary = parseNUnitSummary(readFileSync(resultXml, 'utf8'));
      activeCases = summary.total;
      passedCases = summary.passed;
      failedCases = summary.failed;
      skippedCases = summary.skipped;
      inconclusiveCases = summary.inconclusive;
      failedTestNames = summary.failedTestNames;
      failedTestNamesTruncated = summary.failedTestNamesTruncated;
      failedTestDetails = summary.failedTestDetails;
      failedTestDetailsTruncated = summary.failedTestDetailsTruncated;
      if (summary.result && summary.result !== 'Passed') summaryError = `NUnit reported ${summary.result}`;
    } catch (error) { summaryError = error.message; }
  } else if (spec.resultFormat === 'node-test') {
    try {
      const summary = parseNodeTestSummary(output);
      activeCases = summary.total;
      passedCases = summary.passed;
      failedCases = summary.failed;
      skippedCases = summary.skipped;
      inconclusiveCases = summary.inconclusive;
      if (summary.cancelled > 0) summaryError = `Node TAP reported ${summary.cancelled} cancelled cases`;
    } catch (error) { summaryError = error.message; }
  } else {
    const count = [...output.matchAll(/VALIDATION_TEST_CASES_PASSED=(\d+)/g)].reduce((sum, match) => sum + Number(match[1]), 0);
    activeCases = count;
    passedCases = count;
    failedCases = 0;
  }
  const artifactsAfter = hashArtifacts(spec);
  const artifactsChanged = stable(artifactsBefore) !== stable(artifactsAfter);
  const status = timedOut || exit.error || exit.code !== 0 || summaryError || artifactsChanged || logError ||
    activeCases === null || activeCases < 1 || failedCases > 0 ? 'failed' : 'passed';
  return {
    taskId: task.id,
    status,
    startedAt,
    durationMs: Date.now() - started,
    exitCode: exit.code,
    signal: exit.signal,
    error: exit.error?.message ?? summaryError ?? (artifactsChanged ? 'compiled test artifact changed during execution' : null) ?? logError,
    timedOut,
    outputBytes,
    logSha256: hash(readFileSync(logPath)),
    logFile: path.basename(logPath),
    activeCases,
    passedCases,
    failedCases,
    skippedCases,
    inconclusiveCases,
    failedTestNames,
    failedTestNamesTruncated,
    failedTestDetails,
    failedTestDetailsTruncated,
    inventory: task.inventory,
    artifactInputs: artifactsBefore,
  };
}

function acquireLock() {
  const lockPath = path.join(os.tmpdir(), `chaptarr-validation-${hash(ROOT).slice(0, 16)}.lock`);
  const content = `${JSON.stringify({ pid: process.pid, startedAt: new Date().toISOString() })}\n`;
  const fd = openSync(lockPath, 'wx', 0o600);
  writeSync(fd, content);
  closeSync(fd);
  return () => {
    try {
      if (readFileSync(lockPath, 'utf8') === content) unlinkSync(lockPath);
    } catch { /* keep any replacement lock intact */ }
  };
}

function safeExport(value) {
  const target = path.resolve(value);
  const bases = [os.tmpdir(), process.env.RUNNER_TEMP].filter(Boolean).map((item) => path.resolve(item));
  if (!bases.some((base) => target.startsWith(`${base}${path.sep}`))) throw new Error('Receipt export must be under the system or runner temporary directory');
  return target;
}

function exportReceipt(receipt) {
  const destination = process.env.VALIDATION_RECEIPT_EXPORT;
  if (!destination) return;
  const target = safeExport(destination);
  mkdirSync(path.dirname(target), { recursive: true });
  const fd = openSync(target, 'w', 0o600);
  writeSync(fd, JSON.stringify(receipt, null, 2));
  closeSync(fd);
}

async function execute(plan) {
  const runRoot = path.join(os.tmpdir(), `chaptarr-validation-${randomUUID()}`);
  mkdirSync(runRoot, { mode: 0o700 });
  const releaseLock = acquireLock();
  const results = new Map();
  let sourceChanged = false;
  try {
    const missing = plan.runtimes.some((item) => !item.available || !item.compatible) ||
      plan.missingArtifacts.length > 0 ||
      (plan.executionContext.github && plan.executionContext.github.sha !== plan.candidate.commit);
    const selected = plan.tasks.filter((task) => task.selected);
    const progressState = {};
    for (const task of plan.tasks.filter((item) => !item.selected)) results.set(task.id, {
      taskId: task.id,
      status: 'excluded',
      reason: task.reason,
      required: true,
    });
    if (missing) {
      for (const task of selected) results.set(task.id, { taskId: task.id, status: 'not-run', reason: 'runtime, test artifact, or workflow SHA prerequisite is missing' });
    } else {
      const runningTasks = new Map();
      const reportProgress = () => writeValidationProgress(selected, results, runningTasks, progressState);
      const heartbeat = setInterval(reportProgress, PROGRESS_INTERVAL_MS);
      heartbeat.unref();
      try {
        const pool = await runTaskPool(selected, {
          maxConcurrentTasks: plan.maxConcurrentTasks,
          results,
          checkSource: () => captureCandidate(discoverInventory()).sourceSha256 === plan.candidate.sourceSha256,
          runTask: async (task) => {
            const spec = validation.tasks.find((item) => item.id === task.id);
            if (spec.githubOnly && process.env.GITHUB_ACTIONS !== 'true') {
              return { taskId: task.id, status: 'not-run', reason: 'requires the ephemeral GitHub PostgreSQL service' };
            }
            return runTask(task, runRoot);
          },
          onTaskStart: (task, startedAt) => runningTasks.set(task.id, startedAt),
          onTaskFinish: (task) => runningTasks.delete(task.id),
          onProgress: reportProgress,
        });
        sourceChanged = pool.sourceChanged;
      } finally {
        clearInterval(heartbeat);
      }
    }
    for (const task of plan.tasks) {
      if (!results.has(task.id)) results.set(task.id, {
        taskId: task.id,
        status: task.selected ? 'blocked' : 'excluded',
        reason: task.selected ? 'a prerequisite task did not pass' : task.reason,
      });
    }
    writeValidationProgress(selected, results, new Map(), progressState);
    const tasks = plan.tasks.map((task) => ({
      ...results.get(task.id),
      stage: task.stage,
      concurrencyGroup: task.concurrencyGroup,
      dependsOn: task.dependsOn,
    }));
    const totals = {
      planned: plan.tasks.filter((task) => task.selected).length,
      executed: tasks.filter((task) => task.startedAt).length,
      passed: tasks.filter((task) => task.status === 'passed').length,
      failed: tasks.filter((task) => task.status === 'failed').length,
      blocked: tasks.filter((task) => task.status === 'blocked' || task.status === 'not-run').length,
      excluded: tasks.filter((task) => task.status === 'excluded').length,
      activeCases: tasks.reduce((sum, task) => sum + (task.activeCases ?? 0), 0),
      passedCases: tasks.reduce((sum, task) => sum + (task.passedCases ?? 0), 0),
      failedCases: tasks.reduce((sum, task) => sum + (task.failedCases ?? 0), 0),
      skippedCases: tasks.reduce((sum, task) => sum + (task.skippedCases ?? 0), 0),
      inconclusiveCases: tasks.reduce((sum, task) => sum + (task.inconclusiveCases ?? 0), 0),
    };
    const status = sourceChanged || totals.failed > 0 || totals.blocked > 0 || totals.passed !== totals.planned ? 'failed' : 'passed';
    const receipt = {
      schemaVersion: 1,
      repository: validation.repository,
      runId: randomUUID(),
      startedAt: new Date().toISOString(),
      completedAt: new Date().toISOString(),
      planSha256: plan.planSha256,
      candidate: plan.candidate,
      inventoryCounts: plan.inventoryCounts,
      inventoryLaneDigests: plan.inventoryLaneDigests,
      inventorySha256: plan.inventorySha256,
      executionContext: plan.executionContext,
      maxConcurrentTasks: plan.maxConcurrentTasks,
      status,
      sourceChanged,
      runtimes: plan.runtimes,
      tasks,
      totals,
      evidenceDirectory: runRoot,
    };
    const fd = openSync(path.join(runRoot, 'receipt.json'), 'w', 0o600);
    writeSync(fd, JSON.stringify(receipt, null, 2));
    closeSync(fd);
    exportReceipt(receipt);
    return { receipt, runRoot, exitCode: status === 'passed' ? 0 : 1 };
  } finally { releaseLock(); }
}

function help() {
  process.stdout.write([
    'ChaptarrNG validation engine',
    '',
    'Usage:',
    '  node scripts/validation-engine/engine.mjs --plan [--json] [--tests-only]',
    '  node scripts/validation-engine/engine.mjs --tests-only',
    '  node scripts/validation-engine/engine.mjs --task <task-id>',
    '',
    'The PostgreSQL smoke task requires the ephemeral GitHub Actions service.',
    '',
  ].join('\n'));
}

async function main(argv = process.argv.slice(2)) {
  if (argv.includes('--help')) { help(); return 0; }
  const options = { testsOnly: argv.includes('--tests-only') };
  const taskIndex = argv.indexOf('--task');
  if (taskIndex !== -1) options.task = argv[taskIndex + 1];
  const known = new Set(['--help', '--plan', '--json', '--tests-only', '--task']);
  for (let index = 0; index < argv.length; index += 1) {
    if (argv[index] === '--task') index += 1;
    else if (!known.has(argv[index])) throw new Error(`Unknown argument: ${argv[index]}`);
  }
  if (options.task && !validation.tasks.some((task) => task.id === options.task)) throw new Error(`Unknown task: ${options.task}`);
  const plan = createPlan(options);
  if (argv.includes('--plan')) {
    if (argv.includes('--json')) process.stdout.write(`${JSON.stringify(plan, null, 2)}\n`);
    else {
      process.stdout.write(`Repository: ${plan.repository}\nCandidate: ${plan.candidate.sourceSha256}\n`);
      process.stdout.write(`Inventory: ${JSON.stringify(plan.inventoryCounts)}\n`);
      for (const runtime of plan.runtimes) process.stdout.write(`Runtime ${runtime.command}: ${runtime.version} (${runtime.compatible ? 'ready' : 'unavailable or mismatched'})\n`);
      if (plan.missingArtifacts.length) process.stdout.write(`Missing build outputs: ${plan.missingArtifacts.join(', ')}\n`);
      for (const task of plan.tasks) process.stdout.write(`${task.selected ? '[selected]' : '[excluded]'} ${task.id}: ${task.name}${task.reason ? ` (${task.reason})` : ''}\n`);
    }
    return 0;
  }
  if (plan.tasks.some((task) => task.selected && task.kind === 'test')) {
    requireTrustedLocalTestSandbox();
  }
  const result = await execute(plan);
  process.stdout.write(`${JSON.stringify({ repository: validation.repository, status: result.receipt.status, totals: result.receipt.totals, evidenceDirectory: result.runRoot }, null, 2)}\n`);
  return result.exitCode;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().then((code) => { process.exitCode = code; }).catch((error) => {
    process.stderr.write(`${error.stack ?? error.message}\n`);
    process.exitCode = 2;
  });
}
