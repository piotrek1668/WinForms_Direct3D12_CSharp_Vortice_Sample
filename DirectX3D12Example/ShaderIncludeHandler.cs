﻿// Copyright (c) Amer Koleci and contributors.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using SharpGen.Runtime;
using System.Runtime.InteropServices;
using Vortice.Dxc;

namespace DirectX3D12Example
{
    internal class ShaderIncludeHandler : CallbackBase, IDxcIncludeHandler
    {
        private readonly string[] _includeDirectories;
        private readonly Dictionary<string, SourceCodeBlob> _sourceFiles = new();

        public ShaderIncludeHandler(params string[] includeDirectories)
        {
            _includeDirectories = includeDirectories;
        }

        public Result LoadSource(string fileName, out IDxcBlob? includeSource)
        {
            if (fileName.StartsWith("./"))
                fileName = fileName[2..];

            var includeFile = GetFilePath(fileName);

            if (string.IsNullOrEmpty(includeFile))
            {
                includeSource = default;

                return Result.Fail;
            }

            if (!_sourceFiles.TryGetValue(includeFile, out SourceCodeBlob? sourceCodeBlob))
            {
                byte[] data = NewMethod(includeFile);

                sourceCodeBlob = new SourceCodeBlob(data);
                _sourceFiles.Add(includeFile, sourceCodeBlob);
            }

            includeSource = sourceCodeBlob.Blob;

            return Result.Ok;
        }

        private static byte[] NewMethod(string includeFile) => File.ReadAllBytes(includeFile);

        private string? GetFilePath(string fileName)
        {
            for (int i = 0; i < _includeDirectories.Length; i++)
            {
                var filePath = Path.GetFullPath(Path.Combine(_includeDirectories[i], fileName));

                if (File.Exists(filePath))
                    return filePath;
            }

            return null;
        }


        private class SourceCodeBlob : IDisposable
        {
            private GCHandle _dataPointer;
            private IDxcBlobEncoding? _blob;

            internal IDxcBlob? Blob { get => _blob; }

            public SourceCodeBlob(byte[] data)
            {
                _dataPointer = GCHandle.Alloc(data, GCHandleType.Pinned);

                _blob = DxcCompiler.Utils.CreateBlob(_dataPointer.AddrOfPinnedObject(), data.Length, Dxc.DXC_CP_UTF8);
            }

            public void Dispose()
            {
                _blob = null;

                if (_dataPointer.IsAllocated)
                    _dataPointer.Free();
                _dataPointer = default;
            }
        }
    }
}