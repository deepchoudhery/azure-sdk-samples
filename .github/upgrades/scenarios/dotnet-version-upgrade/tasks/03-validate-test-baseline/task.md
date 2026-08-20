# 03-validate-test-baseline: Validate the upgraded build and regression baseline

Run final automated validation after the atomic upgrade. Build the full scoped workspace and re-run every test project recorded under `## Test Baseline` in `scenario-instructions.md`, treating failures as potential upgrade regressions and following the execution-stage baseline validation flow.

Document any deferred recommendations, but do not accept skipped or failing baseline tests as successful validation.

**Done when**: The full scoped workspace builds with zero errors and all recorded baseline tests pass on `net10.0`.
