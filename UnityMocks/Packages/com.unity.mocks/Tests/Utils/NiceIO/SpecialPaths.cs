using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Coding.Utils;

namespace NiceIO.Tests
{
	[TestFixture]
	public class SpecialPaths
	{
		[Test]
		public void CurrentDirectory()
		{
			Assert.IsTrue(NPath.CurrentDirectory.DirectoryExists());
		}

		[Test]
		public void HomeDirectory()
		{
			Assert.IsTrue(NPath.HomeDirectory.DirectoryExists());
		}

		[Test]
		public void SystemTempDirectory()
		{
			Assert.IsTrue(NPath.SystemTemp.DirectoryExists());
		}
	}
}
