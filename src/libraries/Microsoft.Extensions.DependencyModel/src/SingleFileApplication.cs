// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.IO.MemoryMappedFiles;

namespace Microsoft.Extensions.DependencyModel
{
    internal static class SingleFileApplication
    {
        private static byte[] bundleSignature =
        {
            // 32 bytes represent the bundle signature: SHA-256 for ".net core bundle"
            // The first byte is actually 0x8b but we don't want to accidentally have a second place where
            // the bundle signature can appear in the single file application so it's set in the static constructor
            // See https://github.com/dotnet/runtime/blob/v6.0.3/src/installer/managed/Microsoft.NET.HostModel/AppHost/HostWriter.cs#L216-L222
            0x00, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38, 0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
            0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18, 0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae
        };

        static SingleFileApplication()
        {
            bundleSignature[0] = 0x8b;
        }

        public static Stream? GetDepsJsonStream()
        {
#if NET6_0_OR_GREATER
            string? appHostPath = System.Environment.ProcessPath;
#else
            string appHostPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
#endif
            if (appHostPath == null)
            {
                return null;
            }

            using MemoryMappedFile appHostFile = MemoryMappedFile.CreateFromFile(appHostPath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using Stream appHostStream = appHostFile.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
            int bundleSignatureIndex = SearchBundleSignature(appHostStream);

            if (bundleSignatureIndex != -1)
            {
                using BinaryReader appHostReader = new BinaryReader(appHostStream);
                appHostReader.BaseStream.Position = bundleSignatureIndex - 8;
                long bundleHeaderOffset = appHostReader.ReadInt64();
                if (bundleHeaderOffset > 0 && bundleHeaderOffset < appHostStream.Length)
                {
                    // See https://github.com/dotnet/runtime/blob/v6.0.3/src/installer/managed/Microsoft.NET.HostModel/Bundle/Manifest.cs#L32-L39
                    // and https://github.com/dotnet/runtime/blob/v6.0.3/src/installer/managed/Microsoft.NET.HostModel/Bundle/Manifest.cs#L144-L155
                    appHostReader.BaseStream.Position = bundleHeaderOffset;
                    uint majorVersion = appHostReader.ReadUInt32();
                    _ = appHostReader.ReadUInt32(); // minorVersion
                    int numEmbeddedFiles = appHostReader.ReadInt32();
                    _ = appHostReader.ReadString(); // bundleId
                    if (majorVersion >= 2)
                    {
                        long depsJsonOffset = appHostReader.ReadInt64();
                        long depsJsonSize = appHostReader.ReadInt64();
                        return appHostFile.CreateViewStream(depsJsonOffset, depsJsonSize, MemoryMappedFileAccess.Read);
                    }

                    // For version < 2 all the file entries must be enumerated until the `DepsJson` type is found
                    // See https://github.com/dotnet/runtime/blob/v6.0.3/src/installer/managed/Microsoft.NET.HostModel/Bundle/FileEntry.cs#L43-L54
                    for (int i = 0; i < numEmbeddedFiles; i++)
                    {
                        long offset = appHostReader.ReadInt64();
                        long size = appHostReader.ReadInt64();
                        byte type = appHostReader.ReadByte();
                        _ = appHostReader.ReadString(); // relativePath
                        if (type == 3)
                        {
                            // type 3 is the .deps.json configuration file
                            // See https://github.com/dotnet/runtime/blob/v6.0.3/src/installer/managed/Microsoft.NET.HostModel/Bundle/FileType.cs#L17
                            return appHostFile.CreateViewStream(offset, size, MemoryMappedFileAccess.Read);
                        }
                    }
                }
            }

            return null;
        }

        private static int SearchBundleSignature(Stream stream)
        {
            int m = 0;
            int i = 0;

            while (m + i < stream.Length)
            {
                stream.Position = m + i;
                if (bundleSignature[i] == stream.ReadByte())
                {
                    if (i == bundleSignature.Length - 1)
                    {
                        return m;
                    }
                    i++;
                }
                else
                {
                    m += i == 0 ? 1 : i;
                    i = 0;
                }
            }

            return -1;
        }
    }
}
