# Local Packages Directory

This directory is intentionally kept in the repository to serve as a local NuGet package source for CI/CD builds.

During the end-to-end test process, the `Aikido.Zen.DotNetCore` project is packed and the resulting `.nupkg` file is placed here. This allows the test suite to restore it as a real package, simulating a production environment.

**This directory should remain empty in source control, except for this README file.** Any packages within it are ignored by `.gitignore`.
