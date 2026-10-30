# Code signing policy — pending Foundation approval

METANOIA submitted an application to SignPath Foundation on 2026-10-30; receipt was confirmed by the application form. No Foundation certificate or project approval has been received, and the currently available executable is unsigned. We do not claim SignPath currently signs this project.

## Team

- Committer, reviewer and release approver: [vOnyxxx](https://github.com/vOnyxxx).
- Repository: https://github.com/vOnyxxx/METANOIA
- Privacy policy: [PRIVACY.md](../../PRIVACY.md).

## Release controls

Signing requests must originate from GitHub-hosted runners building source on the repository's default branch. Unit and integration tests must pass before publishing the unsigned artifact used as signing input. The signing path requires the GitHub `release-signing` environment; configure an authorized maintainer as required reviewer. Each production signing request must also receive the maintainer's manual approval in SignPath.

The API token stays in GitHub environment secrets. Never store it in source files or send it through chat. SignPath access should be limited to this repository and a submitter authorized for the configured project/policy. Configure MFA for maintainers. Protect the default branch and review changes to build scripts.

## Configuration after acceptance

The workflow `.github/workflows/windows-release.yml` can build unsigned artifacts without SignPath. The `request_signature` input defaults to false. Only enable it once Foundation acceptance and permissions are complete.

Set environment variables `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG` and `SIGNPATH_SIGNING_POLICY_SLUG`, and environment secret `SIGNPATH_API_TOKEN`. These must be supplied by the approved SignPath account, not invented. Import `artifact-configuration.xml` and link the trusted GitHub build system.

After approval, add the required attribution to the download page: “Free code signing provided by SignPath.io, certificate by SignPath Foundation”. Keep the application marked unsigned until an actual signed artifact has a valid Authenticode signature. The workflow checks that signature before accepting its output.

## Eligibility / application

The application requires a public source repository, open-source license, download page, project description, build system, reputation evidence and maintainer contact information. METANOIA is new: describe its maturity honestly. Local tests are evidence of development, not proof of public adoption. Acceptance and turnaround are controlled by the Foundation.

Sources: https://signpath.org/terms ; https://signpath.org/apply ; https://docs.signpath.io/trusted-build-systems/github
