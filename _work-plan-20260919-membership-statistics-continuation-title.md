# Membership Statistics Continuation Title Plan

## Configuration Strategy

Use the existing `section_title` and `heading_spacing` model values supplied by `MembershipStatisticsSectionRenderer`. The existing static subtitle remains part of the template; no new labels or configuration values are introduced.

## Files Affected

| File | Change | Why |
|---|---|---|
| `document/templates/_data-driven/membership-statistics-page.html` | Render the title and subtitle before the continuation table. | Align the continuation page with the established meetings-table and unit-index page-title pattern. |
| `_work-plan-20260919-membership-statistics-continuation-title.md` | Record scope, implementation, risks, and validation. | Required plan for this non-trivial template change. |

## Scope

- Repeat the configured section title and existing explanatory subtitle whenever a second membership-statistics page is rendered.
- Preserve the current data split, table headers, totals row, and pagination configuration.

## Approach

1. Add the same centered title block used on the first statistics page before the continuation table.
2. Apply the existing `heading_spacing` value to retain consistent title-to-table spacing.
3. Render the document PDF and verify the continuation page begins with the same heading placement.

## Risks

- The additional heading consumes vertical space on page two; the configured row count may need adjustment only if it causes an overflow.

## Rollback

Remove the continuation title block from `membership-statistics-page.html`.

## Approval Status

Approved by the user on 2026-09-19.