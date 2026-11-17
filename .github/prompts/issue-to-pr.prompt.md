---
name: issue-to-pr
description: Implement a GitHub issue end to end (branch, C# code, tests, docs) and open a Pull Request without manual steps.
argument-hint: "#<issue number> | <issue number> | <GitHub issue URL>"
agent: agent
---

# /issue-to-pr

Run the autonomous issue-to-PR workflow defined in [skills/issue-to-pr/SKILL.md](../../skills/issue-to-pr/SKILL.md).

1. Read [SKILL.md](../../skills/issue-to-pr/SKILL.md) completely and follow it exactly. It is the only source of the workflow; this file only routes the slash command to it.
2. Also read [rules/dotnet-architecture.md](../../skills/issue-to-pr/rules/dotnet-architecture.md) and [rules/git-workflow.md](../../skills/issue-to-pr/rules/git-workflow.md) before Stage 1.
3. The issue reference is the text typed after `/issue-to-pr` in the chat (for example `#42`, `42` or a full issue URL).
   If nothing was typed, ask once for ${input:issue:issue number or URL} and then continue without further questions.
4. You run in VS Code GitHub Copilot agent mode, so use subagent mode 2 from SKILL.md (*VS Code GitHub Copilot*):
   start each subagent with the subagent tool and put the full agent file, both rule files and the filled-in prompt template into its prompt.
5. Do not ask for confirmation between stages. Stop only in the ABORT state defined in SKILL.md.
