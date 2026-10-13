from pathlib import Path
import unittest


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


class WorkflowRunnerPolicyTests(unittest.TestCase):
    def test_push_trigger_is_main_only(self) -> None:
        push = event_block(WORKFLOW.read_text(encoding="utf-8"), "push")
        self.assertRegex(push, r"(?m)^    branches:\s*\[\s*main\s*\]$")
        self.assertNotIn("**", push)

    def test_pull_requests_never_select_the_persistent_runner(self) -> None:
        source = WORKFLOW.read_text(encoding="utf-8")
        runner = next(
            line.strip()
            for line in source.splitlines()
            if line.lstrip().startswith("runs-on:") and "self-hosted" in line
        )
        self.assertNotIn("github.event_name == 'pull_request'", runner)
        self.assertIn("github.event_name == 'push' && github.ref == 'refs/heads/main'", runner)
        self.assertIn("(github.event_name == 'workflow_dispatch' && github.ref == 'refs/heads/main')", runner)
        self.assertNotIn("startsWith(github.ref, 'refs/heads/')", runner)
        self.assertIn("fromJSON('[\"self-hosted\",\"Linux\",\"X64\",\"host\"]')", runner)
        self.assertIn("|| matrix.os", runner)

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


if __name__ == "__main__":
    unittest.main()
