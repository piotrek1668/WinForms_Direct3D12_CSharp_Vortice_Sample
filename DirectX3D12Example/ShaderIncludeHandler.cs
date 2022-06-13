// Copyright (c) Amer Koleci and contributors.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using SharpGen.Runtime;
using System.Runtime.InteropServices;
using Vortice.Dxc;

namespace DirectX3D12Example
{
    internal class ShaderIncludeHandler : CallbackBase, IDxcIncludeHandler
    {
        private readonly string[] includeDirectories;
        private readonly Dictionary<string, SourceCodeBlob> sourceFiles = new();

        public ShaderIncludeHandler(params string[] includeDirectories)
        {
            this.includeDirectories = includeDirectories;
        }

        public Result LoadSource(string fileName, out IDxcBlob? includeSource)
        {
            if (fileName.StartsWith("./"))
            {
                fileName = fileName[2..];
            }

            var includeFile = GetFilePath(fileName);

            if (string.IsNullOrEmpty(includeFile))
            {
                includeSource = default;

                return Result.Fail;
            }

            if (!this.sourceFiles.TryGetValue(includeFile, out SourceCodeBlob? sourceCodeBlob))
            {
                byte[] data = NewMethod(includeFile);

                sourceCodeBlob = new SourceCodeBlob(data);
                this.sourceFiles.Add(includeFile, sourceCodeBlob);
            }

            includeSource = sourceCodeBlob.Blob;

            return Result.Ok;
        }

        private static byte[] NewMethod(string includeFile) => File.ReadAllBytes(includeFile);

        private string? GetFilePath(string fileName)
        {
            foreach (var directory in this.includeDirectories)
            {
                var filePath = Path.GetFullPath(Path.Combine(directory, fileName));

                if (File.Exists(filePath))
                {
                    return filePath;
                }
            }

            return null;
        }

        private class SourceCodeBlob : IDisposable
        {
            private GCHandle dataPointer;
            private IDxcBlobEncoding? blob;

            internal IDxcBlob? Blob => this.blob;

            public SourceCodeBlob(byte[] data)
            {
                this.dataPointer = GCHandle.Alloc(data, GCHandleType.Pinned);

                this.blob = DxcCompiler.Utils.CreateBlob(this.dataPointer.AddrOfPinnedObject(), data.Length, Dxc.DXC_CP_UTF8);
            }

            public void Dispose()
            {
                this.blob = null;

                if (this.dataPointer.IsAllocated)
                {
                    this.dataPointer.Free();
                }

                this.dataPointer = default;
            }
        }
    }
}