using System;
using System.Collections;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Mocks.Tests;
using Unity.Utils;

namespace Utils
{
    class NativeTests : TestFileSystemFixture
    {
        [Test]
        public void Set_Permission_Works([ValueSource(nameof(PermissionScenarios))] NativeUnix.UnixFilePermissions permission)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            var path = BaseDir.CreateFile("normal.txt");
            Assert.That(NativeUnix.SetFileMode(path, NativeUnix.UnixFilePermissions.None), Is.EqualTo(0));

            NativeUnix.UnixFilePermissions actual;
            Assert.That(NativeUnix.GetFileMode(path, out actual), Is.EqualTo(0));
            Assert.That(actual, Is.EqualTo(NativeUnix.UnixFilePermissions.None));

            Assert.That(NativeUnix.SetFileMode(path, permission), Is.EqualTo(0));

            Assert.That(NativeUnix.GetFileMode(path, out actual), Is.EqualTo(0));
            Assert.That(actual, Is.EqualTo(permission));
        }

        public static IEnumerable PermissionScenarios()
        {
            foreach(var v in Enum.GetValues(typeof(NativeUnix.UnixFilePermissions)))
                yield return (NativeUnix.UnixFilePermissions) v;

            yield return NativeUnix.UnixFilePermissions.S_IRGRP | NativeUnix.UnixFilePermissions.S_IROTH | NativeUnix.UnixFilePermissions.S_IRUSR;
            yield return NativeUnix.UnixFilePermissions.S_IRGRP | NativeUnix.UnixFilePermissions.S_IWGRP | NativeUnix.UnixFilePermissions.S_IXGRP;
            yield return
                NativeUnix.UnixFilePermissions.S_IRUSR |
                NativeUnix.UnixFilePermissions.S_IWUSR |
                NativeUnix.UnixFilePermissions.S_IXUSR |

                NativeUnix.UnixFilePermissions.S_IRGRP |
                NativeUnix.UnixFilePermissions.S_IWGRP |
                NativeUnix.UnixFilePermissions.S_IXGRP |

                NativeUnix.UnixFilePermissions.S_IROTH |
                NativeUnix.UnixFilePermissions.S_IWOTH |
                NativeUnix.UnixFilePermissions.S_IXOTH;
        }
    }
}
