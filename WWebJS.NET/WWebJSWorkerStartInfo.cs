public struct WWebJSWorkerStartInfo
{

    static string GetEnvSpecificRuntimeBinaryPath(string fileNameAndExtension)
    {
        string folder = "x86";
        if (Environment.Is64BitProcess) folder = "x64";
        return @$"{folder}\{fileNameAndExtension}";
    }
    public static WWebJSWorkerStartInfo LocalEnvSpecificPackaged = new WWebJSWorkerStartInfo(GetEnvSpecificRuntimeBinaryPath("wwebjs-dotnet-server.exe"));
    public static WWebJSWorkerStartInfo LocalEnvSpecificNode = new WWebJSWorkerStartInfo(GetEnvSpecificRuntimeBinaryPath("node.exe"),"wwebjs-dotnet-server/");
    ///<summary>
    /// the index.js entry file relative to the <see cref="WorkerStartInfo.NodeAppDirectory"/> , e.g "dist/index.js"
    ///</summary>
    public static string RelativeEntryPointFile { get; set; } = "dist/index.js";
    public WWebJSWorkerStartInfo(string packagedExe)
    {
        this.PackagedExecutablePath= packagedExe;
        this.NodeAppDirectory=null;
        this.NodeExecutablePath=null;
        CreateNoWindow = true;
    }
     public WWebJSWorkerStartInfo(string nodeExe, string nodePackageDir)
    {
        this.PackagedExecutablePath= null;
        this.NodeAppDirectory=nodePackageDir;
        this.NodeExecutablePath=nodeExe;
        CreateNoWindow = true;
    }
    ///<summary>
    /// the node.exe path <inheritdoc case we don't use a packaged executable
    ///</summary>
    public string? NodeExecutablePath { get; set; }
    ///<summary>
    /// the wweb-js-server.exe path (packaged with pkg or else), it is assumed that this doesn'<see langword="true"/> depend on node installation or <see langword="async"/> other files
    ///</summary>
    public string? PackagedExecutablePath { get; set; }
    ///<summary>
    ///the folder containing the entry point (index.js) file of the wweb-js-server 
    /// NOTE: this folder is used in case we're using node.exe, and it must contain all the source files as well as the node_module dependencies tree
    ///</summary>
    public string? NodeAppDirectory { get; set; }
    public bool CreateNoWindow { get; set; }

    public void ValidateCanStartWithPackagedExecutable (bool checkFiles)
    {
        if (string.IsNullOrWhiteSpace(PackagedExecutablePath))
        {
            throw new Exception("PackagedExecutablePath must be provided");
        }
        if (checkFiles)
        {
            if (!File.Exists(this.PackagedExecutablePath)) throw new Exception($"packaged executable not found: '{this.PackagedExecutablePath}'");
        }
    }
    public void ValidateCanStartWithNode(bool checkFiles)
    {
        if (string.IsNullOrWhiteSpace(NodeExecutablePath))
        {
            throw new Exception("NodeExecutablePath must be provided");
        }
        if (string.IsNullOrWhiteSpace(NodeAppDirectory))
        {
            throw new Exception("NodeAppDirectory must be provided");
        }
        if (checkFiles)
        {
            if (!File.Exists(this.NodeExecutablePath)) throw new Exception($"node.exe not found: '{this.NodeExecutablePath}'");
            if (!Path.GetFileName(this.NodeExecutablePath).Equals("node.exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"invalid node path: '{this.NodeExecutablePath}'");
            }
            if (!Directory.Exists(this.NodeAppDirectory))
            {
                throw new Exception($"wds app directory not found or is not valid at : '{this.NodeAppDirectory}'");
            }
            if (!Directory.Exists(Path.Combine(this.NodeAppDirectory, "node_modules")))
            {
                throw new Exception($"wds app directory not found or is not valid at : '{this.NodeAppDirectory}'");
            }
            var indexJsPath = Path.Combine(this.NodeAppDirectory, RelativeEntryPointFile);
            if (!File.Exists(indexJsPath))
            {
                throw new Exception($"{this} expected in app directory : '{this.NodeAppDirectory}'");
            }
        }
    }
}