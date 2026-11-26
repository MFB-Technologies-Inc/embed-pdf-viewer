#!/usr/bin/env python3
"""Fails when a release train job could publish without its one approval.

    check-release-gate.py <release-train.yml>

The train's approve job is the only one with an environment; its reviewers
approve once, after everything is built. This checks that

  - approve exists and names an environment, and no other job, in the train or
    in a workflow it calls, names one;
  - every train job that publishes needs approve, directly or through other
    jobs. A job publishes when one of its lines pushes or tags (git push,
    git tag), makes or changes a release (gh release create, upload, edit or
    delete; github-release.sh publish; publish-engine-payload.mjs --stage
    publish; a release action such as softprops/action-gh-release) or deploys
    a site (wrangler deploy, wrangler pages deploy, cloudflare/wrangler-action)
    or pushes a NuGet package (dotnet nuget push; release-viewer.sh without
    --dry-run on the same line), when its token can write contents or
    packages, or when it calls a local workflow with
    `stage: publish` or one with a publishing job that doesn't wait for that
    stage;
  - in each called workflow, a job that publishes by the same test runs only
    when its `if:` asks for inputs.stage == 'publish'. A called job's token
    can write contents when its own permissions say so or, with no
    permissions of its own or at its workflow's top, when the caller job
    grants it;
  - neither a publishing job nor any job between it and approve has an `if:`
    that runs it after approve failed or was rejected (always(), cancelled(),
    !cancelled(), failure()) unless that `if:` also requires
    needs.approve.result == 'success'.

Runners don't promise PyYAML, so this reads the workflows line by line. It
understands the subset these files use (two-space indents, job ids at indent
2, job keys at indent 4, `needs` as a name, a [flow, list] or a block list,
permissions as a word, a {flow: map} or a block map) and stops on anything else
it finds where it looks, rather than guess. Comment lines are ignored, so a
comment that mentions git push is not a push.
"""

import re
import sys
from pathlib import Path

PUBLISHES = re.compile(
    r"\bgit push\b|\bgit tag\b|\bgh release (create|upload|edit|delete)\b|\brelease create\b"
    r"|github-release\.sh publish|--stage publish\b"
    r"|\bdotnet nuget push\b|\brelease-viewer\.sh\b(?!.*--dry-run)"
    r"|\bwrangler\S*\s+(pages\s+)?deploy\b"
    r"|uses:\s*(softprops/action-gh-release|ncipollo/release-action|actions/create-release"
    r"|actions/upload-release-asset|svenstaro/upload-release-action|cloudflare/wrangler-action)\b"
)
STATUS = re.compile(r"\balways\(\)|\bcancelled\(\)|\bfailure\(\)")
APPROVED = "needs.approve.result == 'success'"
STAGED = "inputs.stage == 'publish'"
JOB = re.compile(r"^  ([A-Za-z0-9_-]+):\s*(#.*)?$")
KEY = re.compile(r"^    ([A-Za-z0-9_-]+):\s*(.*?)\s*$")
UNSET = object()


class GateError(Exception):
    pass


def strip_comment(value):
    return re.sub(r"\s+#.*$", "", value).strip()


def inline_permissions(value, where):
    """A permissions value written on its key's line."""
    if value in ("read-all", "write-all"):
        return value
    if value.startswith("{") and value.endswith("}"):
        perms = {}
        for entry in filter(None, (e.strip() for e in value[1:-1].split(","))):
            key, sep, level = entry.partition(":")
            if not sep:
                raise GateError(f"{where}: can't read permissions {value!r}")
            perms[key.strip()] = level.strip()
        return perms
    raise GateError(f"{where}: can't read permissions {value!r}")


def writes_contents(perms):
    """Whether a token can write contents or packages: either can publish."""
    return perms == "write-all" or (
        isinstance(perms, dict) and "write" in (perms.get("contents"), perms.get("packages")))


def parse_workflow(path):
    """(top-level permissions or UNSET, {job id: job})"""
    lines = Path(path).read_text().splitlines()
    try:
        start = lines.index("jobs:")
    except ValueError:
        raise GateError(f"{path}: no top-level jobs:")

    top = UNSET
    for number, line in enumerate(lines[:start], 1):
        if line.startswith("permissions:"):
            value = strip_comment(line[len("permissions:"):])
            if value:
                top = inline_permissions(value, f"{path}:{number}")
            else:
                top = {}
                for entry in lines[number:start]:
                    if entry.strip() and not entry.startswith(" "):
                        break
                    item = re.match(r"^  ([a-z-]+):\s*(\S+)", entry)
                    if item:
                        top[item.group(1)] = strip_comment(item.group(2))

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
                {"needs": [], "if": "", "uses": "", "with": {}, "environment": False,
                 "permissions": UNSET, "lines": []},
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
            elif key == "permissions":
                job["permissions"] = {} if block else inline_permissions(value, f"{path}:{number}")
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
        elif block == "permissions":
            item = re.match(r"^      ([a-z-]+):\s*(\S+)", line)
            if not item:
                raise GateError(f"{path}:{number}: can't read this permission: {line!r}")
            job["permissions"][item.group(1)] = strip_comment(item.group(2))
        elif block == "if" and line.startswith("      "):
            job["if"] += " " + line.strip()
    return top, jobs


def publishing_lines(job):
    return [l.strip() for l in job["lines"] if PUBLISHES.search(l) and not l.lstrip().startswith("#")]


def check(train):
    train = Path(train)
    root = train.resolve().parents[2]
    errors = []
    top, jobs = parse_workflow(train)
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

    def own(job, fallback):
        # A token with no permissions anywhere gets the repository default,
        # which can write; count it as writing.
        perms = job["permissions"] if job["permissions"] is not UNSET else fallback
        return "write-all" if perms is UNSET else perms

    # A call publishes on its own when the workflow it calls has a publishing
    # job that doesn't wait for stage publish.
    ungated_calls = set()
    seen = set()
    for name, job in jobs.items():
        uses = job["uses"]
        if not uses.startswith("./"):
            continue
        grant = own(job, top)
        called_top, called = parse_workflow(root / uses[2:])
        for inner, inner_job in called.items():
            if inner_job["environment"] and uses not in seen:
                errors.append(f"{uses} job {inner} names an environment; only the train's approve may")
            fallback = called_top if called_top is not UNSET else grant
            publishes = publishing_lines(inner_job) or writes_contents(own(inner_job, fallback))
            if publishes and STAGED not in inner_job["if"]:
                ungated_calls.add(name)
                errors.append(
                    f"{name} calls {uses}, whose job {inner} publishes or can write contents"
                    f" but whose if doesn't ask for {STAGED}"
                )
        seen.add(uses)

    def publishes(name, job):
        if job["uses"].startswith("./"):
            return job["with"].get("stage") == "publish" or name in ungated_calls
        return bool(publishing_lines(job)) or writes_contents(own(job, top))

    def ancestors(name, seen=None):
        seen = set() if seen is None else seen
        for need in jobs.get(name, {}).get("needs", []):
            if need not in seen:
                seen.add(need)
                ancestors(need, seen)
        return seen

    publishers = [name for name, job in jobs.items() if publishes(name, job)]
    if not publishers:
        errors.append("no job publishes; the patterns no longer match the train")
    for name in publishers:
        above = ancestors(name)
        if "approve" not in above:
            errors.append(f"{name} publishes but doesn't need approve, directly or through another job")
            continue
        # Every job from approve down to this one: a status function in any
        # of their ifs can run it after approve failed or was rejected.
        path = [name] + sorted(a for a in above if "approve" in ancestors(a))
        for step in path:
            condition = jobs[step]["if"]
            if STATUS.search(condition) and APPROVED not in condition:
                errors.append(
                    f"{step} has if: {condition}, which can run {name} after approve fails;"
                    f" add {APPROVED} to it"
                )
    return publishers, sorted(set(errors), key=errors.index)


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
