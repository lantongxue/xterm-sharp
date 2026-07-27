---
layout: doc
title: NuGet releases
description: Configure package authentication, validate a release build, and publish all XtermSharp packages from GitHub Actions.
category: Project
search: true
---

# NuGet releases

The `Publish packages to NuGet` workflow builds and publishes all 12 packages from one source
revision. Publishing is automatic when a GitHub Release changes to the `published` state.

## Configure authentication

The publish job uses the `nuget` GitHub environment. Configure one of these authentication methods:

1. Recommended: create a NuGet.org Trusted Publishing policy for this repository, the
   `.github/workflows/nuget.yml` workflow, and the `nuget` environment. Add the NuGet.org account
   username of the policy creator as the `NUGET_USER` GitHub environment variable. This is the
   creator's personal NuGet.org username, not the package-owner organization or GitHub username.
2. Alternative: add a `NUGET_API_KEY` repository or `nuget` environment secret. Scope the key to
   the `XtermSharp` package prefix, grant only package push permission, and use a short expiration.

When `NUGET_API_KEY` is absent, the workflow uses GitHub OIDC through `NuGet/login` to obtain a
short-lived publishing key.

## Prepare a release

Every packable project declares the same version. Update all 12 `<Version>` values together, then
run the complete verification matrix before creating the release.

The GitHub Release tag must match the project version. An optional `v` prefix is accepted:

```text
Project version: 0.1.0-alpha.1
Release tag:     v0.1.0-alpha.1
```

The workflow rejects unsupported tag formats or any project whose declared version differs from
the release. It overrides package repository metadata with the repository and commit being built.

## Validate without publishing

Run the workflow manually from the Actions page, enter the current version, and leave `publish`
disabled. The workflow restores the required MAUI workloads, packs every project on Windows,
verifies the exact 12-package set, and uploads the `.nupkg` files as a 30-day workflow artifact.

Enabling `publish` on a manual run uses the same protected publish job as a GitHub Release. NuGet
pushes use `--skip-duplicate`, so a rerun can safely complete a release that was only partially
published.
