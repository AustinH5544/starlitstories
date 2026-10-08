"""PreToolUse hook: stop Claude from changing `main` (pushes to main deploy to production).

Blocks, for Bash/PowerShell commands:
  - any write-type git command while the checked-out branch is main
  - any git command that switches to main and then writes
  - any push that targets main, deletes main, or force-pushes
  - merging a pull request with the GitHub CLI (`gh pr merge`, including --auto, or merge calls via `gh api`)
Everything else (including all work on staging, and opening/editing/commenting on PRs) is allowed.
The user reviews and merges PRs into main themselves.
"""
import json
import re
import subprocess
import sys

PROTECTED = "main"
# Trailing lookahead so read-only look-alikes (merge-base, commit-graph) don't match.
WRITE_OPS = r"(commit|merge|rebase|reset|cherry-pick|revert|am|pull|push)(?![\w-])"


def deny(reason: str) -> None:
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "deny",
            "permissionDecisionReason": reason,
        }
    }))
    sys.exit(0)


def current_branch(cwd: str | None) -> str | None:
    try:
        out = subprocess.run(
            ["git", "rev-parse", "--abbrev-ref", "HEAD"],
            cwd=cwd or None, capture_output=True, text=True, timeout=10,
        )
        return out.stdout.strip() if out.returncode == 0 else None
    except Exception:
        return None


def main() -> None:
    data = json.load(sys.stdin)
    cmd = (data.get("tool_input") or {}).get("command") or ""

    # Pull requests: Claude may open them; only the user merges them.
    # Match `gh` only where a command starts (not inside a quoted commit message or PR body).
    gh_cmd = r"(?:^|[;&|(\n]|\$\()\s*(?:\w+=\S*\s+)*gh\s+"
    if re.search(gh_cmd + r"pr\s+merge\b", cmd):
        deny("Merging pull requests is reserved for the user. Open the PR and ask them to review and merge it.")
    for api_call in re.findall(gh_cmd + r"api\b[^;&|\n]*", cmd):
        if re.search(r"/(pulls/\d+/merge|merges)\b", api_call):
            deny("Merging through the GitHub API is reserved for the user.")

    if not re.search(r"\bgit\b", cmd):
        return

    main_ref = rf"(?<![\w/.-]){PROTECTED}(?![\w/.-])"

    # Force pushes of any branch
    for push in re.findall(r"\bgit\s+push\b[^;&|\n]*", cmd):
        if re.search(r"(--force\b|--force-with-lease\b|\s-f\b|\s\+\S)", push):
            deny("Force-pushing is not allowed for Claude in this repo.")
        # Pushing to, or deleting, main: `git push origin main`, `HEAD:main`, `--delete main`, `:main`
        if re.search(main_ref, push) or re.search(rf":{PROTECTED}\b", push):
            deny(f"Pushing to or deleting '{PROTECTED}' is reserved for the user (it deploys to production).")

    # Switching to main and then writing in the same command
    if re.search(rf"\bgit\s+(checkout|switch)\s+(-\S+\s+)*{PROTECTED}\b", cmd) and \
            re.search(rf"\bgit\s+{WRITE_OPS}\b", cmd):
        deny(f"Changing '{PROTECTED}' is reserved for the user. Work on 'staging' instead.")

    # Deleting or renaming the main branch locally
    if re.search(rf"\bgit\s+branch\s+.*-[dDmM]\b.*{main_ref}", cmd):
        deny(f"Deleting or renaming '{PROTECTED}' is not allowed.")

    # Write operations while main is checked out
    if re.search(rf"\bgit\s+{WRITE_OPS}\b", cmd) and current_branch(data.get("cwd")) == PROTECTED:
        deny(f"'{PROTECTED}' is checked out. Claude may not commit, merge, rebase, reset, pull or push "
             f"on '{PROTECTED}'. Switch to 'staging' first.")


if __name__ == "__main__":
    main()
