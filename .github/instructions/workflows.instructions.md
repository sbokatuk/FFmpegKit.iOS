---
applyTo: ".github/workflows/*.yml"
---

# Workflows

- Every build, pack and test job runs on **macOS** (`macos-15`) — iOS has no cross-platform native toolchain, unlike the Android bindings. Only version resolution, the guard and publishing run on ubuntu.
- `build.yml` is the reusable pipeline. Its `verify` input means "run package validation, the sample builds and the e2e suites": pull requests pass `true`, releases pass `false` because the tagged commit was already verified on its pull request. Keep the name and the meaning identical across the sibling repositories.
- That trade is only sound because `release.yml`'s `guard` job proves the tagged commit is an ancestor of the default branch. Do not remove it, weaken it, or add a publish path that skips it.
- Publishing uses **trusted publishing**: `NuGet/login@v1` with `user: ${{ secrets.NUGET_USER }}`, `environment: nuget.org` (must match the nuget.org policy) and `permissions: id-token: write`. There is no API key secret — never add one. Keep the login step immediately before `dotnet nuget push`: the issued key lasts an hour and each OIDC token can be exchanged exactly once.
- Each SDK band is installed and invoked from a scratch directory carrying its own `global.json`, because the SDK is resolved from the working directory and the repository's `global.json` pins .NET 9. Preserve that pattern when adding jobs.
- The xcframework cache key includes the native version — a build for an older line must not restore the previous version's frameworks.
- `auto-release.yml` tags only release notes that were **added** (`--diff-filter=A`) and starts `release.yml` with `gh workflow run`, because a tag pushed with `GITHUB_TOKEN` does not trigger `on: push: tags`. Keep both triggers on `release.yml`.
- Forked pull requests get no OIDC token, so the beta publish job is gated on `github.event.pull_request.head.repo.full_name == github.repository`; they must still build and test.
