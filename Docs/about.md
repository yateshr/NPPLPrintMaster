# About NPPLPrintMaster

**Print. Layout. Create.**

NPPLPrintMaster is a Windows desktop application for preparing label images, job-card artwork and image-collage PDFs for the production workflow at **Nirmal Poly Plast Pvt. Ltd.** It brings label extraction, image formatting and layout preparation into one workspace.

## Built by Yatesh Rohit

**Author and developer: Yatesh Rohit**

IT Department, Nirmal Poly Plast Pvt. Ltd.

Yatesh Rohit created the application to reduce repetitive label-image preparation work between IT, Production Planning and Control (PPC), Quality Control (QC) and Production.

## How the project began

The earlier process involved opening a BarTender template, capturing and cropping a screenshot, naming the image and preparing a sample for a job card. The project began with CMD and PowerShell automation, then developed into a C# Windows Forms application.

The SAP DMS workflow gave that preparation a practical purpose: approved label images could be stored against the appropriate Finished Goods code through the company's SAP process. PrintMaster prepares those files; it does not upload them directly to SAP or approve labels on behalf of QC.

## Workspaces

| Workspace | Purpose |
| --- | --- |
| Extract Labels | Render BarTender templates into image files. |
| Format Images | Add padding and borders, and prepare image output. |
| Quick Job Card / Job Cards | Assemble product, carton and related artwork. |
| BTW Image Library / Image Library | Find and review label artwork. |
| Image Collage | Arrange images, edit captions and export a sheet layout. |
| Freeform Designer / Freeform Studio | Edit a layout with freely positioned elements. |

Some workspace names differ between Current UI and Modern UI (Preview). Both appearances use the same application workflows. Settings is at the bottom of the sidebar, above the developer credit.

## Development and acknowledgements

The application uses C# and .NET Framework Windows Forms, BarTender automation for template rendering, and open-source .NET libraries for image, document and barcode processing. BarTender and SAP are separate products with their own licensing requirements.

Technical documentation, the GitHub/open-source community and AI-assisted development tools, including Google Gemini, have supported research, learning and implementation work. These acknowledgements do not imply endorsement by those providers.

## Where to find details

- **Help > FAQ & How to Use:** everyday workflows and questions.
- **Help > System Requirements:** installation, dependencies and file locations.
- **Help > Troubleshooting Guide:** diagnosing a specific problem.
- **Help > Release Notes:** published changes and local improvements under review.
- **Help > License Agreement:** the software's existing licence terms.
- **Help > Software Updates:** the installed executable version and update status.

Copyright © 2026 Yatesh Rohit. All Rights Reserved. See the License Agreement for the full notice and terms.
