# Releases

Maintainer workflows for packaging and publishing Ciallo. For development setup and PRs,
see the [Contributing Guide](../.github/CONTRIBUTING.md).

## Local exports

Run from the repository root to prepare export dependencies for the current platform:

```sh
./engine.sh sync --templates
./engine.sh ffmpeg
```

Then open the project editor and use **Project > Export** with the presets in `Ciallo/export_presets.cfg`.
See [Engine setup](engine-setup.md) for local-engine templates and
[Bundled FFmpeg](../Ciallo/ExternalData/ffmpeg/README.md) for other export targets.

Official Windows, Linux, and macOS packages are built by CI. Signing and notarization
are only configured there, so local exports are development builds.

## Feature test builds

Use a feature test build when a branch needs downloadable builds for testers before
merging to `dev`. It produces short-lived GitHub Actions artifacts only — no GitHub
Release, no Steam upload.

Run the **Ciallo Feature Test** workflow from the Actions page and pick the branch in
the "Run workflow" ref dropdown. The workflow locks that branch's current commit SHA,
exports all three platforms, and retains the Actions artifacts for two days.

## Development builds

Run the **Ciallo Development Build** workflow from the Actions page. It locks the current
tip of `origin/dev`, exports all three platforms, and sets the build live on Steam's
**development** branch. Development builds do not create tags or GitHub Releases.

## Final releases (managed by owner)

Run the **Ciallo Publish Release** workflow from the Actions page. It locks the current
tip of `origin/main`, exports and verifies all three platforms, then passes through the
`production` environment's approval gate. Configure required reviewers there to require
owner approval. The workflow creates the immutable `v<config/version>` tag on the verified
commit and publishes the artifacts already built in that run.

Final builds create normal GitHub Releases and upload to Steam. A project owner must
promote the build to the **default** branch by hand in Steamworks App Admin > Builds.
A failed publish may reuse a tag only when it still points to the same verified commit.
If the source changes, increase `config/version`; release tags are never moved.

## Hotfix releases

Use a hotfix when the latest stable release needs an urgent public fix.
Hotfixes are temporary prereleases under the already published stable version:

```text
v0.1.0
v0.1.0-hotfix.1
v0.1.0-hotfix.2
v0.1.1
```

Hotfixes are allowed only for the latest stable GitHub Release.

Create hotfix branches from `main` using the `hotfix/` prefix:

```sh
git checkout main
git pull --ff-only
git checkout -b hotfix/fix-export-crash
```

Open a PR from `hotfix/fix-export-crash` to `main`.
Hotfix PRs must be squash merged.
After the PR is merged, the **Ciallo Hotfix Release** workflow automatically:

- exports and verifies the merged `main` commit;
- creates the next immutable `vX.Y.Z-hotfix.N` tag after the export succeeds;
- publishes the tag as a GitHub Prerelease with exported Windows, Linux, and macOS artifacts;
- uploads the build to Steam for manual promotion to **default**;
- creates a backport PR from `backport/hotfix-<PR>-to-dev` into `dev`.

If the automatic cherry-pick to `dev` conflicts, the workflow fails and the fix must be backported manually.
Do not manually bump `project.godot` for hotfixes; bump it for the next stable release.
