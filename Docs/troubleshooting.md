# Troubleshooting Guide

Find the symptom below and work through its checks. For routine instructions, use **Help > FAQ & How to Use**. For prerequisites and storage paths, use **Help > System Requirements**.

## The app will not start

1. Extract the entire distribution, including DLLs, configuration and native-library subfolders. Do not launch the EXE inside a ZIP.
2. Confirm that .NET Framework 4.7.2 or newer is available on the PC.
3. Try the complete distribution in a writable local folder. If company controls prevent execution, ask IT to review it rather than changing security settings.
4. Record the exact error and the executable version for support.

## BarTender cannot start or templates fail to render

Confirm that BarTender is installed, licensed and able to open a representative template manually. If it cannot, resolve that problem in BarTender first.

If it works manually but fails through PrintMaster, ask IT to check the registered BarTender automation components and compatibility with the app. Check any external data sources, fonts or printer configuration used by the template.

For a batch with missing outputs, test a failing template individually and compare the result count with the selected input count. A batch can continue after an individual template fails.

## Images look blurry or a barcode will not scan

- Inspect the original-image preview at 100% before judging a small thumbnail.
- Compare the source with the exported file at the intended output size. Avoid enlarging a small raster image beyond its useful resolution.
- For BTW rendering, review format and DPI. PNG avoids JPG compression artefacts.
- Check whether the PDF viewer, downstream system or print dialog has scaled the label. Compare the printed dimensions and retain required barcode margins.
- Verify the source barcode and final printed sample through your approved QC process. Increasing DPI alone is not a complete diagnosis.

## A collage is too dense or its printed size is wrong

Review the PDF paper size, orientation, grid and cell text. A larger sheet or fewer cells gives each image more room. A3, A2 and custom paper choices apply to PDF; Word and Excel exports follow their own layouts.

Confirm the exported PDF page dimensions and the print dialog's scaling settings. A larger PDF sheet will not print at its designed size if it is automatically reduced to a smaller printer sheet.

## A saved layout has missing images

Layouts retain source-image paths. Check whether the originals were renamed, moved, deleted or stored on a disconnected share. Restore the expected files or reopen the job with its original source folder available. Moving only a PDF or .npplcollage file does not copy the originals needed to edit the arrangement.

## Recovery is offered, or autosave reports a failure

After an interruption, review the offered draft before continuing and save a named layout once recovered. Recovery is separate from a regular saved layout.

If autosave fails, save the layout manually to a writable folder and check permissions and free space in the recovery location listed in System Requirements. Do not delete recovery files while trying to recover an unfinished job.

## Export or preferences cannot be saved

Check the chosen destination, available disk space and write permission. Close an exported file if another program has locked it. For duplicate source names, separate the outputs or preserve the source-folder structure.

Preferences are stored beside the EXE. If that folder is read-only, use an approved writable installation location. Moving the app to a different folder can make it appear that earlier preferences were lost.

## Help says Document Not Found

Use a current complete build or installer. The Documents folder should contain about.md, faq.md, license.md, releasenotes.md, requirements.md and troubleshooting.md. Current builds also embed these documents as a fallback.

If the problem remains, confirm the running EXE's version and location; an old shortcut may be opening a different copy. Include the Help menu item and filename shown in the error when reporting it.

## Explorer or a shortcut still shows an old logo

Confirm that the shortcut opens the current EXE. Close and reopen the folder to refresh its display. Windows can retain an older icon after a file is replaced; a stale Explorer icon does not by itself prove that the running app is old. For local rebuilds, the developer's tools/Refresh-AppIcons.ps1 script requests a shell refresh without deleting cache files.

## No update appears, or a download fails

Open **Help > Software Updates** and check manually. The app needs a newer public stable release with the expected installer and verification metadata; a local build can already be ahead of it. Startup remains quiet on check failures and when no suitable update is available.

If checking fails, review connectivity with IT and retry. If download verification fails, download again through the update section. Do not launch a partially downloaded installer or bypass a verification failure. The installer remains interactive: save work before allowing it to close the app.

## Reporting an issue

Contact your internal IT team or the software maintainer. Include:

- Installed executable version and whether you used Current UI or Modern UI (Preview).
- The affected workspace, exact steps and full error text.
- Whether one file fails or every file fails, plus the output settings used.
- A screenshot and relevant activity-log entries if available. The log location is listed in System Requirements; not every failure produces a log entry.

Review logs and screenshots for internal product names and paths before sharing them outside the approved support channel. Keep original templates and a copy of the affected layout available for diagnosis.
