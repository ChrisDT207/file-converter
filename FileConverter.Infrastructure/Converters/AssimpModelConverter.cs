using Assimp;
using CliWrap;
using FileConverter.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileConverter.Infrastructure.Converters;

public class AssimpModelConverter : IFileConverter
{
    private readonly IBinaryLocator _binaryLocator;
    private readonly ILogger<AssimpModelConverter> _logger;

    private static readonly HashSet<string> ModelExts = new(StringComparer.OrdinalIgnoreCase)
    {
        "obj", "stl", "gltf", "glb", "ply", "fbx", "dae", "3ds", "blend"
    };

    public AssimpModelConverter(IBinaryLocator binaryLocator, ILogger<AssimpModelConverter> logger)
    {
        _binaryLocator = binaryLocator;
        _logger = logger;
    }

    public string Name => "Assimp 3D Mesh Engine";
    public int Priority => 80;

    public bool IsAvailable() => true;

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        var src = sourceExtension.TrimStart('.').ToLowerInvariant();
        var tgt = targetExtension.TrimStart('.').ToLowerInvariant();

        if (src == tgt) return false;

        // If source is .blend, requires Blender CLI
        if (src == "blend" && !_binaryLocator.IsBinaryAvailable("blender"))
        {
            return false;
        }

        return ModelExts.Contains(src) && ModelExts.Contains(tgt);
    }

    public async Task<string> ConvertAsync(
        string sourcePath, 
        string targetExtension, 
        string outputDir, 
        IProgress<int>? progress = null, 
        CancellationToken ct = default)
    {
        var cleanTarget = targetExtension.TrimStart('.').ToLowerInvariant();
        var srcExt = Path.GetExtension(sourcePath).TrimStart('.').ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var outputPath = Path.Combine(outputDir, $"{baseName}.{cleanTarget}");

        Directory.CreateDirectory(outputDir);
        progress?.Report(20);

        // Blender file conversion via Blender CLI
        if (srcExt == "blend" && _binaryLocator.IsBinaryAvailable("blender"))
        {
            var blenderExe = _binaryLocator.GetBinaryPathOrThrow("blender");
            var pythonScript = $"import bpy; bpy.ops.wm.open_mainfile(filepath='{sourcePath.Replace("\\", "/")}'); ";
            if (cleanTarget is "obj") pythonScript += $"bpy.ops.wm.obj_export(filepath='{outputPath.Replace("\\", "/")}');";
            else if (cleanTarget is "stl") pythonScript += $"bpy.ops.wm.stl_export(filepath='{outputPath.Replace("\\", "/")}');";
            else if (cleanTarget is "gltf" or "glb") pythonScript += $"bpy.ops.export_scene.gltf(filepath='{outputPath.Replace("\\", "/")}');";
            else if (cleanTarget is "fbx") pythonScript += $"bpy.ops.export_scene.fbx(filepath='{outputPath.Replace("\\", "/")}');";

            await CliWrap.Cli.Wrap(blenderExe)
                .WithArguments($"--background --python-expr \"{pythonScript}\"")
                .WithValidation(CommandResultValidation.ZeroExitCode)
                .ExecuteAsync(ct);

            progress?.Report(100);
            return outputPath;
        }

        // Standard 3D conversion via AssimpNet
        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var context = new AssimpContext();

            var scene = context.ImportFile(sourcePath, PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals);
            if (scene == null)
            {
                throw new InvalidOperationException($"Assimp failed to parse 3D scene from '{sourcePath}'.");
            }

            progress?.Report(60);

            var exportFormats = context.GetSupportedExportFormats();
            var matchedFormat = exportFormats.FirstOrDefault(f => 
                f.FileExtension.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
                f.FormatId.Contains(cleanTarget, StringComparison.OrdinalIgnoreCase));

            var formatId = matchedFormat?.FormatId ?? cleanTarget switch
            {
                "obj" => "obj",
                "stl" => "stl",
                "ply" => "ply",
                "gltf" => "gltf2",
                "glb" => "glb2",
                "dae" => "collada",
                "3ds" => "3ds",
                _ => cleanTarget
            };

            context.ExportFile(scene, outputPath, formatId);
        }, ct);

        progress?.Report(100);

        if (!File.Exists(outputPath))
        {
            throw new FileNotFoundException($"3D conversion did not generate expected file at '{outputPath}'.");
        }

        return outputPath;
    }
}
