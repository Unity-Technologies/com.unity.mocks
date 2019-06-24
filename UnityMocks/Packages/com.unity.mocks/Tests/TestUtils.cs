using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using Unity.Coding.Utils;

namespace Unity.Coding.Tests
{
    public static class ShouldlyExtensions
    {
        public static void ShouldBe(this IEnumerable<string> actual, params string[] expected)
        {
            actual.ShouldBe<IEnumerable<string>>(expected);
        }
    }

    public struct DirectoryBackup : IDisposable
    {
        public DirectoryBackup(string folderPath)
        {
            m_BackupPath = Path.GetTempPath().ToNPath().Combine(Process.GetCurrentProcess().Id.ToString());
            m_FullPath = folderPath.ToNPath().MakeAbsolute();

            Directory.CreateDirectory(m_BackupPath);
            m_FullPath.CopyFiles(m_BackupPath, true);
        }

        public void Dispose()
        {
            m_BackupPath.CopyFiles(m_FullPath, true);
            m_BackupPath.Delete();
        }

        NPath m_BackupPath;
        NPath m_FullPath;
    }

    public abstract class TestFileSystemFixture
    {
        protected NPath BaseDir { private set; get; }
        protected NPath PackageDir { private set; get; }
        protected string Eol { set; get; } = "\n";
        protected string TestDirectory { set; get; } = TestContext.CurrentContext.TestDirectory;

        [OneTimeSetUp]
        public void InitFixture()
        {
            BaseDir = TestDirectory.ToNPath().Combine("testfs");

            // assume that we are running in the Unity Editor, where the working directory is always the project root
            PackageDir = "Packages/com.unity.coding".ToNPath();

            // if doesn't exist, must be running pure test outside unity. try to find project root.
            if (!PackageDir.DirectoryExists())
            {
                Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
                while (!PackageDir.DirectoryExists())
                    Environment.CurrentDirectory = NPath.CurrentDirectory.Parent;
            }
        }

        [OneTimeTearDown]
        public void TearDownFixture() => DeleteTestFileSystem();

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

        // Allow tests that relies on RecompileScripts() / WaitForDomainReload()
        // to workaround issue in which Setup is called after each `yield`
        protected virtual bool ManualCleanup { get => false; }

        protected NPath WriteAllLines(NPath path, params string[] lines)
            => WriteAllLines(path, lines.AsEnumerable());

        protected NPath WriteAllLines(NPath path, IEnumerable<string> lines)
            => path.WriteAllText(lines.Append("").StringJoin(Eol));

        protected void WriteRootEditorConfig(params string[] configLines)
            => WriteRootEditorConfig(configLines.AsEnumerable());

        protected void WriteRootEditorConfig(IEnumerable<string> configLines)
        {
            WriteAllLines(
                BaseDir.Combine(".editorconfig"),
                configLines.Prepend("root=true"));
        }
    }
}
