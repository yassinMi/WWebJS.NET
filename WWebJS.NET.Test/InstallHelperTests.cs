using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace WWebJS.NET.Test
{
    [TestClass]
    public class InstallHelperTests
    {
        static string localNodePath = @"F:\TOOLS\WWebJS.NET\WWebJS.NET.Test\tests.yass\localNode\node.exe";
        static string localNpmPath = @"F:\TOOLS\WWebJS.NET\WWebJS.NET.Test\tests.yass\localNode\npm.cmd";

        [TestMethod]
        public void InstallWorksWhenFreshInstall()
        {
            //# set up fresh install location
            string wdsDir_1 = @"F:\TOOLS\WWebJS.NET\WWebJS.NET.Test\tests.yass\installLoc_1";
            try
            {
                Directory.Delete(wdsDir_1, true);
            }
            catch (Exception){}
            WWebJSHelper.WdsParentProjectDirectory = wdsDir_1;
            WWebJSHelper.NpmPath = localNpmPath;
            //# execute install
            var installed =  WWebJSHelper.Install(true).GetAwaiter().GetResult();
            Assert.IsTrue(installed, "expected inatall to return true on fresh install");
        }

        [TestMethod]
        public void InstallWorksWhenInstallationExists()
        {
            //this test requires running InstallWorksWhenFreshInstall first
            //# set up fresh install location
            string wdsDir_1 = @"F:\TOOLS\WWebJS.NET\WWebJS.NET.Test\tests.yass\installLoc_1";
            
            WWebJSHelper.WdsParentProjectDirectory = wdsDir_1;
            WWebJSHelper.NpmPath = localNpmPath;
            //# execute install
            var installed = WWebJSHelper.Install(true).GetAwaiter().GetResult();
            Assert.IsTrue(installed==false, "expected inatall to return false, ensure the package is installed");
        }
    }
}