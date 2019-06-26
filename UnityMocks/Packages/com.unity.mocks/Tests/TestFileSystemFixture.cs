using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Utils;

namespace Unity.Mocks.Tests
{
    /// <summary>
    /// This fixture maintains a sandbox "file system" that is automatically created before each test, and nuked after
    /// each test. Use `BaseDir` to access the root of this file system.
    /// </summary>
    public abstract class TestFileSystemFixture
    {
        protected NPath BaseDir { private set; get; }

        [OneTimeSetUp]
        public void OneTimeSetUpTestFileSystem()
        {
            var testBaseDir = TestContext.CurrentContext.TestDirectory.ToNPath();
            BaseDir = testBaseDir.Combine($"testfs_{TestContext.CurrentContext.GetFixtureName()}");

            // TODO: detect this a better way
            var packageDir = "Packages/com.unity.mocks".ToNPath(); 
            
            #if UNITY_EDITOR

            // assume that we are running in the Unity Editor, where the working directory is always the project root
            packageDir.DirectoryMustExist();

            #else
            
            // if doesn't exist, must be running a pure test outside unity. try to find project root so our CWD can
            // match what unity's would be (unity guarantees cwd always set to project root).
            if (!PackageDir.DirectoryExists())
            {
                Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
                while (!PackageDir.DirectoryExists())
                    Environment.CurrentDirectory = NPath.CurrentDirectory.Parent;
            }

            #endif
        }

        [OneTimeTearDown]
        public void OneTimeTearDownTestFileSystem() => DeleteTestFileSystem();

        [SetUp]
        public void InitTest()
        {
            if (!BaseDir.Exists())
                BaseDir.CreateDirectory();
        }

        [TearDown]
        public void CleanupTest() => DeleteTestFileSystem();
        
        protected void DeleteTestFileSystem(bool force = false) 
        {
            if (ManualCleanup && !force)
                return;

            if (!BaseDir.Exists())
                return;

            // TODO: add support for handling readonly files/dirs to NiceIO

            foreach (var path in BaseDir
                .Contents(true)
                .Where(f => (File.GetAttributes(f) & FileAttributes.ReadOnly) != 0))
            {
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            }

            BaseDir.Delete();
        }

        // allow tests that rely on RecompileScripts() / WaitForDomainReload()
        // to work around issue in which Setup is called after each `yield`
        protected virtual bool ManualCleanup => false;
    }
}
