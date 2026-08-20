## Detected Hints

No active breakdown hints apply. The scope has only two projects, no stubs, a known
package replacement path, one test dependent, and no incompatible test framework.

## Breakdown Decisions

### task: 02-upgrade-contoso-documents
- Kept atomic: the selected All-at-Once strategy requires the two TFM changes and the
  coordinated package/API/test migration to remain one bounded upgrade pass.
- Final review pass remains atomic: the table result cap and blob BOM handling are two focused
  behavior-preservation fixes in the same production project with coverage in its sole test
  dependent. `execution.md`, `breakdown-hints/common.md`, and `breakdown-hints/test.md` were
  evaluated; no decomposition condition matched.
