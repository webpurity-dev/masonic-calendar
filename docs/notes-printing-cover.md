# Cover Printing Notes
Notes to be presented to the printer alongside the cover-spread PDF generated with `-cover`.

## Current Version
**2.4.6.5**

## Cover Layout
The supplied cover file is one outside-face spread, arranged from left to right:

```text
Outer Back Cover | Spine | Outer Front Cover
```

| Area | Finished width | Supplied width | Finished height | Supplied height |
|---|---:|---:|---:|---:|
| Outer back cover OBC | 105 mm | 111 mm | 148 mm | 154 mm |
| Spine | 20 mm | 20 mm | 148 mm | 154 mm |
| Outer front cover OFC | 105 mm | 111 mm | 148 mm | 154 mm |
| Total cover spread | 230 mm | 242 mm | 148 mm | 154 mm |

The outer back cover uses `cover-img-back.jpg`. The outer front cover uses `cover-img-yellow.jpg`.

## Spine
| Setting | Value |
|---|---|
| Width | 20 mm |
| Text | Dorset Masonic Calendar 2026 |
| Text orientation | Vertical, reading from bottom to top |
| Text size | 16 pt |
| Text margin | 5 mm |

## Print Specification
| Area | Millimetres |
|---|---:|
| Finished cover height | 148 mm |
| Supplied cover height | 154 mm |
| Finished spread width | 230 mm |
| Supplied PDF sheet | 242 x 154 mm |
| Bleed | 3 mm |

The supplied file includes the configured 3 mm bleed around the outer cover panels. The printer should trim the cover to the finished 230 x 148 mm spread, subject to their binding and spine allowances.

## Crop Marks
Generate the proof/print file with:

```powershell
dotnet run -- -template master_v1 -output pdf -cover -showprint
```

Crop-mark settings:
- Length: 5 mm
- Corner gap: 3 mm
- Stroke: 0.15 px
- Colour: black

For a proof that also shows the bleed boundary, use:

```powershell
dotnet run -- -template master_v1 -output pdf -cover -showbleed -showprint
```

## Printing Instructions
- Print at 100% or Actual Size; do not scale the supplied PDF.
- Preserve the page size, landscape orientation, backgrounds, images, and spine text.
- Do not add printer margins, headers, footers, automatic rotation, or booklet imposition.
- Apply cover finishing, lamination, and PUR binding as agreed in the print quotation.
- Confirm the printer's required spine allowance before production. The artwork is currently configured for a 20 mm spine.

## Preflight Checklist
- Confirm the PDF is a single landscape cover spread.
- Confirm panel order is outer back cover, spine, then outer front cover.
- Confirm the supplied sheet measures 242 x 154 mm.
- Confirm the finished outer-cover spread measures 230 x 148 mm before bleed.
- Confirm the spine is 20 mm wide and the vertical text is centred and legible.
- Confirm cover images extend through the bleed without white borders.
- Confirm crop marks appear at the outer trim corners and are removed during trimming.
- Confirm the printer will not scale, crop, rotate, or impose the supplied PDF.
- Request a physical or calibrated contract proof before production.
