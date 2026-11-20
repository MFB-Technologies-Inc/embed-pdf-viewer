#!/usr/bin/env python3
"""Tests for check-release-gate.py, on copies of this repo's release workflows.

    python3 scripts/release/check-release-gate.test.py
"""

import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
GATE = HERE / "check-release-gate.py"


class ReleaseGate(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.dir)
        workflows = self.dir / ".github" / "workflows"
        workflows.mkdir(parents=True)
        for path in (REPO / ".github" / "workflows").glob("release-*.yml"):
            shutil.copy(path, workflows)
        self.train = workflows / "release-train.yml"

    def edit(self, name, old, new):
        path = self.dir / ".github" / "workflows" / name
        text = path.read_text()
        self.assertEqual(text.count(old), 1, f"{old!r} in {name}")
        path.write_text(text.replace(old, new))

    def run_gate(self):
        return subprocess.run(
            [sys.executable, str(GATE), str(self.train)], capture_output=True, text=True
        )

    def assert_fails(self, message):
        result = self.run_gate()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn(message, result.stdout)

    def test_the_train_passes(self):
        result = self.run_gate()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        for job in ("runtime-publish", "payload-publish", "apple-publish", "pin",
                    "swift-release", "viewer-release", "dotnet-release"):
            self.assertIn(job, result.stdout)

    def test_a_release_job_without_approve_fails(self):
        self.edit("release-train.yml", "needs: [approve, preflight, dotnet]", "needs: [preflight, dotnet]")
        self.assert_fails("dotnet-release publishes but doesn't need approve")

    def test_a_publish_call_without_approve_fails(self):
        self.edit("release-train.yml", "needs: [approve, slices-runtime, runtime]", "needs: [slices-runtime, runtime]")
        self.assert_fails("runtime-publish publishes but doesn't need approve")

    def test_approve_through_another_job_passes(self):
        # pin also needs the three publish calls, which need approve.
        self.edit("release-train.yml",
                  "needs: [approve, apple, apple-publish, runtime-publish, payload-publish]",
                  "needs: [apple, apple-publish, runtime-publish, payload-publish]")
        self.assertEqual(self.run_gate().returncode, 0)

    def test_a_second_environment_fails(self):
        self.edit("release-train.yml", "    name: release viewer\n",
                  "    name: release viewer\n    environment: release\n")
        self.assert_fails("viewer-release names an environment")

    def test_an_ungated_publish_job_in_a_called_workflow_fails(self):
        self.edit("release-runtime.yml",
                  "if: inputs.stage == 'publish' && inputs.published == 'false'",
                  "if: inputs.published == 'false'")
        self.assert_fails("release-runtime.yml job publish publishes but its if doesn't ask for inputs.stage == 'publish'")


if __name__ == "__main__":
    unittest.main()
