# Verification

`dotnet run --project tests/RegressionTests.csproj -c Release` runs the actual resizing/config/option source against minimal game data stubs. It checks growth, shrinkage, odd/non-square sizes, all four materials, cache hits, material-only changes, original templates with matching bounds, ports/door positions and malformed settings. It does not simulate Unity UI or save loading.

`dotnet run --project tests/RegressionTests.csproj -c Release -- --check-mod src/bin/Release/netstandard2.1/CustomRocketInterior.dll` verifies that the release embeds PLib UI, has no external PLib assembly reference, and has no obsolete `TMPro.FaceInfo` type reference. Run `package.sh` to build, run both checks and package only the release files.

The optional `RuntimeSmoke` project runs inside the game. Build the main mod first, then `dotnet build tests/RuntimeSmoke/RuntimeSmoke.csproj -c Release` using `ONI_MANAGED_DIR` if needed. With the game closed, copy `RuntimeSmoke.dll` into the same Dev mod folder as the compiled mod. Enable that Dev mod, start the game and check Player.log for `[CRI-SMOKE] PASS`. It opens/closes the real options dialog three times, verifies localized labels and resizes the real medium-habitat template through 94×94 → 10×10 → 38×38. It does not load/save a colony or write settings. Close the game and remove the diagnostic DLL afterwards. Never include the diagnostic DLL in a release.

The v0.4 smoke run additionally checks the actual wall/window deconstruction flag helper and fixed-port exclusions, real solid-tile prefab classification (including mesh/airflow exclusions), support monitoring on all seven utilities, and the power plug connection gate. It runs at the main menu; it does not perform a duplicant deconstruction chore or save/load/rebuild cycle inside a live colony. Those remain gameplay acceptance checks.

The real U59 `OffsetGroups.BuildReachabilityTable` was also checked in a .NET harness: the original hull filter excludes work from directly below, while the normal-tile null filter permits it. This validates the ceiling reachability change independently of a live colony.
