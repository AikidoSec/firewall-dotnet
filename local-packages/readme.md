# Local Packages Directory

This directory serves as a local NuGet package source for end-to-end testing. It is intentionally kept empty but checked into source control to ensure:

1. The directory exists when NuGet tries to restore packages during CI builds
2. The directory structure is maintained across all development environments
3. E2E tests can reference local packages without needing to create the directory manually

The directory is created by the `EnsureLocalPackageSourceExists` target in `Directory.Build.targets` before NuGet restore operations, but checking it into source control provides an additional guarantee that the directory structure exists.

## Usage

This directory is used by the E2E test projects to reference local package versions of Aikido.Zen packages. The version is controlled by the `E2ETestPackageVersion` property defined in `obj/version.props`.

## Related Files

- `Directory.Build.targets`: Creates this directory if it doesn't exist during restore
- `obj/version.props`: Defines the package version used for E2E testing
- Sample app projects: Reference local packages using `$(E2ETestPackageVersion)`
