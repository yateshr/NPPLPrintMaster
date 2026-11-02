# System Requirements & Installation

This page covers setup and dependencies. Everyday workflows are in **Help > FAQ & How to Use**; failure diagnosis is in **Help > Troubleshooting Guide**.

## Base requirements

| Requirement | Details |
| --- | --- |
| Platform | A company-approved Windows desktop that supports the required .NET Framework. |
| Runtime | Microsoft **.NET Framework 4.7.2 or newer**. The modern .NET runtime alone is not a substitute. |
| Application files | Keep the EXE, configuration file, DLLs and native-library subfolders together. |
| Storage | Enough free space for installation, source images, exports and temporary processing. Setup shows its installation-space estimate. |
| Permissions | Read access to source folders and write access to the application settings, export and recovery locations. |
| Resources | Large originals, dense collages and high-DPI rendering need more memory and processing time. Test a representative job on the intended PC. |

## Additional requirements by task

- **BarTender extraction and BTW library rendering:** a compatible, properly licensed BarTender installation with its automation components registered. Use IT to confirm edition and environment compatibility. Referenced fonts, external data and printer configuration may also be needed by individual templates.
- **Existing-image formatting and collage PDF creation:** these tasks do not use the BarTender rendering engine.
- **SAP DMS upload:** separate SAP access and authorisation, using the company's established procedure. PrintMaster does not provide an SAP login or direct upload service.
- **Update checks and downloads:** access to the project's GitHub release service. Connectivity is not required merely to open a saved local image job.

## Installer or portable copy?

### Windows installer

Run the setup EXE and follow the welcome, information and destination pages. Save open work before continuing. The default per-user location is:

```text
%LOCALAPPDATA%\Programs\NPPLPrintMaster
```

When upgrading, use the existing installation folder to continue using its settings. Setup excludes the user's settings.ini from the files it ships.

### Portable ZIP or local build

Extract the entire ZIP to a writable folder and run **NPPLPrintMaster.exe**. Do not run it from inside the ZIP or copy only the EXE. A local build can be run directly from its complete bin/Release folder.

Portable use does not mean every setting is stored in that folder: recovery drafts and downloaded updates use the Windows user-profile locations below.

## File locations

| Item | Location |
| --- | --- |
| Preferences | settings.ini beside the running EXE. |
| Help documents | Documents subfolder beside the EXE; embedded copies are available as a fallback. |
| Collage recovery | %LOCALAPPDATA%\NPPLPrintMaster\CollageRecovery |
| Downloaded installers | %LOCALAPPDATA%\NPPLPrintMaster\Updates |
| Activity log | The Windows Documents folder, then NPPLPrintMaster\PrintLog.txt. Documents may be redirected by company policy. |
| Saved layouts and exports | The folders you choose in the application. |

## Moving to another PC

Keep the complete application distribution together. Copy saved layouts with their source images, retain the folder structure where possible, and verify file access on the new PC. Copy settings.ini only when you want to carry preferences across; saved folder paths may need changing.

Install and license BarTender separately if that PC will render BTW templates. It is not bundled with PrintMaster.
