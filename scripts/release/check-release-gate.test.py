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
        shutil.copy(REPO / ".github" / "workflows" / "android-release.yml", workflows)
        self.train = workflows / "release-train.yml"

    def edit(self, name, old, new):
        path = self.dir / ".github" / "workflows" / name
        text = path.read_text()
        self.assertEqual(text.count(old), 1, f"{old!r} in {name}")
        path.write_text(text.replace(old, new))

    def run_gate(self, train=None):
        return subprocess.run(
            [sys.executable, str(GATE), str(train or self.train)], capture_output=True, text=True
        )

    def assert_fails(self, message, train=None):
        result = self.run_gate(train)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn(message, result.stdout)

    def test_the_train_passes(self):
        result = self.run_gate()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        for job in ("runtime-publish", "payload-publish", "apple-publish", "pin",
                    "swift-release", "viewer-release", "dotnet-release", "dotnet-viewer-release",
                    "deploy-demo", "android-publish"):
            self.assertIn(job, result.stdout)

    def test_a_release_job_without_approve_fails(self):
        self.edit("release-train.yml", "needs: [approve, preflight, dotnet]", "needs: [preflight, dotnet]")
        self.assert_fails("dotnet-release publishes but doesn't need approve")

    def test_a_nuget_release_without_approve_fails(self):
        self.edit("release-train.yml", "needs: [approve, dotnet, preflight]", "needs: [dotnet, preflight]")
        self.assert_fails("dotnet-viewer-release publishes but doesn't need approve")

    def test_a_nuget_push_publishes(self):
        self.edit("release-train.yml", "needs: [approve, viewer]\n    runs-on: ubuntu-26.04\n    timeout-minutes: 10\n    permissions:\n      contents: write",
                  "needs: [viewer]\n    runs-on: ubuntu-26.04\n    timeout-minutes: 10\n    permissions:\n      contents: read")
        self.edit("release-train.yml", "bash scripts/release/github-release.sh publish \"rust-viewer-$VERSION\"",
                  "dotnet nuget push x.nupkg; echo \"rust-viewer-$VERSION\"")
        self.assert_fails("viewer-release publishes but doesn't need approve")

    def test_a_dry_run_preview_does_not_publish_but_a_real_one_would(self):
        self.assertEqual(self.run_gate().returncode, 0)
        self.edit("release-dotnet.yml", "release-viewer.sh dotnet-viewer \"$VERSION\" \"$GITHUB_SHA\" --dry-run",
                  "release-viewer.sh dotnet-viewer \"$VERSION\" \"$GITHUB_SHA\"")
        self.assert_fails("whose job viewer-release-preview publishes")

    def test_packages_write_in_a_called_job_counts_as_writing(self):
        self.edit("release-dotnet.yml", "    permissions:\n      contents: read\n      packages: read\n",
                  "    permissions:\n      contents: read\n      packages: write\n")
        self.assert_fails("whose job viewer-release-preview publishes or can write")

    def test_a_called_job_asking_for_more_than_its_caller_grants_fails(self):
        self.edit("release-train.yml", "    permissions:\n      contents: read\n      packages: read\n    with:\n      dry_run",
                  "    with:\n      dry_run")
        self.assert_fails("dotnet calls ./.github/workflows/release-dotnet.yml, whose job viewer-release-preview"
                          " asks for packages: read, which dotnet doesn't grant")

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
        self.assert_fails(
            "runtime calls ./.github/workflows/release-runtime.yml, whose job publish publishes"
            " or can write contents or packages but whose if doesn't ask for inputs.stage == 'publish'"
        )

    def test_an_android_publish_without_approve_fails(self):
        self.edit("release-train.yml", "needs: [approve, android]\n    uses: ./.github/workflows/release-android.yml",
                  "needs: [android]\n    uses: ./.github/workflows/release-android.yml")
        self.assert_fails("android-publish publishes but doesn't need approve")

    def test_an_ungated_packages_job_fails(self):
        # packages writes packages and runs publish-staged.sh.
        self.edit("release-android.yml",
                  "if: inputs.stage == 'publish' && inputs.version != '' && inputs.packages_repository != ''",
                  "if: inputs.version != '' && inputs.packages_repository != ''")
        self.assert_fails(
            "android calls ./.github/workflows/release-android.yml, whose job packages publishes"
            " or can write contents or packages"
        )

    def test_packages_write_counts_as_writing(self):
        self.add_job(
            "  registry:\n"
            "    needs: preflight\n"
            "    runs-on: ubuntu-24.04\n"
            "    permissions:\n"
            "      contents: read\n"
            "      packages: write\n"
            "    steps:\n"
            "      - run: echo nothing that looks like a release\n"
        )
        self.assert_fails("registry publishes but doesn't need approve")

    def test_publish_staged_counts_as_publishing(self):
        self.add_job(
            "  registry:\n"
            "    needs: preflight\n"
            "    runs-on: ubuntu-24.04\n"
            "    permissions: {contents: read}\n"
            "    steps:\n"
            "      - run: bash platforms/android/publish-staged.sh staged owner/repo\n"
        )
        self.assert_fails("registry publishes but doesn't need approve")

    def test_the_android_wrapper_passes(self):
        wrapper = self.dir / ".github" / "workflows" / "android-release.yml"
        result = self.run_gate(wrapper)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("android-publish", result.stdout)

    def test_the_android_wrapper_without_approve_fails(self):
        self.edit("android-release.yml", "needs: [approve, android]", "needs: [android]")
        self.assert_fails("android-publish publishes but doesn't need approve",
                          self.dir / ".github" / "workflows" / "android-release.yml")

    def add_job(self, text):
        path = self.train
        path.write_text(path.read_text().rstrip("\n") + "\n\n" + text)

    def test_a_release_upload_without_approve_fails(self):
        self.add_job(
            "  upload:\n"
            "    needs: preflight\n"
            "    runs-on: ubuntu-24.04\n"
            "    steps:\n"
            "      - run: gh release upload v1 notes.md\n"
        )
        self.assert_fails("upload publishes but doesn't need approve")

    def test_a_demo_deploy_without_approve_fails(self):
        self.edit("release-train.yml", "name: deploy demo\n    needs: [approve, viewer]",
                  "name: deploy demo\n    needs: viewer")
        self.assert_fails("deploy-demo publishes but doesn't need approve")

    def test_a_wrangler_pages_deploy_without_approve_fails(self):
        self.add_job(
            "  pages:\n"
            "    needs: preflight\n"
            "    runs-on: ubuntu-24.04\n"
            "    steps:\n"
            "      - run: npx wrangler@4 pages deploy dist\n"
        )
        self.assert_fails("pages publishes but doesn't need approve")

    def test_a_contents_write_job_without_approve_fails(self):
        self.add_job(
            "  writer:\n"
            "    needs: preflight\n"
            "    runs-on: ubuntu-24.04\n"
            "    permissions:\n"
            "      contents: write\n"
            "    steps:\n"
            "      - run: echo nothing that looks like a release\n"
        )
        self.assert_fails("writer publishes but doesn't need approve")

    def test_a_contents_write_job_after_approve_passes(self):
        self.add_job(
            "  writer:\n"
            "    needs: approve\n"
            "    runs-on: ubuntu-24.04\n"
            "    permissions:\n"
            "      contents: write\n"
            "    steps:\n"
            "      - run: echo after approval\n"
        )
        self.assertEqual(self.run_gate().returncode, 0)

    def test_always_on_a_release_job_fails(self):
        self.edit("release-train.yml", "    name: release viewer\n",
                  "    name: release viewer\n    if: always()\n")
        self.assert_fails("viewer-release has if: always(), which can run viewer-release after approve fails")

    def test_not_cancelled_between_approve_and_a_release_job_fails(self):
        # apple-publish sits between approve and pin.
        self.edit("release-train.yml", "    name: release-apple.yml (publish)\n",
                  "    name: release-apple.yml (publish)\n    if: ${{ !cancelled() }}\n")
        self.assert_fails("apple-publish has if: ${{ !cancelled() }}, which can run pin after approve fails")

    def test_always_that_requires_approve_passes(self):
        self.edit("release-train.yml", "    name: release viewer\n",
                  "    name: release viewer\n    if: always() && needs.approve.result == 'success'\n")
        self.assertEqual(self.run_gate().returncode, 0, self.run_gate().stdout)


if __name__ == "__main__":
    unittest.main()
