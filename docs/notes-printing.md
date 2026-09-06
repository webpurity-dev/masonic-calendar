# Printer Notes

## Current `-showprint` Proof

- **File:** `output/V2.4/2.4.5/master_v1.2.4.5-all-sections-showPrint.pdf`
- **Generated:** 6 September 2026
- **Total pages:** **338**
- **Orientation:** Portrait
- **PDF media box:** 315.12 x 437.04 pt, approximately **4.377 x 6.070 in** (**111.17 x 154.18 mm**)

## Document Size

The finished trim size is A6 portrait:

| Area | Inches | Millimetres |
|---|---:|---:|
| Finished trim | 4.134 x 5.827 in | 105 x 148 mm |
| PDF media page | 4.370 x 6.063 in | 111 x 154 mm |
| Bleed on each edge | 0.118 in | 3 mm |

The cover is full bleed. Keep important text, logos, and faces clear of the trim and binding edges.

## Margins

The PDF uses alternating margins for booklet binding. Values below are CSS page margins measured from the edge of the 111 x 154 mm media page and include the 3 mm bleed allowance.

| Page | Top | Bottom | Left | Right |
|---|---:|---:|---:|---:|
| Right / recto / odd | 0.197 in / 5 mm | 0.354 in / 9 mm | 0.354 in / 9 mm | 0.157 in / 4 mm |
| Left / verso / even | 0.197 in / 5 mm | 0.354 in / 9 mm | 0.157 in / 4 mm | 0.354 in / 9 mm |
| First page / cover | 0 in / 0 mm | 0 in / 0 mm | 0 in / 0 mm | 0 in / 0 mm |

The binding-side margin is therefore 9 mm (0.354 in) on both recto and verso pages. The outer margin is 4 mm (0.157 in). The extra bottom margin provides space for page numbers.

## Bleed And Crop Marks

The `-showprint` proof draws configured black crop marks at all four trim corners. It does not add native PDF printer marks; the marks are proof artwork rendered into each page.

- Trim boundary inset: **3 mm / 0.118 in** from the media edge
- Crop-mark length: **5 mm / 0.197 in**, limited by the trim inset where necessary
- Corner gap: **3 mm / 0.118 in**
- Stroke width: **0.15 px**
- Crop-mark colour: **black**
- Bleed boundary: shown separately with `-showbleed` as a dotted guide
- Margin boundary: shown separately with `-showmargins` as a red dotted guide

The clean production PDF should be generated without `-showprint`, `-showbleed`, or `-showmargins` unless the printer specifically asks for proof overlays.

## File To Send To The Printer

- Send the generated **PDF**, not the proofing HTML file.
- Use the PDF generated with `-output pdf` and keep `PrintBackground` enabled so colours, images, and backgrounds are included.
- Do not scale the PDF to fit another paper size. Print at **100% / Actual Size** with the document page size honoured.
- Do not add printer margins, headers, footers, automatic rotation, or extra booklet imposition unless agreed with the printer.
- Ask the printer to impose and bind the pages as an A6 booklet. The source PDF is already laid out for the finished page size.

Example production command:

```powershell
cd src/MasonicCalendar.Console
dotnet run -- -template master_v1 -output pdf
```

## Proofing Overlays

The overlay flags are for checking artwork and page geometry. They should not be used on the clean production PDF unless the printer specifically requests them.

```powershell
# Show the configured bleed boundary
dotnet run -- -template master_v1 -output html -showbleed

# Show crop marks at the trim corners
dotnet run -- -template master_v1 -output html -showprint

# Show the alternating page margins
dotnet run -- -template master_v1 -output html -showmargins

# Show all proof overlays together
dotnet run -- -template master_v1 -output html -showbleed -showprint -showmargins
```

Use the overlays to confirm that:

- background images and colours extend beyond the trim where required;
- no text, page numbers, or important artwork is inside the crop-mark area;
- the binding-side gutter alternates correctly between left and right pages; and
- the first cover page has the intended full-bleed treatment.

## Printer Preflight

Before sending the file, check a representative proof at 100%:

- Page size reports as A6 portrait.
- The cover reaches the page edges and has no unintended white border.
- Crop marks, bleed guides, and margin guides are absent from the clean PDF.
- Small text is legible and no table rows, captions, or page numbers are clipped.
- Images are sharp at final size and have no missing-file or loading placeholders.
- Page count and page order are correct, especially around section starts and the contents pages.
- The final PDF opens without repair warnings and prints with backgrounds enabled.

## Colour And Paper

- The generated document is intended for professional digital or offset printing. Confirm the printer's required colour profile, paper stock, and finish before final production.
- Request a physical or calibrated contract proof when colour matching is important, particularly for the cover image and branded colours.
- Ask the printer to confirm whether they require single pages or an imposed booklet PDF. Do not impose the document manually unless they provide those instructions.

## Common Print Problems

| Problem | Check |
|---|---|
| White edge around the cover | Print at 100% and verify the cover image extends through the bleed area. |
| Content too close to the binding | Check the alternating inside margin and request a binding-safe proof. |
| Colours or images missing | Ensure the PDF was generated with backgrounds enabled; do not use the HTML proof as the print master. |
| Pages appear clipped or shifted | Disable printer scaling and automatic margin options, then recheck the PDF page size. |
| Crop marks visible in the job | Regenerate the clean PDF without proof-overlay flags. |
