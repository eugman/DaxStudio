# GitHub Actions OAuth Token Notes

Notes on using Claude Max subscription with GitHub Actions for spec validation.

---

## Current Setup

- **Secret configured:** `CLAUDE_CODE_OAUTH_TOKEN`
- **Workflow file:** `.github/workflows/validate-reimplementation-spec.yml`
- **Action used:** `anthropics/claude-code-action@v1`

## The Problem

OAuth tokens from Claude Max subscription expire in ~1 day. The official action doesn't auto-refresh.

### Known Bug

[Issue #11016](https://github.com/anthropics/claude-code/issues/11016): `/install-github-app` generates workflows without auto-refresh, even when selecting "long-lived token" option.

## Options

### Option 1: Manual Token Refresh (Current)

- Works immediately with existing `CLAUDE_CODE_OAUTH_TOKEN`
- Breaks after ~1 day when token expires
- To refresh:
  1. Run `claude /logout` then `claude` to re-authenticate
  2. Copy token from `~/.claude/.credentials.json`
  3. Update `CLAUDE_CODE_OAUTH_TOKEN` secret

### Option 2: Community Fork with Auto-Refresh

Uses [grll/claude-code-action](https://github.com/marketplace/actions/claude-code-action-with-oauth) which auto-refreshes tokens.

**Required secrets (4):**

| Secret | Source | Purpose |
|--------|--------|---------|
| `CLAUDE_ACCESS_TOKEN` | `~/.claude/.credentials.json` | Current OAuth token |
| `CLAUDE_REFRESH_TOKEN` | `~/.claude/.credentials.json` | Used to get new access tokens |
| `CLAUDE_EXPIRES_AT` | `~/.claude/.credentials.json` | Token expiration timestamp |
| `SECRETS_ADMIN_PAT` | GitHub Settings > Developer settings > PAT | Allows action to update secrets |

**PAT requirements:**
- Fine-grained token
- Repository access: Select the DaxStudio repo
- Permissions: `secrets:write`

### Option 3: API Key (Costs Money)

- Use `ANTHROPIC_API_KEY` instead of OAuth
- Reliable, no expiration issues
- Incurs API costs separate from Max subscription

## References

- [Official Action](https://github.com/anthropics/claude-code-action)
- [Community Fork with OAuth](https://github.com/marketplace/actions/claude-code-action-with-oauth)
- [Issue #727: Refresh token support request](https://github.com/anthropics/claude-code-action/issues/727)
- [Issue #11016: OAuth tokens expire without auto-refresh](https://github.com/anthropics/claude-code/issues/11016)

---

*Last updated: 2025-12-27*
