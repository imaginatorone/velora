# Contributing

Velora welcomes focused bug fixes, tests, documentation, platform work, and
small runtime improvements. The project is still in `0.x`, so discuss large
changes before spending time on an implementation.

## Before you start

- Search existing issues and pull requests.
- Open an issue for new syntax, public API changes, large refactors, or new
  dependencies.
- Keep game-specific stories, branding, and production artwork outside the
  engine repository. Small fixtures used by tests are welcome.
- Follow the [Code of Conduct](CODE_OF_CONDUCT.md).

Small fixes with an obvious scope do not need a separate issue.

## Set up the project

Fork the repository, create a branch from `main`, and follow the platform
instructions in [BUILDING.md](BUILDING.md).

```sh
cmake --preset debug -DVELORA_WARNINGS_AS_ERRORS=ON
cmake --build --preset debug --parallel
ctest --preset debug -C Debug
```

Use a short branch name such as `fix/save-header` or `docs/audio-reference`.

## Code and documentation style

- Use C++20 and the existing project structure.
- Format C++ files with the repository `.clang-format`.
- Use English for source, logs, comments, documentation, issues, and pull
  requests.
- Prefer names and small functions that explain themselves. Add comments only
  when they capture a constraint or a non-obvious decision.
- Use ordinary ASCII punctuation in source and documentation.
- Keep changes scoped. Do not mix unrelated cleanup into a functional patch.
- Do not commit build output, editor settings, save data, or downloaded
  dependencies.

Run formatting on changed C++ files:

```sh
clang-format -i path/to/changed_file.cpp path/to/changed_file.h
```

## VelScript changes

A language change should include:

- lexer or parser coverage in `tests/CoreTests.cpp`
- runtime coverage when behavior changes
- an update to [docs/velscript.md](docs/velscript.md)
- an update to `assets/scripts/example.vel` when the feature belongs in the
  introductory example

Do not add a second file extension or an alias for old language names.

## Tests

Before opening a pull request:

1. Configure with `VELORA_WARNINGS_AS_ERRORS=ON`.
2. Build the project.
3. Run the full CTest suite.
4. Run `cmake --install` and check that the package contains the executable,
   assets, and license files.
5. Test the affected behavior manually when automated coverage is not enough.

Pull requests are built on Windows, Linux, and macOS. You do not need local
access to all three platforms, but call out any platform you could not test.

## Pull requests

Keep the title direct and use an imperative verb, for example `Fix quick-load
slot validation`. In the description:

- explain the problem and the chosen solution
- list the checks you ran
- link the related issue
- include screenshots only when the visible result changed
- mention compatibility or migration concerns

Review feedback should be resolved with new commits while review is active.
Maintainers may squash commits when merging.

## Licensing

By submitting a contribution, you agree that your contribution may be
distributed under the project's [MIT License](LICENSE). You retain copyright
to your work. No contributor license agreement is required.
