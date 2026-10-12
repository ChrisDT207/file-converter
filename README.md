# File converter - Standalone Desktop File Converter & Studio

A modern, high-performance desktop application for batch file conversion, document merging, page splitting, and format transformation (inspired by iLovePDF). Built with a **React (TypeScript)** frontend and a robust **.NET 8 C#** backend, packaged into a standalone Windows desktop `.exe` via WPF and Edge WebView2.

---

## 1. 3-Layer N-Tier Architecture

The project strictly adheres to Object-Oriented Programming (OOP) and N-Tier separation of concerns:

```
file-converter/
├── FileConverter.sln                    # Root Solution File
├── build.bat                            # 1-Click Build & Packaging Script
├── run-dev.bat                          # Hot-Reload Development Runner
│
├── FileConverter.Desktop/               # [LAYER 1: PRESENTATION - DESKTOP HOST]
│   ├── App.xaml / App.xaml.cs           # DI Container Registration (Microsoft.Extensions.DependencyInjection)
│   ├── MainWindow.xaml / .cs            # WebView2 Host Window, OS Drag-and-Drop, Virtual Host Mapping
│   ├── Bridge/
│   │   └── IpcBridge.cs                 # Type-Safe Bidirectional JSON-RPC IPC Dispatcher
│   ├── Services/
│   │   └── NativeDialogService.cs       # Native Windows OpenFileDialog & OpenFolderDialog Implementation
│   └── FileConverter.Desktop.csproj     # Packages React dist into output wwwroot
│
├── client/                              # [LAYER 1: PRESENTATION - REACT UI]
│   ├── index.html                       # Desktop Web Application Host
│   ├── src/
│   │   ├── components/
│   │   │   ├── Header.tsx               # App Branding, Destination Directory, Dark Mode Toggle
│   │   │   ├── DropZone.tsx             # Prominent Drag & Drop Zone, "Browse Files" Trigger
│   │   │   ├── FileQueue.tsx            # Unlimited Queue, Format Chips, Bulk Selection, Removal
│   │   │   ├── ActionBar.tsx            # Context-Aware Actions (PDF, Word, Text, Merge, Split)
│   │   │   ├── SplitModal.tsx           # Page Range Selector (Single Pages or Custom Expressions)
│   │   │   └── ConversionModal.tsx      # Real-Time Progress Bar, Completion Stats & Output Links
│   │   ├── services/
│   │   │   └── nativeBridge.ts          # Type-Safe WebView2 Bridge with Browser Fallback Simulation
│   │   ├── types/
│   │   │   └── index.ts                 # DTOs, Enums, and Protocol Interfaces
│   │   ├── App.tsx                      # Root Application Controller & State Orchestrator
│   │   └── index.css                    # Windows 11 Fluent Design System (Light & Dark Themes)
│   └── package.json
│
├── FileConverter.Core/                  # [LAYER 2: BUSINESS LOGIC LAYER]
│   ├── Enums/
│   │   └── FileActionType.cs            # ConvertToPdf, PdfToWord, PdfToText, MergeDocuments, SplitDocument
│   ├── Models/
│   │   ├── FileItemDto.cs               # File Metadata, Format Categorization, Formatted Size
│   │   ├── ConversionRequestDto.cs      # Job Definition, Source Files, Output Config, Range Syntax
│   │   ├── ConversionResultDto.cs       # Output Paths, Time Elapsed, Success/Fail Counters
│   │   ├── ProgressReportDto.cs         # Percent, Current File, Stage Status Description
│   │   └── IpcMessage.cs                # Generic IPC Protocol Payloads
│   ├── Interfaces/
│   │   ├── IPdfConversionService.cs     # Images/Word/Text -> PDF contract
│   │   ├── IPdfExtractionService.cs     # PDF -> Word/Text/Images contract
│   │   ├── IDocumentMergeService.cs     # Multi-document consolidation contract
│   │   ├── IDocumentSplitService.cs     # Document splitting contract
│   │   ├── IFileSystemService.cs        # OS storage, path validation, explorer contract
│   │   ├── INativeDialogService.cs      # Windows native dialogs contract
│   │   └── IConversionOrchestrator.cs   # Central processing coordinator contract
│   └── Services/
│       └── ConversionOrchestrator.cs    # Multi-format coordinator, batch processor, progress reporter
│
└── FileConverter.Infrastructure/        # [LAYER 3: DATA & INFRASTRUCTURE LAYER]
    ├── FileSystem/
    │   └── FileSystemService.cs         # Windows FileSystem, Path Resolution, Explorer Execution
    └── Services/
        ├── PdfConversionService.cs      # ImageSharp/System.Drawing & OpenXml & PdfSharpCore -> PDF
        ├── PdfExtractionService.cs      # PdfPig & OpenXml WordprocessingDocument Generator
        ├── DocumentMergeService.cs      # PdfSharpCore Document Assembler & Page Importer
        └── DocumentSplitService.cs      # PdfSharpCore Page Range Parser & Document Slicer
```

---

## 2. Universal File Cross-Conversion Matrix

FileFlow now supports full multi-directional conversion across **Documents, Images, Data & Spreadsheets, and Web Formats**:

| Source Category | Source Format | Available Target Conversions | Underlying Engine |
| :--- | :--- | :--- | :--- |
| **PDF** | `.pdf` | `DOCX`, `TXT`, `MD`, `HTML`, `PNG`, `JPG`, `BMP`, Split, Merge | `PdfPig` text parsing + OpenXML + `PdfSharpCore` |
| **Word** | `.docx`, `.doc` | `PDF`, `TXT`, `MD`, `HTML` | OpenXML `WordprocessingDocument` + `PdfSharpCore` |
| **Markdown** | `.md` | `HTML`, `PDF`, `DOCX`, `TXT` | `Markdig` + OpenXML + `PdfSharpCore` |
| **Plain Text** | `.txt`, `.log` | `PDF`, `DOCX`, `HTML`, `MD` | `PdfSharpCore` paginator + OpenXML |
| **Web** | `.html`, `.htm` | `PDF`, `DOCX`, `TXT`, `MD` | Regex HTML parser + OpenXML + `PdfSharpCore` |
| **Images** | `.png`, `.jpg`, `.jpeg`, `.webp`, `.bmp`, `.gif`, `.tiff`, `.ico` | `PNG`, `JPG`, `WEBP`, `BMP`, `GIF`, `TIFF`, `ICO`, `PDF` | `System.Drawing.Common` encoders + ICO header synthesizer |
| **CSV** | `.csv` | `JSON`, `XLSX`, `XML`, `HTML`, `PDF`, `TXT` | OpenXML `SpreadsheetDocument` + `System.Text.Json` + `PdfSharpCore` |
| **JSON** | `.json` | `CSV`, `XLSX`, `XML`, `HTML`, `PDF`, `TXT` | JSON object tree flattener + OpenXML |
| **XML** | `.xml` | `JSON`, `CSV`, `HTML`, `TXT` | `XDocument` parser & recursive XML-to-JSON mapper |
| **Excel** | `.xlsx`, `.xls` | `CSV`, `JSON`, `HTML`, `PDF`, `TXT` | OpenXML `SpreadsheetDocument` row/cell extractor |
| **Multi-File** | Any mixed files | Single unified `.pdf` | Pre-conversion pipeline + `PdfSharpCore` page assembler |
| **Split Tool** | `.pdf` | Multi-page `.pdf`s or ranges (`1-3, 5, 8-10`) | `PdfSharpCore` range slicer |

---

## 3. How the Frontend Bridges with the Backend

1. **Host Container:** The C# desktop application initializes Microsoft Edge WebView2 inside a native WPF window shell (`MainWindow.xaml`).
2. **Asset Serving (No External Web Server):** In production, WebView2 maps `https://app.assets/` directly to the compiled React bundle inside `wwwroot/` via `SetVirtualHostNameToFolderMapping`. No external localhost ports or security firewall prompts required.
3. **Bidirectional IPC:**
   - **Frontend to Backend:**
     ```typescript
     window.chrome.webview.postMessage(JSON.stringify({ id, action, payload }));
     ```
   - **Backend to Frontend:**
     ```csharp
     MainWebView.CoreWebView2.PostWebMessageAsJson(jsonString);
     ```
   The `nativeBridge.ts` module wraps this into typed `async/await` Promises with correlation IDs.
4. **Native Windows Dialogs:** Clicking "Browse Files" or "Save to..." dispatches to `INativeDialogService`, invoking native Windows 11/10 `OpenFileDialog` and `OpenFolderDialog` dialogs with multi-select support.
5. **Native OS Drag-and-Drop:** Dragging files onto the drop zone or directly onto the native desktop window border automatically intercepts `DataFormats.FileDrop` and populates the queue.

---

## 4. Building the Standalone Executable (.exe)

### Option A: 1-Click Build Script
Double-click `build.bat` in the root folder. It compiles the React TypeScript client, builds the .NET solution, and outputs the desktop application to `./dist/FileConverter.Desktop.exe`.

### Option B: Manual CLI Build
```bash
# 1. Build React UI
cd client
npm install
npm run build
cd ..

# 2. Compile .NET Solution
dotnet build FileConverter.sln -c Release

# 3. Publish Standalone Single-File Executable
dotnet publish FileConverter.Desktop/FileConverter.Desktop.csproj \
  -r win-x64 \
  -c Release \
  -p:PublishSingleFile=true \
  --self-contained false \
  -o ./dist
```

To create a **100% self-contained** executable (which includes the .NET runtime so end-users don't need .NET installed):
```bash
dotnet publish FileConverter.Desktop/FileConverter.Desktop.csproj \
  -r win-x64 \
  -c Release \
  -p:PublishSingleFile=true \
  --self-contained true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./dist-standalone
```

---

## 5. Development Workflow (Hot-Reload)

To run the application with live React hot-reloading:
1. Start the Vite dev server:
   ```bash
   cd client
   npm run dev
   ```
2. In a second terminal, launch the desktop app:
   ```bash
   dotnet run --project FileConverter.Desktop/FileConverter.Desktop.csproj
   ```
   The C# desktop app automatically detects the running Vite dev server at `http://localhost:5173` and loads it directly with instant hot-module replacement (HMR).
   *(Or simply run `run-dev.bat` which launches both automatically!)*
