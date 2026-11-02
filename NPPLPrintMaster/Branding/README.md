# Approved orange PrintMaster logo

The user approved the orange layered-page/P concept on 29 October 2026. This replaces the incorrect gold barcode-and-wrench icon.

- PrintMaster.Source.png: high-resolution orange app tile, isolated from the approved concept with the built-in image generation tool. Transparent outside the rounded square; white layered sheets, folded corner, negative-space P and four collage squares.
- PrintMaster.png: 256px display asset exported from the source, preserving transparency and aspect ratio.
- PrintMaster.ico: Windows icon with 16, 20, 24, 32, 40, 48, 64, 128 and 256px bitmap frames from the same source. Bitmap/DIB frames are required for correct .NET Framework Icon.ToBitmap rendering.
- InstallerWelcome.bmp and InstallerSmall.bmp: welcome/finish and header graphics using the same logo.

Sidebar placements are 52px in Current UI and 46px in Modern UI; Software Updates uses 48px. All app forms and dynamically created dialogs inherit BrandedForm. Explorer, taskbar, shortcuts and uninstall use the embedded executable icon. No separate logo file is needed to run the app.

The 1.2.0.0 build is for local review. Do not push or publish until the user explicitly requests it. The installer explicitly enables its branded Welcome and Finished pages and shows formatted before/after installation guides, both credited to Yatesh Rohit. Help documents ship in Documents/ and are embedded as a fallback; Settings is anchored above the sidebar credit with a gear icon.

## Asset-generation prompt

Built-in image_gen, edit of the approved concept sheet: isolate only the upper-right orange rounded-square app tile; preserve white stacked pages, folded corner, negative-space P and four grid cutouts; no wordmark, redesign, wrench or gold; transparent outside the tile. Conversion to PNG/ICO/BMP only resizes or packages the generated artwork.
