# Work Plan: Configurable Cover Spread

**Date:** 2026-09-22  
**Status:** Implemented  
**Approval:** Approved by user on 2026-09-22

## Configuration Strategy

Add a configuration-driven cover spread block to `document/master_v1.yaml`:

- Spine text: `Dorset Masonic Calendar 2026`
- Spine width: `25mm`
- Spine text margin: `5mm` on both sides
- Spread width: `247mm` (`111mm` back panel + `25mm` spine + `111mm` front panel)
- Spread height: `154mm`
- Existing front and back cover images remain the outer panels

No cover text or dimensions will be hardcoded in the renderer or template.

## Files Affected

- `document/master_v1.yaml` — add cover-spread configuration and change the `-cover` section selection to use the spread section.
- `document/templates/cover-spread.html` — new wraparound spread template with back cover, spine, and front cover panels.
- `src/MasonicCalendar.Core/Loaders/DocumentLayoutLoader.cs` — add the YAML model needed to load cover-spread configuration.
- `src/MasonicCalendar.Core/Renderers/SchemaPdfRenderer.cs` — emit the spread page size and use the spread template/page style for `-cover` output.
- `src/MasonicCalendar.Console/Program.cs` — keep `-cover` output routing and filename behavior, if no renderer API adjustment is required.

## Scope

- `-cover` renders one spread page only.
- `-nocover` remains unchanged and continues rendering the complete document without front/back cover sections.
- The normal full-document render remains unchanged.
- Front and back cover artwork continues to come from the existing configured image paths.
- Spine text is vertical and centred unless a different orientation is requested.

## Approach

1. Add a `cover_spread` configuration block with dimensions, text, image paths, and text margins.
2. Add a `cover-spread.html` template using CSS grid/flex layout for back panel, spine, and front panel.
3. Add a dedicated page style or renderer option so the cover-only document uses `247mm 154mm` rather than A6 page size.
4. Render the spread through the existing `-cover` path.
5. Validate HTML and PDF output dimensions, section count, artwork presence, and configured spine text.

## Risks

- Puppeteer/Paged.js may paginate the wide spread unexpectedly unless the dedicated page size is applied before pagination.
- Existing cover image positioning variables are A6-oriented and may need spread-specific positioning.
- PDF trim/bleed and crop-mark behavior may need separate spread handling.

## Rollback

Revert the cover-spread YAML block, new template, loader model, and renderer page-size changes. Existing `cover` and `back_cover` sections remain available in the current implementation until the spread is enabled.

## Implementation Results

- Added `cover_spread` configuration to `document/master_v1.yaml`.
- Added `document/templates/cover-spread.html`.
- Added `CoverSpreadConfig` to the layout model.
- Added `RenderCoverSpreadAsync()` and routed `-cover` to it.
- `-nocover` remains on the full-document path with normal first-page margins.
