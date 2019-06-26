using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using Unity.Utils;

namespace Unity.Mocks.Tests
{
    public static class ShouldlyExtensions
    {
        public static void ShouldBe(this IEnumerable<string> actual, params string[] expected)
        {
            actual.ShouldBe<IEnumerable<string>>(expected);
        }
    }

    public static class TestAdapterExtensions
    {
        public static string GetFixtureName(this TestContext @this)
            => @this.Test.ClassName.Split('.').Last();
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
}