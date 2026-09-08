# Printing Notes

## Current Version

**2.4.5.1**

## Print Specification

| Area | Inches | Millimetres |
|---|---:|---:|
| Finished trim | 4.134 x 5.827 in | 105 x 148 mm |
| PDF media page | 4.370 x 6.063 in | 111 x 154 mm |
| Bleed on each edge | 0.118 in | 3 mm |

The document is A6 portrait with a full-bleed cover.

## Binding Margins

The PDF uses alternating margins for booklet binding. Measurements are from the media-page edge and include the 3 mm bleed allowance.

| Page | Top | Bottom | Inside | Outside |
|---|---:|---:|---:|---:|
| Right / recto / odd | 5 mm | 9 mm | 9 mm | 4 mm |
| Left / verso / even | 5 mm | 9 mm | 9 mm | 4 mm |
| First page / cover | 0 mm | 0 mm | 0 mm | 0 mm |

The inside margin is 9 mm, the outside margin is 4 mm, and the 9 mm bottom margin provides space for page numbers.

## Bleed And Crop Marks

The document includes 3 mm bleed on every edge. Crop-mark proof draws black proof crop marks at all four trim corners. These are rendered proof artwork, not native PDF printer marks.

- Trim boundary inset: 3 mm
- Crop-mark length: 5 mm
- Corner gap: 3 mm
- Crop-mark colour: black

## Production File

- Supply the generated PDF, not the proofing HTML.
- Print at 100% or Actual Size; do not scale to another paper size.
- Preserve backgrounds, images, page size, and orientation.
- Do not add printer margins, headers, footers, automatic rotation, or booklet imposition unless agreed with the printer.
- Confirm whether the printer requires single pages or an imposed booklet PDF. The supplied PDF is laid out at the finished A6 page size.

```powershell
cd src/MasonicCalendar.Console
dotnet run -- -template master_v1 -output pdf
```

To generate a crop-mark proof:

```powershell
dotnet run -- -template master_v1 -output pdf -showprint
```

## Preflight Checklist

- Confirm the PDF reports A6 portrait with 3 mm bleed.
- Check the cover extends to the media-page edge without an unintended white border.
- Confirm text, page numbers, and essential artwork remain clear of the trim and binding edges.
- Check small text, table rows, captions, and page numbers for clipping.
- Confirm images are sharp at final size and contain no missing-file placeholders.
- Check page count, page order, section starts, and contents pages.
- Open the final PDF without repair warnings and ensure backgrounds print.
- Confirm colour profile, paper stock, finish, and imposition requirements with the printer.

Request a physical or calibrated contract proof when accurate colour reproduction is important, especially for cover artwork and branded colours.

## Common Print Problems

| Problem | Check |
|---|---|
| White edge around the cover | Print at 100% and confirm the cover extends through the bleed area. |
| Content too close to the binding | Check the alternating 9 mm inside margin and request a binding-safe proof. |
| Colours or images missing | Use the generated PDF with backgrounds enabled; do not use the HTML proof as the print master. |
| Pages appear clipped or shifted | Disable printer scaling, margins, and automatic rotation, then recheck the PDF page size. |
| Crop marks visible in the job | Regenerate the production PDF without `-showprint`. |