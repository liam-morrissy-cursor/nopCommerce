---
name: write-pull-request
description: >-
  Opens or updates a GitHub pull request using the repo template, then runs
  humanizer on the body. Use when creating a PR, updating a PR description,
  writing a merge request, or when the change is test coverage.
---

# Write pull request

Fill `.github/PULL_REQUEST_TEMPLATE.md`, then apply `.cursor/skills/humanizer/SKILL.md` to the body before `gh pr create` or `gh pr edit`. Do not skip humanizer. Return the PR URL when done.

Follow the user's git/PR safety rules (no force push, no `--no-verify`, commit only if asked). Gather status, diff, tracking, and `git log` / `git diff [base]...HEAD` in parallel first.

## Body rules

- Title states the outcome (`Cover order cancel status transitions`, not `Add tests` or `Update stuff`).
- Jira-related PRs: prefix the key in the title (`[NOP-3] …`) and put `Jira: [NOP-3](https://<site>.atlassian.net/browse/NOP-3)` at the top of the body. See `.cursor/rules/jira-pr-links.mdc`. Do not invent a key.
- What changed: concrete behavior, 1–3 bullets. Not a file list.
- Why: the bug, false miss, or decision. Short.
- How to verify: commands a reviewer can run, plus expected signal.
- What did not change: only if a reader would otherwise guess wrong. Otherwise omit the heading.
- No secrets, PAN, tokens, or production-like PII in the body.
- After drafting, run humanizer in embedded mode. Ship the rewritten body only.
- Drop HTML comments (`<!-- … -->`) from the body you submit. Leave unfilled optional sections out rather than shipping placeholders.

## Test-coverage PRs

A PR is test-coverage when tests are the point (new or expanded), not a side effect of a product change.

The reader should know what risk closed without reading the diff. They should not have to wade through a test-writing essay.

Keep **Coverage** to about 20 lines:

1. **Gap** — 1–2 sentences: what was untested, what that allowed.
2. **Cases** — one line each: `behavior / input → expected`. No restated assertions.
3. **Still open** — only real leftovers a reviewer might assume you hit. If none, write `None`.
4. **Run** — exact `dotnet test … --filter …` (and `dotnet test` for the suite if that is the CI path).

Delete Coverage on non-test PRs.

Do not: paste test source, list every file, write a philosophy of testing, or stack hedges.

See [examples.md](examples.md) for a tight vs bloated Coverage section.

## GitHub steps

1. Create/switch branch if needed.
2. Push with `-u` if the branch has no upstream.
3. `gh pr create` (or `gh pr edit`) with the humanized body via HEREDOC, using the template headings.

If both a product change and tests ship together, write a normal PR and add a short Coverage subsection only when the tests close a gap that is not obvious from What changed.
