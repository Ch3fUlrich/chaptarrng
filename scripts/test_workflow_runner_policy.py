import os
import re
import subprocess
import tempfile
import unittest
from pathlib import Path


WORKFLOW = Path(__file__).resolve().parents[1] / ".github" / "workflows" / "build.yml"


def event_block(source: str, event: str) -> str:
    lines = source.splitlines()
    start = lines.index(f"  {event}:")
    body = []
    for line in lines[start + 1 :]:
        if line.strip() and len(line) - len(line.lstrip()) <= 2:
            break
        body.append(line)
    return "\n".join(body)


def job_block(source: str, job: str) -> str:
    lines = source.splitlines()
    start = lines.index(f"  {job}:")
    body = []
    for line in lines[start + 1 :]:
        if line.strip() and len(line) - len(line.lstrip()) <= 2:
            break
        body.append(line)
    return "\n".join(body)


def step_block(source: str, name: str) -> str:
    lines = source.splitlines()
    marker = f"    - name: {name}"
    start = lines.index(marker)
    body = []
    for line in lines[start + 1 :]:
        if line.startswith("    - name:"):
            break
        body.append(line)
    return "\n".join(body)


class WorkflowRunnerPolicyTests(unittest.TestCase):
    def test_push_trigger_is_main_only(self) -> None:
        push = event_block(WORKFLOW.read_text(encoding="utf-8"), "push")
        self.assertRegex(push, r"(?m)^    branches:\s*\[\s*main\s*\]$")
        self.assertNotIn("**", push)

    def test_build_matrix_uses_hosted_runners(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        build = job_block(source, "build")
        runner_match = re.search(r"(?m)^    runs-on: (?P<runner>[^\n]+)$", build)
        self.assertIsNotNone(runner_match)
        self.assertEqual(runner_match.group("runner"), "${{ matrix.os }}")
        self.assertNotIn("self-hosted", source)
        self.assertNotIn("TRUSTED_MAIN_RUNNERS_ENABLED", source)

    def test_pr_body_edits_revalidate_notes_without_restarting_builds(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        pull_request = event_block(source, "pull_request")
        release_notes = job_block(source, "release-notes")
        build = job_block(source, "build")
        migrations = job_block(source, "postgres-migrations")

        self.assertRegex(pull_request, r"(?m)^    types:.*\bedited\b")
        self.assertIn("github.event.changes.body != null", release_notes)
        self.assertIn("github.event.action != 'edited'", build)
        self.assertIn("github.event.action != 'edited'", migrations)
        self.assertIn("github.event.action == 'edited' && 'metadata' || 'source'", source)

    def test_release_note_check_fetches_the_pr_head_after_merge(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        release_notes = job_block(source, "release-notes")

        self.assertIn("PR_NUMBER: ${{ github.event.pull_request.number }}", release_notes)
        fetch = (
            'git fetch --no-tags origin "refs/pull/${PR_NUMBER}/head:'
            'refs/remotes/pull/${PR_NUMBER}/head"'
        )
        self.assertIn(fetch, release_notes)
        self.assertIn('git cat-file -e "${HEAD_SHA}^{commit}"', release_notes)
        self.assertLess(release_notes.index(fetch), release_notes.index("node scripts/check-release-notes.mjs"))

    def test_retained_pull_ref_fetches_head_without_source_branch(self) -> None:
        with tempfile.TemporaryDirectory(prefix="chaptarr-pr-ref-") as scratch:
            root = Path(scratch)
            remote = root / "remote.git"
            seed = root / "seed"
            checkout = root / "checkout"
            subprocess.run(["git", "init", "--bare", str(remote)], check=True, capture_output=True)
            subprocess.run(["git", "init", "-b", "main", str(seed)], check=True, capture_output=True)
            environment = os.environ | {
                "GIT_AUTHOR_NAME": "Validation Test",
                "GIT_AUTHOR_EMAIL": "validation@example.invalid",
                "GIT_COMMITTER_NAME": "Validation Test",
                "GIT_COMMITTER_EMAIL": "validation@example.invalid",
            }

            def git(*args: str) -> str:
                result = subprocess.run(
                    ["git", "-C", str(seed), *args],
                    check=True,
                    capture_output=True,
                    text=True,
                    env=environment,
                )
                return result.stdout.strip()

            (seed / "base.txt").write_text("base\n", encoding="utf-8")
            git("add", "base.txt")
            git("commit", "-m", "base")
            git("remote", "add", "origin", str(remote))
            git("push", "origin", "main")
            (seed / "head.txt").write_text("pull request head\n", encoding="utf-8")
            git("add", "head.txt")
            git("commit", "-m", "pull request head")
            head = git("rev-parse", "HEAD")
            git("push", "origin", "HEAD:refs/heads/pr-branch")
            subprocess.run(
                ["git", "--git-dir", str(remote), "update-ref", "refs/pull/75/head", head],
                check=True,
                capture_output=True,
            )
            subprocess.run(
                ["git", "--git-dir", str(remote), "update-ref", "-d", "refs/heads/pr-branch"],
                check=True,
                capture_output=True,
            )
            subprocess.run(
                ["git", "clone", "--branch", "main", "--single-branch", str(remote), str(checkout)],
                check=True,
                capture_output=True,
            )

            subprocess.run(
                [
                    "git",
                    "-C",
                    str(checkout),
                    "fetch",
                    "--no-tags",
                    "origin",
                    "refs/pull/75/head:refs/remotes/pull/75/head",
                ],
                check=True,
                capture_output=True,
            )
            subprocess.run(
                ["git", "-C", str(checkout), "cat-file", "-e", f"{head}^{{commit}}"],
                check=True,
                capture_output=True,
            )

    def test_playwright_install_uses_hosted_linux_dependencies(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn("Install Chromium and system dependencies on GitHub-hosted Linux", source)
        self.assertIn("install --with-deps chromium", source)
        self.assertNotIn("runner.environment", source)

    def test_frontend_build_runs_after_validation_receipts_are_exported(self) -> None:
        build = job_block(WORKFLOW.read_text(encoding="utf-8"), "build")

        receipt_upload = build.index("- name: Upload validation receipt")
        frontend_build = build.index("- name: Build frontend")
        publish = build.index("- name: Publish")

        self.assertLess(receipt_upload, frontend_build)
        self.assertLess(frontend_build, publish)


if __name__ == "__main__":
    unittest.main()
