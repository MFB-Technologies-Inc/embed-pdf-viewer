#!/usr/bin/env python3
"""Fails when a release train job could publish before its one approval.

    check-release-gate.py <release-train.yml>

The train's approve job is the only one with an environment; its reviewers
approve once, after everything is built. This checks that

  - approve exists and names an environment, and no other job, in the train or
    in a workflow it calls, names one;
  - every train job that publishes needs approve, directly or through other
    jobs. A job publishes when its own lines push (git push), tag and release
    (gh release create, github-release.sh publish), or when it calls a local
    workflow with `stage: publish` or one whose publishing job doesn't wait
    for that stage;
  - in each called workflow, a job that publishes (the same commands, or
    publish-engine-payload.mjs --stage publish) runs only when its `if:` asks
    for inputs.stage == 'publish'.

Runners don't promise PyYAML, so this reads the workflows line by line. It
understands the subset these files use (two-space indents, job ids at indent
2, job keys at indent 4, `needs` as a name, a [flow, list] or a block list)
and stops on anything else it finds where it looks, rather than guess.
Comment lines are ignored, so a comment that mentions git push is not a push.
"""

import re
import sys
from pathlib import Path

PUBLISHES = re.compile(r"\bgit push\b|\brelease create\b|github-release\.sh publish|--stage publish\b")
JOB = re.compile(r"^  ([A-Za-z0-9_-]+):\s*(#.*)?$")
KEY = re.compile(r"^    ([A-Za-z0-9_-]+):\s*(.*?)\s*$")


class GateError(Exception):
    pass


def strip_comment(value):
    return re.sub(r"\s+#.*$", "", value).strip()


def parse_jobs(path):
    """{job id: {"needs": [...], "if": str, "uses": str, "with": {}, "environment": bool, "lines": [...]}}"""
    lines = Path(path).read_text().splitlines()
    try:
        start = lines.index("jobs:")
    except ValueError:
        raise GateError(f"{path}: no top-level jobs:")
    jobs = {}
    job = None
    block = None  # the job key whose indented lines follow
    for number, line in enumerate(lines[start + 1 :], start + 2):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if not line.startswith(" "):
            break  # the next top-level key
        match = JOB.match(line)
        if match:
            job = jobs.setdefault(
                match.group(1),
                {"needs": [], "if": "", "uses": "", "with": {}, "environment": False, "lines": []},
            )
            block = None
            continue
        if job is None:
            raise GateError(f"{path}:{number}: expected a job id at indent 2: {line!r}")
        job["lines"].append(line)
        match = KEY.match(line)
        if match:
            key, value = match.group(1), strip_comment(match.group(2))
            # Only a key with nothing after it, or a folded or literal
            # scalar, owns the lines below it.
            block = key if value in ("", ">", ">-", "|", "|-") else None
            if key == "needs":
                if value.startswith("["):
                    if not value.endswith("]"):
                        raise GateError(f"{path}:{number}: needs list must close on its line")
                    job["needs"] = [n.strip() for n in value[1:-1].split(",") if n.strip()]
                elif value:
                    job["needs"] = [value]
            elif key == "if":
                job["if"] = "" if block else value
            elif key == "uses":
                job["uses"] = value
            elif key == "environment":
                job["environment"] = True
            continue
        if block == "needs":
            item = re.match(r"^      - ([A-Za-z0-9_-]+)$", re.sub(r"\s+#.*$", "", line).rstrip())
            if not item:
                raise GateError(f"{path}:{number}: can't read this needs entry: {line!r}")
            job["needs"].append(item.group(1))
        elif block == "with":
            item = re.match(r"^      ([A-Za-z0-9_-]+):\s*(.*)$", line)
            if item:
                job["with"][item.group(1)] = strip_comment(item.group(2)).strip("'\"")
        elif block == "if" and line.startswith("      "):
            job["if"] += " " + line.strip()
    return jobs


def publishing_lines(job):
    return [l.strip() for l in job["lines"] if PUBLISHES.search(l) and not l.lstrip().startswith("#")]


def check(train):
    train = Path(train)
    root = train.resolve().parents[2]
    errors = []
    jobs = parse_jobs(train)
    if "approve" not in jobs:
        raise GateError(f"{train}: no approve job")
    if not jobs["approve"]["environment"]:
        errors.append("approve names no environment")
    for name, job in jobs.items():
        for need in job["needs"]:
            if need not in jobs:
                errors.append(f"{name} needs {need}, which isn't a job")
        if name != "approve" and job["environment"]:
            errors.append(f"{name} names an environment; only approve may")

    called = {}
    for name, job in jobs.items():
        uses = job["uses"]
        if not uses.startswith("./") or uses in called:
            continue
        workflow = parse_jobs(root / uses[2:])
        ungated = []
        for inner, inner_job in workflow.items():
            if inner_job["environment"]:
                errors.append(f"{uses} job {inner} names an environment; only the train's approve may")
            if publishing_lines(inner_job) and "inputs.stage == 'publish'" not in inner_job["if"]:
                ungated.append(inner)
                errors.append(f"{uses} job {inner} publishes but its if doesn't ask for inputs.stage == 'publish'")
        called[uses] = ungated

    def publishes(job):
        if publishing_lines(job):
            return True
        if job["uses"].startswith("./"):
            return job["with"].get("stage") == "publish" or bool(called.get(job["uses"]))
        return False

    def ancestors(name, seen=None):
        seen = set() if seen is None else seen
        for need in jobs.get(name, {}).get("needs", []):
            if need not in seen:
                seen.add(need)
                ancestors(need, seen)
        return seen

    publishers = [name for name, job in jobs.items() if publishes(job)]
    if not publishers:
        errors.append("no job publishes; the patterns no longer match the train")
    for name in publishers:
        if "approve" not in ancestors(name):
            errors.append(f"{name} publishes but doesn't need approve, directly or through another job")
    return publishers, errors


def main(argv):
    if len(argv) != 2:
        print("usage: check-release-gate.py <release-train.yml>", file=sys.stderr)
        return 64
    try:
        publishers, errors = check(argv[1])
    except (GateError, OSError) as error:
        print(f"::error::{error}")
        return 1
    for error in errors:
        print(f"::error::{error}")
    if errors:
        return 1
    print(f"release gate: {', '.join(sorted(publishers))} all wait for approve")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
