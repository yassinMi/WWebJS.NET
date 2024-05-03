using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WWebJS.NET;

public static class WWebJSHelper
{
    /// <summary>
    /// Specify the full path to the directory at which the node dependencies will be installed, queried, and updated by the helper methods in this class, e.g "C:\MyAppModules\wds\"
    /// </summary>
    public static string? WdsParentProjectDirectory { get; set; }
    /// <summary>
    /// Specify the full path to e.g. "path\to\npm.cmd" which is required to perform installs and updates, if npm is installed globally, then see a <see cref="UseGlobalNpm"/> 
    /// </summary>
    public static string? NpmPath { get; set; }
    /// <summary>
    /// Set this to true if npm is installed on the system and can be used, in which case <see cref="NpmPath"/> is not required and may be null.
    /// </summary>
    public static bool UseGlobalNpm { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether to set the PUPPETEER_SKIP_CHROMIUM_DOWNLOAD envirenment during installing, set to true (default) if you provide your own chromium binaries, or set to false if you wish to have chromium downloaded during the node dependecies installation. (NOTE: this library doesn't automatically use the installed chrome, you still need to specify its full path during configuration)
    /// </summary>
    public static bool SkipChromeDownload { get; set; } = true;
    static bool isInstalling;
    /// <summary>
    /// Install the wwebjs-dotnet-server as a dependency at the specified parent project 
    /// directory, throws an exception if the installation failed.
    /// returns true if the wwebjs-dotnet-server package was added, and false if it already exists
    /// </summary>
    /// <param name="createParentProject">true to create the parent project if it's missing. if false is passed and the parent project is not present (i.e. the package.json) an exception will be thrown</param>
    /// <returns></returns>
    public static async Task<bool> Install(bool createParentProject)
    {
        lock (lock_)
        {
            if (isInstalling) throw new Exception("cannot run install tasks concurrently");
            isInstalling = true;
        }
        try
        {
            var WdsParentProjectDirectory_ = WdsParentProjectDirectory;//capturing value in case it changed during install
            if (string.IsNullOrWhiteSpace(WdsParentProjectDirectory_)) throw new InvalidOperationException($"value unset, make sure the static property {nameof(WWebJSHelper)}.{nameof(WdsParentProjectDirectory)} is set properly");
            if (!UseGlobalNpm && string.IsNullOrWhiteSpace(NpmPath)) throw new InvalidOperationException($"value unset, make sure the static property {nameof(WWebJSHelper)}.{nameof(NpmPath)} is set properly");
            if (!UseGlobalNpm && !File.Exists(NpmPath)) throw new Exception($"cannot find npm path'{NpmPath}', please make sure the static property {nameof(WWebJSHelper)}.{nameof(NpmPath)} is set properly");
            string parentProjectPackageJson = Path.Combine(WdsParentProjectDirectory_, "package.json");
            if (createParentProject)
            {
                if (!File.Exists(parentProjectPackageJson))
                {
                    //# creating the parent project (todo: we don't have to use npm here)
                    Directory.CreateDirectory(WdsParentProjectDirectory_);
                    var npmInitRes = await new CliTask()
                         .FromCommand(UseGlobalNpm ? "npm" : $"\"{NpmPath}\"")
                         .WithArgs("init -y --json")
                         .WithWorkingDirectory(WdsParentProjectDirectory_)
                         .GetOutputString().ConfigureAwait(false);

                    //assert package.json is created
                    if (!File.Exists(parentProjectPackageJson)) throw new Exception("install failed, package.json not created");
                    //just another assetion
                    var parsed = ReadFirstJsonInString(npmInitRes.Trim());

                    //altering the parent package.json to include wwebjs-dotnet-server as a dependency (wihout install)
                    var packageJson = JObject.Parse(File.ReadAllText(parentProjectPackageJson));
                    packageJson["dependencies"] = new JObject { { PackageName, PackageInitialVersion } };
                    File.WriteAllText(parentProjectPackageJson, packageJson.ToString(Newtonsoft.Json.Formatting.Indented));
                }
            }
            if (!File.Exists(parentProjectPackageJson))
            {
                throw new Exception($"cannot find parent project's package.json at '{WdsParentProjectDirectory_}', please make sure the static property {nameof(WWebJSHelper)}.{nameof(WdsParentProjectDirectory)} is set properly to an existing project, or call Install with createParentProject = true");
            }
            var res = await new CliTask()
               .FromCommand(UseGlobalNpm ? "npm" : $"\"{NpmPath}\"")
               .WithEnvirenment("PUPPETEER_SKIP_CHROMIUM_DOWNLOAD", "true")
               .WithWorkingDirectory(WdsParentProjectDirectory_)
               .WithArgs("install --json")
               .GetOutputString().ConfigureAwait(false);
            ;

            var obj = ReadFirstJsonInString(res);
            JArray addedCollection = (JArray)obj["added"]!;
            bool existed = true;
            if (addedCollection.Any(t => (string)t["name"]! == PackageName)) existed = false;
            InvalidateInstalledVersion();
            return !existed;
        }
        finally
        {
            isInstalling = false;
        }
    }

    private static string? InstalledVersion_;
    /// <summary>
    /// Gets the version of wwebjs-dotnet-server package or null if it's not installed on disk at the <see cref="WdsParentProjectDirectory"/> location. NOTE: this value is cached, you can trigger requerying by calling <see cref="InvalidateInstalledVersion"/>
    /// </summary>
    public static string? InstalledVersion
    {
        get
        {
            if (InstalledVersion_ == null)
            {
                InstalledVersion_ = ReadInstalledVersion();
            }
            return InstalledVersion_;
        }
    }
    /// <summary>
    /// Defaults to 60 seconds
    /// </summary>
    public static int CheckUpdateTimeout { get; set; } = 60000;

    const string PackageName = "wwebjs-dotnet-server";
    const string PackageInitialVersion = "^0.1.0";//hardcoded, used for initial creation of the node parent project
                                                  /// <summary>
                                                  /// retunss null if the package.json files does't exist, throws an exception if something goes wrong, returns the version from the package.json file
                                                  /// </summary>
                                                  /// <returns></returns>
    private static string? ReadInstalledVersion()
    {
        //read the version from the node package
        if (string.IsNullOrWhiteSpace(WdsParentProjectDirectory)) throw new InvalidOperationException($"value unset, make sure the static property {nameof(WWebJSHelper)}.{nameof(WdsParentProjectDirectory)} is set properly before accessing {nameof(InstalledVersion)}");

        string indexFile = Path.Combine(WdsParentProjectDirectory, $@"node_modules\{PackageName}\package.json");
        if (!File.Exists(indexFile)) return null;
        try
        {
            var parsed = JObject.Parse(File.ReadAllText(indexFile));
            var appName = (string?)parsed.SelectToken("name");
            if (appName != PackageName)
            {
                throw new Exception($"read version: unexpected package name '{appName}'");
            }
            return (string?)parsed.SelectToken("version");

        }
        catch (Exception)
        {
            throw;
        }
    }
    /// <summary>
    /// Fired when <see cref="InstalledVersion"/> is updated, which may occure when expecitely calling <see cref="InvalidateInstalledVersion"/> or during other operations
    /// </summary>
    public static event EventHandler? InstalledVersionChanged;
    /// <summary>
    /// Force updating <see cref="InstalledVersion"/> based on the on-disk state of the package
    /// </summary>
    public static void InvalidateInstalledVersion()
    {
        InstalledVersion_ = ReadInstalledVersion();
        InstalledVersionChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Returns the same version as <see cref="InstalledVersion"/> if no updates are available, throws exceptions if the cheking fails, returns the latest suggested version otherwise
    /// NOTE: this will throw an exception if the package is not installed
    /// </summary>
    /// <returns></returns>
    public static async Task<string> CheckUpdate()
    {
        if (isInstalling) throw new Exception("cannot chekc updates while installing");
        if (string.IsNullOrWhiteSpace(WdsParentProjectDirectory)) throw new InvalidOperationException($"value unset, make sure the static property {nameof(WWebJSHelper)}.{nameof(WdsParentProjectDirectory)} is set properly");
        if (!UseGlobalNpm && string.IsNullOrWhiteSpace(NpmPath)) throw new InvalidOperationException($"value unset, make sure the static property {nameof(WWebJSHelper)}.{nameof(NpmPath)} is set properly");
        if (!UseGlobalNpm && !File.Exists(NpmPath)) throw new Exception($"cannot find npm path'{NpmPath}', please make sure the static property {nameof(WWebJSHelper)}.{nameof(NpmPath)} is set properly");

        InvalidateInstalledVersion();

        if (InstalledVersion == null) throw new Exception("no current installed version, call Install first");
        var npmOutdatedDump = await new CliTask()
            .FromCommand(UseGlobalNpm ? "npm" : $"\"{NpmPath}\"")
            .WithWorkingDirectory(WdsParentProjectDirectory)
            .WithArgs("outdated")
            .WithArgs(PackageName)
            .WithArgs("--json")
            .WithTimeout(CheckUpdateTimeout)
            .GetOutputString(false).ConfigureAwait(false);

        if (npmOutdatedDump.Trim() == "{}")
        {
            //npm outputs empty object if no updates are available
            return InstalledVersion;
        }
        else
        {
            var parsed = JObject.Parse(npmOutdatedDump.Trim());
            var package = parsed.SelectToken(PackageName);
            if (package != null)
            {
                var wanted = (string)package["wanted"]!;
                var current = (string)package["current"]!;
                var latest = (string)package["latest"]!;
                var latest_ = (latest);
                return latest_;//normally this package is designed to keep backward compatibility with any version of WWebJS.NET and we don't intend to release major 2.x.x, the above message is there just in case 

            }
            else
            {
                return InstalledVersion;
            }
        }

    }
    static object lock_ = new object();
    /// <summary>
    /// Install a specific version of wwebjs-dotnet-server, if <see cref="InstalledVersion"/> is null then use <see cref="Install(bool)"/> instead
    /// </summary>
    /// <param name="version">must pass the latest version as returned by <see cref="CheckUpdate"/>, otherwise an exception will be thrown</param>
    /// <returns></returns>
    public static async Task InstallUpdate(string version)
    {
        lock (lock_)
        {
            if (isInstalling) throw new Exception("cannot run install tasks concurrently");
            isInstalling = true;
        }
        try
        {
            if (string.IsNullOrWhiteSpace(WdsParentProjectDirectory)) throw new InvalidOperationException($"value unset, make sure the static property {nameof(WWebJSHelper)}.{nameof(WdsParentProjectDirectory)} is set properly");
            if (!UseGlobalNpm && string.IsNullOrWhiteSpace(NpmPath)) throw new InvalidOperationException($"value unset, make sure the static property {nameof(WWebJSHelper)}.{nameof(NpmPath)} is set properly");
            if (!UseGlobalNpm && !File.Exists(NpmPath)) throw new Exception($"cannot find npm path'{NpmPath}', please make sure the static property {nameof(WWebJSHelper)}.{nameof(NpmPath)} is set properly");

            if (string.IsNullOrWhiteSpace(version)) throw new ArgumentNullException("version");
            var cmdDump = await new CliTask()
                .FromCommand(UseGlobalNpm ? "npm" : $"\"{NpmPath}\"")
                .WithArgs($"install {PackageName}@{version.ToString()}")
                .WithArgs($"--json")
                .WithWorkingDirectory(WdsParentProjectDirectory)
                .WithEnvirenment("PUPPETEER_SKIP_CHROMIUM_DOWNLOAD", "true")
                .GetOutputString().ConfigureAwait(false);

            var obj = ReadFirstJsonInString(cmdDump);
            var updatedElements = obj.SelectTokens("$.updated[*]");
            var updatedPkg = updatedElements.FirstOrDefault(t => (string)t["name"]! == PackageName && (string)t["action"]! == "update");
            if (updatedPkg == null)
            {
                throw new Exception($"target package {PackageName} not updated");
            }
            if ((string)updatedPkg["version"]! != version)
            {
                throw new Exception($"expected version {version}, actual {(string)updatedPkg["version"]!}");
            }
            InstalledVersion_ = version;
        }
        finally
        {
            isInstalling = false;
        }
    }

    static JObject ReadFirstJsonInString(string data)
    {
        var indexOfJson = data.IndexOf('{');
        if (indexOfJson < 0) throw new Exception("no json output");
        using (var sr = new StringReader(data))
        {
            using (var jtr = new Newtonsoft.Json.JsonTextReader(new StringReader(data.Substring(indexOfJson))))
            {
                var obj = JObject.ReadFrom(jtr);
                if (obj == null) throw new Exception("cannot read output json");
                return (JObject)obj;
            }
        }
    }

    class CliTask
    {
        public class CliTaskResult
        {
            public CliTaskResult(string output, int exitCode)
            {
                Ouput=output;   
                ExitCode=exitCode;   
            }
            public string Ouput { get; private set; }
            public int ExitCode { get; private set; }
        }
        /// <summary>
        /// an existing file name must be passed (or executable)
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public CliTask FromFileName(string fileName)
        {
            if (this.Command != null) throw new InvalidOperationException("a file name is already specified");
            this.FileName = fileName;
            return this;
        }
        string? Command;
        /// <summary>
        /// uses cmd.exe /c
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public CliTask FromCommand(string command)
        {
            if (this.FileName != null) throw new InvalidOperationException("a command is already specified");
            this.Command = command;
            return this;
        }
        public CliTask WithTimeout(int milliseconds)
        {
            this.Timeout = milliseconds;
            return this;
        }
        public CliTask WithEnvirenment(string key, string value)
        {
            this.Env.Add(key, value);
            return this;
        }
        Dictionary<string, string> Env = new Dictionary<string, string>();
        List<string> Args { get; set; } = new List<string>();
        public string? FileName { get; private set; }
        public int Timeout { get; private set; } = -1;
        public string? WorkingDirectory { get; private set; }

        public CliTask WithArgs(string args)
        {
            this.Args.Add(args);
            return this;
        }



        public Task<string> GetOutputString(bool throwOnNonZeroExit = false)
        {
            return Task.Run(() =>
            {

                Process p = new Process();
                var filename = this.FileName ?? "cmd.exe";
                var args = this.FileName != null ? string.Join(" ", Args) : "/c " + Command + " " + string.Join(" ", Args);
                var si = new ProcessStartInfo(filename, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = WorkingDirectory,
                };
                foreach (var kp in Env)
                {
                    si.Environment.Add(kp.Key, kp.Value);
                }
                p.StartInfo = si;
                StringBuilder sb = new StringBuilder();
                DataReceivedEventHandler h_OutputDataReceived = (s, e) =>
                {
                    sb.AppendLine(e.Data);
                };
                DataReceivedEventHandler h_OutputErrorReceived = (s, e) =>
                {
                    sb.AppendLine(e.Data);

                };
                p.OutputDataReceived += h_OutputDataReceived;
                p.ErrorDataReceived += h_OutputDataReceived;

                p.Start();
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
                var timedOut = false;
                if (Timeout == -1)
                    p.WaitForExit();
                else
                    timedOut = !p.WaitForExit(this.Timeout);
                if (throwOnNonZeroExit)
                {
                    if (p.ExitCode != 0)
                        throw new Exception($"process exited with code: {p.ExitCode}");
                }
                if (timedOut) throw new TimeoutException($"cliTask: the process timed out ({Timeout}ms)");
                return sb.ToString();
            });

        }

        internal CliTask WithWorkingDirectory(string v)
        {
            this.WorkingDirectory = v;
            return this;
        }
    }

}
