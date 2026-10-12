namespace FileConverter.Core.Models;

public class FormatDefinition
{
    public string Extension { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "document", "spreadsheet", "presentation", "image", "raw", "audio", "video", "3d", "archive"
    public string MimeType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool ReadOnly { get; set; } = false; // e.g. Camera RAW, RAR are read-only sources
}

public static class ConversionRegistry
{
    // Canonical format registry with MIME types and categories
    public static readonly Dictionary<string, FormatDefinition> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        // === DOCUMENTS & E-BOOKS ===
        ["pdf"]   = new() { Extension = "pdf", Label = "PDF Document (.pdf)", Category = "document", MimeType = "application/pdf", Description = "Portable Document Format" },
        ["docx"]  = new() { Extension = "docx", Label = "Word Document (.docx)", Category = "document", MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", Description = "Microsoft Word Document" },
        ["doc"]   = new() { Extension = "doc", Label = "Word 97-2003 (.doc)", Category = "document", MimeType = "application/msword", Description = "Legacy Word Document" },
        ["docm"]  = new() { Extension = "docm", Label = "Word Macro-Enabled (.docm)", Category = "document", MimeType = "application/vnd.ms-word.document.macroEnabled.12", Description = "Word Macro Document" },
        ["odt"]   = new() { Extension = "odt", Label = "OpenDocument Text (.odt)", Category = "document", MimeType = "application/vnd.oasis.opendocument.text", Description = "OpenOffice/LibreOffice Writer" },
        ["rtf"]   = new() { Extension = "rtf", Label = "Rich Text Format (.rtf)", Category = "document", MimeType = "application/rtf", Description = "Rich Text Document" },
        ["txt"]   = new() { Extension = "txt", Label = "Plain Text (.txt)", Category = "document", MimeType = "text/plain", Description = "Unformatted Text File" },
        ["log"]   = new() { Extension = "log", Label = "Log File (.log)", Category = "document", MimeType = "text/plain", Description = "System / Event Log Text" },
        ["md"]    = new() { Extension = "md", Label = "Markdown (.md)", Category = "document", MimeType = "text/markdown", Description = "Lightweight Markdown Markup" },
        ["html"]  = new() { Extension = "html", Label = "HTML Document (.html)", Category = "document", MimeType = "text/html", Description = "HyperText Markup Language" },
        ["htm"]   = new() { Extension = "htm", Label = "HTML Document (.htm)", Category = "document", MimeType = "text/html", Description = "HTML Web Page" },
        ["xhtml"] = new() { Extension = "xhtml", Label = "XHTML Document (.xhtml)", Category = "document", MimeType = "application/xhtml+xml", Description = "Extensible HyperText Markup" },
        ["epub"]  = new() { Extension = "epub", Label = "EPUB E-Book (.epub)", Category = "document", MimeType = "application/epub+zip", Description = "Electronic Publication E-Book" },
        ["mobi"]  = new() { Extension = "mobi", Label = "MOBI E-Book (.mobi)", Category = "document", MimeType = "application/x-mobipocket-ebook", Description = "Mobipocket E-Book" },
        ["azw3"]  = new() { Extension = "azw3", Label = "Kindle E-Book (.azw3)", Category = "document", MimeType = "application/vnd.amazon.ebook", Description = "Amazon Kindle Format 8" },
        ["fb2"]   = new() { Extension = "fb2", Label = "FictionBook (.fb2)", Category = "document", MimeType = "application/x-fictionbook+xml", Description = "FictionBook 2.0" },
        ["latex"] = new() { Extension = "latex", Label = "LaTeX Source (.latex)", Category = "document", MimeType = "application/x-latex", Description = "LaTeX Academic Typesetting" },
        ["tex"]   = new() { Extension = "tex", Label = "TeX Source (.tex)", Category = "document", MimeType = "application/x-tex", Description = "TeX Document Source" },
        ["rst"]   = new() { Extension = "rst", Label = "reStructuredText (.rst)", Category = "document", MimeType = "text/x-rst", Description = "reStructuredText Document" },
        ["org"]   = new() { Extension = "org", Label = "Org Mode (.org)", Category = "document", MimeType = "text/x-org", Description = "Emacs Org Mode Document" },
        ["wpd"]   = new() { Extension = "wpd", Label = "WordPerfect (.wpd)", Category = "document", MimeType = "application/wordperfect", Description = "Corel WordPerfect Document" },
        ["pages"] = new() { Extension = "pages", Label = "Apple Pages (.pages)", Category = "document", MimeType = "application/x-iwork-pages-sffpages", Description = "Apple Pages Document" },

        // === SPREADSHEETS & DATA ===
        ["xlsx"]  = new() { Extension = "xlsx", Label = "Excel Workbook (.xlsx)", Category = "spreadsheet", MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Description = "Microsoft Excel Workbook" },
        ["xls"]   = new() { Extension = "xls", Label = "Excel 97-2003 (.xls)", Category = "spreadsheet", MimeType = "application/vnd.ms-excel", Description = "Legacy Excel Spreadsheet" },
        ["xlsm"]  = new() { Extension = "xlsm", Label = "Excel Macro-Enabled (.xlsm)", Category = "spreadsheet", MimeType = "application/vnd.ms-excel.sheet.macroEnabled.12", Description = "Macro-Enabled Excel Workbook" },
        ["xlsb"]  = new() { Extension = "xlsb", Label = "Excel Binary (.xlsb)", Category = "spreadsheet", MimeType = "application/vnd.ms-excel.sheet.binary.macroEnabled.12", Description = "Excel Binary Spreadsheet" },
        ["ods"]   = new() { Extension = "ods", Label = "OpenDocument Calc (.ods)", Category = "spreadsheet", MimeType = "application/vnd.oasis.opendocument.spreadsheet", Description = "OpenOffice/LibreOffice Calc" },
        ["csv"]   = new() { Extension = "csv", Label = "CSV Spreadsheet (.csv)", Category = "spreadsheet", MimeType = "text/csv", Description = "Comma-Separated Values" },
        ["tsv"]   = new() { Extension = "tsv", Label = "TSV Spreadsheet (.tsv)", Category = "spreadsheet", MimeType = "text/tab-separated-values", Description = "Tab-Separated Values" },
        ["json"]  = new() { Extension = "json", Label = "JSON Data (.json)", Category = "spreadsheet", MimeType = "application/json", Description = "JavaScript Object Notation" },
        ["xml"]   = new() { Extension = "xml", Label = "XML Data (.xml)", Category = "spreadsheet", MimeType = "application/xml", Description = "eXtensible Markup Language" },
        ["yaml"]  = new() { Extension = "yaml", Label = "YAML Data (.yaml)", Category = "spreadsheet", MimeType = "application/x-yaml", Description = "YAML Ain't Markup Language" },
        ["yml"]   = new() { Extension = "yml", Label = "YAML Data (.yml)", Category = "spreadsheet", MimeType = "application/x-yaml", Description = "YAML Data Format" },
        ["numbers"]= new() { Extension = "numbers", Label = "Apple Numbers (.numbers)", Category = "spreadsheet", MimeType = "application/x-iwork-numbers-sffnumbers", Description = "Apple Numbers Spreadsheet" },

        // === PRESENTATIONS ===
        ["pptx"]  = new() { Extension = "pptx", Label = "PowerPoint (.pptx)", Category = "presentation", MimeType = "application/vnd.openxmlformats-officedocument.presentationml.presentation", Description = "Microsoft PowerPoint" },
        ["ppt"]   = new() { Extension = "ppt", Label = "PowerPoint 97-2003 (.ppt)", Category = "presentation", MimeType = "application/vnd.ms-powerpoint", Description = "Legacy PowerPoint" },
        ["pptm"]  = new() { Extension = "pptm", Label = "PowerPoint Macro (.pptm)", Category = "presentation", MimeType = "application/vnd.ms-powerpoint.presentation.macroEnabled.12", Description = "Macro-Enabled Presentation" },
        ["odp"]   = new() { Extension = "odp", Label = "OpenDocument Impress (.odp)", Category = "presentation", MimeType = "application/vnd.oasis.opendocument.presentation", Description = "LibreOffice Impress Presentation" },
        ["key"]   = new() { Extension = "key", Label = "Apple Keynote (.key)", Category = "presentation", MimeType = "application/x-iwork-keynote-sffkey", Description = "Apple Keynote Presentation" },

        // === RASTER & VECTOR IMAGES ===
        ["png"]   = new() { Extension = "png", Label = "PNG Image (.png)", Category = "image", MimeType = "image/png", Description = "Portable Network Graphics" },
        ["jpg"]   = new() { Extension = "jpg", Label = "JPEG Image (.jpg)", Category = "image", MimeType = "image/jpeg", Description = "JPEG Compressed Image" },
        ["jpeg"]  = new() { Extension = "jpeg", Label = "JPEG Image (.jpeg)", Category = "image", MimeType = "image/jpeg", Description = "Joint Photographic Experts Group" },
        ["webp"]  = new() { Extension = "webp", Label = "WebP Image (.webp)", Category = "image", MimeType = "image/webp", Description = "WebP Image Format" },
        ["avif"]  = new() { Extension = "avif", Label = "AVIF Image (.avif)", Category = "image", MimeType = "image/avif", Description = "AV1 Image File Format" },
        ["gif"]   = new() { Extension = "gif", Label = "GIF Image (.gif)", Category = "image", MimeType = "image/gif", Description = "Graphics Interchange Format" },
        ["tiff"]  = new() { Extension = "tiff", Label = "TIFF Image (.tiff)", Category = "image", MimeType = "image/tiff", Description = "Tagged Image File Format" },
        ["tif"]   = new() { Extension = "tif", Label = "TIFF Image (.tif)", Category = "image", MimeType = "image/tiff", Description = "Tagged Image File" },
        ["bmp"]   = new() { Extension = "bmp", Label = "Bitmap (.bmp)", Category = "image", MimeType = "image/bmp", Description = "Windows Bitmap" },
        ["ico"]   = new() { Extension = "ico", Label = "Icon (.ico)", Category = "image", MimeType = "image/x-icon", Description = "Windows Icon / Favicon" },
        ["heic"]  = new() { Extension = "heic", Label = "HEIC Image (.heic)", Category = "image", MimeType = "image/heic", Description = "High Efficiency Image Container" },
        ["heif"]  = new() { Extension = "heif", Label = "HEIF Image (.heif)", Category = "image", MimeType = "image/heif", Description = "High Efficiency Image File" },
        ["psd"]   = new() { Extension = "psd", Label = "Photoshop (.psd)", Category = "image", MimeType = "image/vnd.adobe.photoshop", Description = "Adobe Photoshop Document" },
        ["svg"]   = new() { Extension = "svg", Label = "Scalable Vector (.svg)", Category = "image", MimeType = "image/svg+xml", Description = "Scalable Vector Graphics" },
        ["eps"]   = new() { Extension = "eps", Label = "Encapsulated PostScript (.eps)", Category = "image", MimeType = "application/postscript", Description = "Encapsulated PostScript" },
        ["ai"]    = new() { Extension = "ai", Label = "Adobe Illustrator (.ai)", Category = "image", MimeType = "application/illustrator", Description = "Adobe Illustrator Artwork" },
        ["tga"]   = new() { Extension = "tga", Label = "Truevision TGA (.tga)", Category = "image", MimeType = "image/x-tga", Description = "Targa Image File" },
        ["hdr"]   = new() { Extension = "hdr", Label = "Radiance HDR (.hdr)", Category = "image", MimeType = "image/vnd.radiance", Description = "High Dynamic Range Image" },
        ["exr"]   = new() { Extension = "exr", Label = "OpenEXR (.exr)", Category = "image", MimeType = "image/x-exr", Description = "OpenEXR High Dynamic Range" },
        ["dds"]   = new() { Extension = "dds", Label = "DirectDraw Surface (.dds)", Category = "image", MimeType = "image/x-dds", Description = "DirectX Texture File" },
        ["pcx"]   = new() { Extension = "pcx", Label = "PC Paintbrush (.pcx)", Category = "image", MimeType = "image/x-pcx", Description = "PCX Bitmap Image" },
        ["pnm"]   = new() { Extension = "pnm", Label = "Portable AnyMap (.pnm)", Category = "image", MimeType = "image/x-portable-anymap", Description = "Netpbm Image Format" },
        ["xpm"]   = new() { Extension = "xpm", Label = "X PixMap (.xpm)", Category = "image", MimeType = "image/x-xpixmap", Description = "X PixMap Format" },
        ["j2k"]   = new() { Extension = "j2k", Label = "JPEG 2000 (.j2k)", Category = "image", MimeType = "image/jp2", Description = "JPEG 2000 Codestream" },
        ["jp2"]   = new() { Extension = "jp2", Label = "JPEG 2000 (.jp2)", Category = "image", MimeType = "image/jp2", Description = "JPEG 2000 Image" },

        // === CAMERA RAW (READ-ONLY) ===
        ["cr2"]   = new() { Extension = "cr2", Label = "Canon RAW (.cr2)", Category = "raw", MimeType = "image/x-canon-cr2", ReadOnly = true, Description = "Canon Digital Camera RAW 2" },
        ["cr3"]   = new() { Extension = "cr3", Label = "Canon RAW (.cr3)", Category = "raw", MimeType = "image/x-canon-cr3", ReadOnly = true, Description = "Canon Digital Camera RAW 3" },
        ["nef"]   = new() { Extension = "nef", Label = "Nikon RAW (.nef)", Category = "raw", MimeType = "image/x-nikon-nef", ReadOnly = true, Description = "Nikon Electronic Format RAW" },
        ["arw"]   = new() { Extension = "arw", Label = "Sony RAW (.arw)", Category = "raw", MimeType = "image/x-sony-arw", ReadOnly = true, Description = "Sony Alpha RAW Image" },
        ["dng"]   = new() { Extension = "dng", Label = "Digital Negative (.dng)", Category = "raw", MimeType = "image/x-adobe-dng", ReadOnly = true, Description = "Adobe Digital Negative RAW" },
        ["raf"]   = new() { Extension = "raf", Label = "Fuji RAW (.raf)", Category = "raw", MimeType = "image/x-fuji-raf", ReadOnly = true, Description = "Fuji RAW Image" },
        ["orf"]   = new() { Extension = "orf", Label = "Olympus RAW (.orf)", Category = "raw", MimeType = "image/x-olympus-orf", ReadOnly = true, Description = "Olympus RAW File" },
        ["rw2"]   = new() { Extension = "rw2", Label = "Panasonic RAW (.rw2)", Category = "raw", MimeType = "image/x-panasonic-rw2", ReadOnly = true, Description = "Panasonic Lumix RAW" },

        // === AUDIO ===
        ["mp3"]   = new() { Extension = "mp3", Label = "MP3 Audio (.mp3)", Category = "audio", MimeType = "audio/mpeg", Description = "MPEG-1 Audio Layer III" },
        ["wav"]   = new() { Extension = "wav", Label = "WAV Audio (.wav)", Category = "audio", MimeType = "audio/wav", Description = "Waveform Audio File Format" },
        ["flac"]  = new() { Extension = "flac", Label = "FLAC Lossless (.flac)", Category = "audio", MimeType = "audio/flac", Description = "Free Lossless Audio Codec" },
        ["aac"]   = new() { Extension = "aac", Label = "AAC Audio (.aac)", Category = "audio", MimeType = "audio/aac", Description = "Advanced Audio Coding" },
        ["ogg"]   = new() { Extension = "ogg", Label = "OGG Vorbis (.ogg)", Category = "audio", MimeType = "audio/ogg", Description = "Ogg Vorbis Compressed Audio" },
        ["oga"]   = new() { Extension = "oga", Label = "OGG Audio (.oga)", Category = "audio", MimeType = "audio/ogg", Description = "Ogg Audio Container" },
        ["m4a"]   = new() { Extension = "m4a", Label = "M4A Audio (.m4a)", Category = "audio", MimeType = "audio/mp4", Description = "MPEG-4 Audio" },
        ["wma"]   = new() { Extension = "wma", Label = "Windows Media Audio (.wma)", Category = "audio", MimeType = "audio/x-ms-wma", Description = "Windows Media Audio" },
        ["alac"]  = new() { Extension = "alac", Label = "Apple Lossless (.alac)", Category = "audio", MimeType = "audio/alac", Description = "Apple Lossless Audio Codec" },
        ["aiff"]  = new() { Extension = "aiff", Label = "AIFF Audio (.aiff)", Category = "audio", MimeType = "audio/x-aiff", Description = "Audio Interchange File Format" },
        ["opus"]  = new() { Extension = "opus", Label = "Opus Audio (.opus)", Category = "audio", MimeType = "audio/opus", Description = "Opus Interactive Audio Codec" },
        ["amr"]   = new() { Extension = "amr", Label = "AMR Voice (.amr)", Category = "audio", MimeType = "audio/amr", Description = "Adaptive Multi-Rate Audio" },
        ["au"]    = new() { Extension = "au", Label = "Sun AU (.au)", Category = "audio", MimeType = "audio/basic", Description = "Sun/NeXT Audio File" },
        ["voc"]   = new() { Extension = "voc", Label = "Creative Voice (.voc)", Category = "audio", MimeType = "audio/x-voc", Description = "Creative Labs Voice File" },

        // === VIDEO & ANIMATION ===
        ["mp4"]   = new() { Extension = "mp4", Label = "MP4 Video (.mp4)", Category = "video", MimeType = "video/mp4", Description = "MPEG-4 Part 14 Video" },
        ["mkv"]   = new() { Extension = "mkv", Label = "Matroska Video (.mkv)", Category = "video", MimeType = "video/x-matroska", Description = "Matroska Multimedia Container" },
        ["mov"]   = new() { Extension = "mov", Label = "QuickTime Video (.mov)", Category = "video", MimeType = "video/quicktime", Description = "Apple QuickTime Movie" },
        ["avi"]   = new() { Extension = "avi", Label = "AVI Video (.avi)", Category = "video", MimeType = "video/x-msvideo", Description = "Audio Video Interleave" },
        ["webm"]  = new() { Extension = "webm", Label = "WebM Video (.webm)", Category = "video", MimeType = "video/webm", Description = "WebM Open Media Format" },
        ["flv"]   = new() { Extension = "flv", Label = "Flash Video (.flv)", Category = "video", MimeType = "video/x-flv", Description = "Adobe Flash Video" },
        ["wmv"]   = new() { Extension = "wmv", Label = "Windows Media Video (.wmv)", Category = "video", MimeType = "video/x-ms-wmv", Description = "Windows Media Video" },
        ["m4v"]   = new() { Extension = "m4v", Label = "M4V Video (.m4v)", Category = "video", MimeType = "video/x-m4v", Description = "iTunes Video Format" },
        ["3gp"]   = new() { Extension = "3gp", Label = "3GPP Mobile (.3gp)", Category = "video", MimeType = "video/3gpp", Description = "3GPP Mobile Video" },
        ["ts"]    = new() { Extension = "ts", Label = "MPEG Transport Stream (.ts)", Category = "video", MimeType = "video/mp2t", Description = "MPEG-2 Transport Stream" },
        ["m2ts"]  = new() { Extension = "m2ts", Label = "BDAV Video (.m2ts)", Category = "video", MimeType = "video/mp2t", Description = "Blu-ray BDAV Video" },
        ["vob"]   = new() { Extension = "vob", Label = "DVD Video Object (.vob)", Category = "video", MimeType = "video/x-ms-vob", Description = "DVD Video Object" },
        ["ogv"]   = new() { Extension = "ogv", Label = "OGG Video (.ogv)", Category = "video", MimeType = "video/ogg", Description = "Ogg Theora Video" },
        ["asf"]   = new() { Extension = "asf", Label = "Advanced Systems (.asf)", Category = "video", MimeType = "video/x-ms-asf", Description = "Advanced Systems Format" },
        ["rmvb"]  = new() { Extension = "rmvb", Label = "RealMedia (.rmvb)", Category = "video", MimeType = "application/vnd.rn-realmedia-vbr", Description = "RealMedia Variable Bitrate" },

        // === 3D MODELS ===
        ["obj"]   = new() { Extension = "obj", Label = "Wavefront 3D (.obj)", Category = "3d", MimeType = "model/obj", Description = "Wavefront 3D Object Format" },
        ["stl"]   = new() { Extension = "stl", Label = "Stereolithography (.stl)", Category = "3d", MimeType = "model/stl", Description = "3D Printable Geometry" },
        ["gltf"]  = new() { Extension = "gltf", Label = "glTF 2.0 (.gltf)", Category = "3d", MimeType = "model/gltf+json", Description = "GL Transmission Format JSON" },
        ["glb"]   = new() { Extension = "glb", Label = "Binary glTF (.glb)", Category = "3d", MimeType = "model/gltf-binary", Description = "Binary GL Transmission Format" },
        ["ply"]   = new() { Extension = "ply", Label = "Polygon File (.ply)", Category = "3d", MimeType = "model/ply", Description = "Stanford Polygon File Format" },
        ["fbx"]   = new() { Extension = "fbx", Label = "Autodesk FBX (.fbx)", Category = "3d", MimeType = "application/octet-stream", Description = "Filmbox 3D Asset Format" },
        ["dae"]   = new() { Extension = "dae", Label = "Collada 3D (.dae)", Category = "3d", MimeType = "model/vnd.collada+xml", Description = "COLLADA Digital Asset Exchange" },
        ["3ds"]   = new() { Extension = "3ds", Label = "3D Studio (.3ds)", Category = "3d", MimeType = "image/x-3ds", Description = "3D Studio Mesh" },
        ["blend"] = new() { Extension = "blend", Label = "Blender File (.blend)", Category = "3d", MimeType = "application/x-blender", Description = "Blender 3D Project" },

        // === ARCHIVES & PACKAGES ===
        ["zip"]   = new() { Extension = "zip", Label = "ZIP Archive (.zip)", Category = "archive", MimeType = "application/zip", Description = "Standard ZIP Archive" },
        ["7z"]    = new() { Extension = "7z", Label = "7-Zip Archive (.7z)", Category = "archive", MimeType = "application/x-7z-compressed", Description = "7-Zip High Compression" },
        ["tar"]   = new() { Extension = "tar", Label = "Tape Archive (.tar)", Category = "archive", MimeType = "application/x-tar", Description = "POSIX Tape Archive" },
        ["tar.gz"]= new() { Extension = "tar.gz", Label = "GZipped Tar (.tar.gz)", Category = "archive", MimeType = "application/gzip", Description = "Compressed Tarball" },
        ["tgz"]   = new() { Extension = "tgz", Label = "Tarball (.tgz)", Category = "archive", MimeType = "application/gzip", Description = "GZipped Tarball" },
        ["tar.bz2"]=new() { Extension = "tar.bz2", Label = "BZip2 Tar (.tar.bz2)", Category = "archive", MimeType = "application/x-bzip2", Description = "BZip2 Compressed Tar" },
        ["xz"]    = new() { Extension = "xz", Label = "XZ Archive (.xz)", Category = "archive", MimeType = "application/x-xz", Description = "XZ Compressed File" },
        ["wim"]   = new() { Extension = "wim", Label = "Windows Image (.wim)", Category = "archive", MimeType = "application/x-ms-wim", Description = "Windows Imaging Format" },
        ["rar"]   = new() { Extension = "rar", Label = "RAR Archive (.rar)", Category = "archive", MimeType = "application/vnd.rar", ReadOnly = true, Description = "WinRAR Archive (Extract Only)" },
        ["iso"]   = new() { Extension = "iso", Label = "Disc Image (.iso)", Category = "archive", MimeType = "application/x-iso9660-image", ReadOnly = true, Description = "Optical Disc Image (Extract Only)" },
        ["cab"]   = new() { Extension = "cab", Label = "Cabinet (.cab)", Category = "archive", MimeType = "application/vnd.ms-cab-compressed", ReadOnly = true, Description = "Windows Cabinet Archive" },
        ["gz"]    = new() { Extension = "gz", Label = "GZip File (.gz)", Category = "archive", MimeType = "application/gzip", ReadOnly = true, Description = "GNU Zip Compressed Stream" }
    };

    public static string GetCategory(string ext)
    {
        var clean = ext.TrimStart('.').ToLowerInvariant();
        return Formats.TryGetValue(clean, out var def) ? def.Category : "document";
    }

    public static string GetMimeType(string ext)
    {
        var clean = ext.TrimStart('.').ToLowerInvariant();
        return Formats.TryGetValue(clean, out var def) ? def.MimeType : "application/octet-stream";
    }

    public static List<TargetOptionDto> GetAvailableTargetsForExtension(string ext)
    {
        var clean = ext.TrimStart('.').ToLowerInvariant();
        var category = GetCategory(clean);
        var results = new List<TargetOptionDto>();

        switch (category)
        {
            case "document":
                AddTargets(results, clean, new[] { "pdf", "docx", "odt", "rtf", "txt", "html", "md", "epub", "latex" });
                break;

            case "spreadsheet":
                AddTargets(results, clean, new[] { "csv", "xlsx", "ods", "json", "xml", "tsv", "yaml", "html", "pdf", "txt" });
                break;

            case "presentation":
                AddTargets(results, clean, new[] { "pdf", "pptx", "odp", "png", "jpg" });
                break;

            case "image":
                AddTargets(results, clean, new[] { "png", "jpg", "webp", "avif", "gif", "tiff", "bmp", "ico", "svg", "pdf", "tga", "dds", "jp2" });
                break;

            case "raw":
                // Camera RAW converts into standard raster formats
                AddTargets(results, clean, new[] { "jpg", "png", "webp", "tiff", "bmp", "avif", "pdf" });
                break;

            case "audio":
                AddTargets(results, clean, new[] { "mp3", "wav", "flac", "aac", "ogg", "m4a", "opus", "wma", "alac", "aiff" });
                break;

            case "video":
                // Video cross-conversions, plus audio and animation extraction
                AddTargets(results, clean, new[] { "mp4", "mkv", "webm", "avi", "mov", "wmv", "mp3", "wav", "aac", "flac", "gif", "webp" });
                break;

            case "3d":
                AddTargets(results, clean, new[] { "obj", "stl", "gltf", "glb", "ply", "dae", "3ds" });
                break;

            case "archive":
                AddTargets(results, clean, new[] { "zip", "7z", "tar", "tar.gz", "xz" });
                break;

            default:
                AddTargets(results, clean, new[] { "pdf", "txt" });
                break;
        }

        return results;
    }

    private static void AddTargets(List<TargetOptionDto> list, string currentExt, string[] candidateExts)
    {
        foreach (var candidate in candidateExts)
        {
            if (candidate.Equals(currentExt, StringComparison.OrdinalIgnoreCase)) continue;
            if (currentExt is "jpeg" && candidate is "jpg") continue;
            if (currentExt is "jpg" && candidate is "jpeg") continue;
            if (currentExt is "yml" && candidate is "yaml") continue;

            if (Formats.TryGetValue(candidate, out var def))
            {
                list.Add(new TargetOptionDto
                {
                    Extension = def.Extension,
                    Label = def.Label,
                    Category = def.Category,
                    Description = def.Description
                });
            }
        }
    }

    public static Dictionary<string, List<TargetOptionDto>> GetAllMatrix()
    {
        var dict = new Dictionary<string, List<TargetOptionDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in Formats.Keys)
        {
            dict[key] = GetAvailableTargetsForExtension(key);
        }
        return dict;
    }
}
