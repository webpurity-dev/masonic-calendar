# Work Plan: Cover As Three Pages

**Date:** 2026-09-24  
**Status:** Approved  
**Approval:** Approved by user request on 2026-09-24

## Configuration Strategy

Reuse the existing `cover_spread` dimensions and styling values for the three-page output. Add `cover_spread.spine_template: "cover-spine.html"` so the new spine template path remains configuration-driven.

## Files Affected

- `document/master_v1.yaml` - configure the spine template path.
- `document/templates/cover-spine.html` - render a centred spine on a full panel-sized page.
- `src/MasonicCalendar.Core/Loaders/DocumentLayoutLoader.cs` - load the spine template property.
- `src/MasonicCalendar.Core/Renderers/SchemaPdfRenderer.cs` - render OFC, OBC, and SPINE as three separate pages using existing cover configuration and proof overlays.
- `src/MasonicCalendar.Console/Program.cs` - parse `-cover-as-pages`, route it to the new renderer method, and update help/output naming.

## Scope

- `-cover` continues to render the existing single cover spread.
- `-cover-as-pages` renders three panel-sized pages in order: OFC, OBC, SPINE.
- `-cover-as-pages` is mutually exclusive with `-cover` and `-nocover`.
- Existing `-showbleed`, `-showprint`, `-showmargins`, HTML, and PDF output paths remain available.

## Validation

Render HTML and PDF with `-cover-as-pages`; verify three pages, configured front/back artwork, centred spine, and configured page dimensions.
