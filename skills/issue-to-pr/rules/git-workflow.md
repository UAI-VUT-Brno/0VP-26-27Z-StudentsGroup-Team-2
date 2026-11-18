# Git & hygiene rules for /issue-to-pr

The team process is: branch → commits → Pull Request to `main` → code review by a teammate → merge → delete branch.
Nobody pushes to `main` directly, and the workflow never merges its own PR.

## 1. Branch

Name: `feat/t2-<N>-<short-slug>`, where `<N>` is the GitHub issue number.

Build `<short-slug>` from the issue title:

1. Drop the task prefix such as `0VP-T2-20:`.
2. Lowercase, transliterate diacritics to ASCII (`ú` → `u`), replace everything except `a-z0-9` with `-`.
3. Collapse repeated `-` and trim `-` at both ends.
4. Keep at most 5 words. If the result is longer than 40 characters, cut it at the last `-` before character 40.

Example: issue #45 `0VP-T2-20: Refactor McpTestFixture to avoid duplicate host creation`
→ `feat/t2-45-refactor-mcptestfixture-to-avoid`.

Always branch from an up-to-date `main` (`git pull --ff-only origin main`).
If the branch already exists locally or on `origin`, stop. Never reuse, reset or force-push it.

## 2. Commits

Use [Conventional Commits](https://www.conventionalcommits.org/) in the form already used in this repository:

```text
<type>(<task-id>): <imperative summary in English> (#<N>)
```

- `<type>`: `feat` (production code), `fix` (bug fix, including self-healing repairs), `test` (tests only), `docs` (README, `.http`, XML docs only), `refactor`, `chore`.
- `<task-id>`: the `0VP-T2-xx` code from the issue title. If the title has none, use `#<N>` and drop the trailing `(#<N>)`.
- Summary: imperative mood, lowercase start, no trailing period, at most 72 characters for the whole line.
- One logical change per commit, for example one commit per tier in Stage 3.

Example: `test(0VP-T2-20): use a single Kestrel host in McpTestFixture (#45)`.

Staging:

- Stage files by explicit path: `git add <path> ...`. Never use `git add -A`, `git add .` or `git commit -a`.
- Never commit build output or local state: `bin/`, `obj/`, `.vs/`, `.DS_Store`, `logs/`, `todos.json`, `.env`.
- Never change `WORKLOG.md` (maintained by the team leader), `.github/workflows/`, `skills/` or `.agents/`.

## 3. Formatting (`.editorconfig`)

- `.cs` files: UTF-8 **with BOM**. A changed `.cs` file that has no BOM yet gets one.
- 4-space indentation (2 for `.json`, `.yml`, `.yaml`, `.csproj`), trimmed trailing whitespace, final newline.
- Keep the line endings a file already has. A whole-file line-ending change hides the real diff from reviewers.
- `.md` files: UTF-8 without BOM, LF.

## 4. Pre-push gates

Run all of them on the branch before pushing. Every one must pass.

On Windows, the helper script [`skills/issue-to-pr/scripts/verify-and-test.ps1`](../scripts/verify-and-test.ps1) runs the BOM check/fix, `git diff --check`, and `dotnet test UAI.ZeroVP.sln --configuration Release` in one command (pass `-CheckOnly -SkipTests` for read-only pre-push gate verification).

**Whitespace errors:**

```bash
git diff --check main...HEAD
```

**UTF-8 BOM on changed C# files** (bash; prints nothing when everything is fine):

```bash
git diff --name-only --diff-filter=AM main...HEAD -- '*.cs' | while read -r f; do
  [ "$(head -c 3 "$f" | od -An -tx1 | tr -d ' \n')" = "efbbbf" ] || echo "missing BOM: $f"
done
```

The same check in PowerShell:

```powershell
git diff --name-only --diff-filter=AM main...HEAD -- '*.cs' | ForEach-Object {
  $b = [System.IO.File]::ReadAllBytes((Resolve-Path $_))
  if ($b.Length -lt 3 -or $b[0] -ne 0xEF -or $b[1] -ne 0xBB -or $b[2] -ne 0xBF) { "missing BOM: $_" }
}
```

To add a missing BOM in bash: `printf '\xef\xbb\xbf' | cat - "$f" > "$f.tmp" && mv "$f.tmp" "$f"`.

**Build and tests** (same configuration as CI):

```bash
dotnet test UAI.ZeroVP.sln --configuration Release
```

**Security gate** – vulnerable NuGet packages, the same check as `.github/workflows/ci.yml`.
The `grep` matches both the English and the Czech output of the .NET SDK:

```bash
output=$(dotnet list UAI.ZeroVP.sln package --vulnerable --include-transitive)
echo "$output"
if echo "$output" | grep -iqE "(has the following vulnerable packages|má při aktuálních zdrojích následující zranitelné balíčky)"; then
  echo "Vulnerable NuGet packages detected"; exit 1
fi
```

A vulnerable package is never fixed by the workflow on its own. Stop and report it, because upgrading packages is outside the scope of an issue.

## 5. Push and Pull Request

```bash
git push -u origin feat/t2-<N>-<short-slug>
gh pr create --base main --head feat/t2-<N>-<short-slug> --title "<title>" --body-file <temp file>
```

- PR title: the issue title as is, for example `0VP-T2-20: Refactor McpTestFixture to avoid duplicate host creation`.
- PR body: generated from `prompts/generate-pr.md`. It must contain `Closes #<N>`.
- Write the body to a file in the OS temp directory, not into the repository.
- CI (`.github/workflows/ci.yml`) runs on every PR to `main`. Restore, security scan, Release build and tests must be green before review.
- Never use `git push --force`, never push to `main` and never merge the PR.
