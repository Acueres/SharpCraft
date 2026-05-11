using System.Diagnostics;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: ShaderCompiler <projectDir> <targetDir>");
    return 1;
}

string projectDir = Path.GetFullPath(args[0]);
string targetDir = Path.GetFullPath(args[1]);

string shaderSourceDir = Path.Combine(projectDir, "Shaders");
string shaderOutputDir = Path.Combine(targetDir, "Shaders");

Directory.CreateDirectory(shaderOutputDir);

CompileShader(
    source: Path.Combine(shaderSourceDir, "cube.hlsl"),
    entryPoint: "MainVS",
    targetProfile: "vs_6_0",
    output: Path.Combine(shaderOutputDir, "cube.vert.spv")
);

CompileShader(
    source: Path.Combine(shaderSourceDir, "cube.hlsl"),
    entryPoint: "MainFS",
    targetProfile: "ps_6_0",
    output: Path.Combine(shaderOutputDir, "cube.frag.spv")
);

return 0;

static void CompileShader(
    string source,
    string entryPoint,
    string targetProfile,
    string output)
{
    if (!File.Exists(source))
        throw new FileNotFoundException($"Shader source not found: {source}");

    if (IsUpToDate(source, output))
    {
        Console.WriteLine($"Shader up to date: {Path.GetFileName(output)}");
        return;
    }

    Console.WriteLine(
        $"Compiling shader: {Path.GetFileName(source)}:{entryPoint} -> {Path.GetFileName(output)}"
    );

    Directory.CreateDirectory(Path.GetDirectoryName(output)!);

    using Process process = new();

    process.StartInfo = new ProcessStartInfo
    {
        FileName = "dxc",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    process.StartInfo.ArgumentList.Add("-spirv");
    process.StartInfo.ArgumentList.Add("-T");
    process.StartInfo.ArgumentList.Add(targetProfile);
    process.StartInfo.ArgumentList.Add("-E");
    process.StartInfo.ArgumentList.Add(entryPoint);
    process.StartInfo.ArgumentList.Add(source);
    process.StartInfo.ArgumentList.Add("-Fo");
    process.StartInfo.ArgumentList.Add(output);

    process.Start();

    string stdout = process.StandardOutput.ReadToEnd();
    string stderr = process.StandardError.ReadToEnd();

    process.WaitForExit();

    if (!string.IsNullOrWhiteSpace(stdout))
        Console.WriteLine(stdout);

    if (process.ExitCode != 0)
    {
        if (!string.IsNullOrWhiteSpace(stderr))
            Console.Error.WriteLine(stderr);

        throw new InvalidOperationException(
            $"DXC failed for shader '{source}', entry '{entryPoint}'. Exit code: {process.ExitCode}"
        );
    }

    if (!string.IsNullOrWhiteSpace(stderr))
        Console.WriteLine(stderr);
}

static bool IsUpToDate(string source, string output)
{
    if (!File.Exists(output))
        return false;

    return File.GetLastWriteTimeUtc(output) >= File.GetLastWriteTimeUtc(source);
}