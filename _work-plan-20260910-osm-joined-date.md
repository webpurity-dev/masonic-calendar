# OSM Empty Joined Column Plan

## Configuration Strategy

Extend the existing content-derived column visibility pattern. `UnitModelBuilder` will determine whether any joining past head has a non-empty mapped `JoinedDate` and whether any past head has a non-empty `YearInstalled`, then expose model flags for the shared template. This is data-driven and has no new hardcoded display labels or per-order settings. The current YAML mappings remain valid so other orders retain columns when their data supplies them.

## Files Affected

| File | Change | Why |
|---|---|---|
| `src/MasonicCalendar.Core/Renderers/Utilities/UnitModelBuilder.cs` | Add content-presence checks and model flags. | The model owns conditional field availability for past-heads and joining-past-heads tables. |
| `document/templates/_data-driven/unit-page.html` | Conditionally render `Year` for past heads and `Joined` for joining past heads, expanding `Conclaves` to 24% when joined dates are absent. | Prevent empty columns from being emitted and use the joined-date width for conclave data. |
| `src/MasonicCalendar.Tests/UnitModelBuilderJoiningUnitsDisplayTests.cs` | Render both joined-date and past-heads-year visibility states. | Prevent regression of the data-driven column visibility and width behavior. |
| `_work-plan-20260910-osm-joined-date.md` | Record scope, approach, risks, and validation. | Required plan for this non-trivial two-code-file change. |

## Scope

- Apply the rule to all orders using the shared unit-page template.
- Show the `Joined` column when at least one joining past head has a non-empty `JoinedDate`.
- Do not change the OSM source mapping, labels, CSV data, or other joining-past-heads fields.
- When `Joined` is omitted and `Conclaves` is present, expand `Conclaves` from 12% to 24%.
- Hide the past-heads `Year` heading and values when no past head provides a `YearInstalled` value; leave the remaining widths unchanged.

## Approach

1. Calculate `hasJoinedDateData` from `SchemaUnit.JoinPastMasters` alongside the existing `hasPastUnitsData` check.
2. Add `showJoinedDateColumn` to the existing `sectionHeadings` model data.
3. Guard the shared template's `Joined` header and value cell with that flag, and conditionally use a 24% `Conclaves` width when `Joined` is absent.
4. Run focused rendering tests to verify the column is absent and `Conclaves` expands for OSM-style rows, while joined-date rows retain both 12% columns.
5. Run a focused rendering test to verify the past-heads `Year` heading is hidden only when all installed years are empty.

## Risks

- The rule is shared, so any order with entirely blank joined dates will now omit the column; that is the intended content-driven behavior.
- The conditional width is shared, so all orders without joined dates will give that space to their visible joining-units column.
- The past-heads rule is shared, so any order without installed-year data will omit the `Year` heading and values.

## Rollback

Revert the `showJoinedDateColumn` and `showPastHeadsInstalledColumn` model flags, their template guards, conditional widths, and focused tests.

## Approval Status

✅ Approved by user on 2026-09-10.

The past-heads extension was approved by user on 2026-09-10.

## Validation

- `dotnet test` with a fully qualified filter: 2 passed. The tests confirm OSM-style rows omit `Joined` and render `Conclaves` at 24%, while rows with joined dates retain both 12% columns.
- `dotnet run --project .\\src\\MasonicCalendar.Console\\MasonicCalendar.Console.csproj -- -template master_v1 -output html -section osm_units`: completed successfully for five OSM units.
- Generated `output/master_v1.2.4.6-osm_units.html` contains `Conclaves` columns at 24% and no `Joined` headers.
- Focused past-heads rendering tests: 2 passed. The tests confirm `Year` is omitted when all installed years are empty and retained when an installed year exists.
- Re-rendered `osm_units` successfully after the past-heads update. The generated HTML contains neither `Year` nor `Joined` headers, while retaining the 24% `Conclaves` headers.
- The broader existing joining-past-heads test file has one unrelated failing assertion: it expects `RowsPerTable` to be 46 while the current master layout configures 40.
