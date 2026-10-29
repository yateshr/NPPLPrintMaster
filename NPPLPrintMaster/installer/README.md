# Publishing updates

The application checks the latest public stable GitHub Release at
https://github.com/yateshr/NPPLPrintMaster/releases/latest on each launch.
There are no startup messages for network failures, missing releases or an up-to-date installation.
Help > Software Updates contains status, retry, Download now, and Download & install.
Download now saves a verified installer under LocalAppData/NPPLPrintMaster/Updates and opens its folder.
Install opens the interactive installer targeting the running application's directory; it never force-exits the application.
Save open work before accepting the installer's application-closing request.

## Release procedure

1. Increase AssemblyVersion and AssemblyFileVersion in Properties/AssemblyInfo.cs to the same four-part number, such as 1.0.1.0.
2. Build NPPLPrintMaster.csproj in Release configuration, from a clean output directory so no test programs are included.
3. Install Inno Setup 6 on the build machine and compile installer/NPPLPrintMaster.iss using ISCC.exe.
4. Create a public, stable GitHub Release with the matching tag, for example v1.0.1.0.
5. Upload output/installer/NPPLPrintMaster-Setup-1.0.1.0.exe as a release asset. GitHub supplies its SHA-256 digest through the release API.
6. Test a previous installed version: startup offers the update; Download now only downloads; Download & install opens setup and updates that installation.

Do not upload GitHub source-code ZIPs as installers. Prereleases and drafts are ignored.
Both downloaded length and SHA-256 must match GitHub's asset metadata before installation is enabled.
Keep the repository/releases public; no credentials are embedded in the app. If releases are private, a public release feed is needed.
The updater is implemented locally; this change does not publish a release or install Inno Setup.
Code signing the release installer is recommended for Windows publisher identification.
