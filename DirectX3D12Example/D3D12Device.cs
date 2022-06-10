using SharpGen.Runtime;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DirectWrite;
using Vortice.Dxc;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D12.D3D12;
using BlendDescription = Vortice.Direct3D12.BlendDescription;
using FeatureLevel = Vortice.Direct3D.FeatureLevel;
using ResultCode = Vortice.DXGI.ResultCode;

#nullable disable

namespace DirectX3D12Example
{
    public partial class D3D12GraphicsDevice
    {
        #region Constants and Fields

        private const int BufferCount = 2;

        private Control[] controls;
        private ID3D12Device2 device;
        private ID3D12DebugDevice2 debugDevice;
        private ID3D12DescriptorHeap rtvHeap;
        private int rtvDescriptorSize;
        private ID3D12Resource[] renderTargets;
        private ID3D12CommandAllocator[] commandAllocators;

        private ID3D12RootSignature rootSignature;
        private ID3D12PipelineState pipelineState;

        private ID3D12GraphicsCommandList4 commandList;

        private ID3D12Resource vertexBufferTriangle;
        private ID3D12Resource vertexBufferSignal;
        private ID3D12Resource vertexBufferPoint;

        private int vertexBufferTriangleSize;
        private int vertexBufferSignalSize;
        private int vertexBufferPointSize;

        private ID3D12Fence frameFence;
        private AutoResetEvent frameFenceEvent;
        private ulong frameCount;
        private ulong frameIndex;
        private int backbufferIndex;
        private FeatureLevel maxSupportedFeatureLevel;
        public bool initialized = false;

        private ID2D1HwndRenderTarget hwndRenderTarget;
        private IDWriteTextFormat textFormat;
        private string text = "Hello, DirectWrite!";

        private Color4 clearColor = new Color4(0.0f, 0.2f, 0.4f, 1.0f);
        private Color4 colorYellow = new Color4(1.0f, 1.0f, 0.0f, 1.0f);
        private Color4 colorRed = new Color4(1.0f, 0.0f, 0.0f, 1.0f);
        private Color4 colorGreen = new Color4(0.0f, 1.0f, 0.0f, 1.0f);

        #endregion

        #region Properties

        public ID3D12CommandQueue CommandQueue { get; set; }

        public IDXGISwapChain3 SwapChain { get; set; }

        #endregion

        #region Constructor

        public D3D12GraphicsDevice(Control[] controls)
        {
            this.controls = controls;
        }

        #endregion

        public void OnInit()
        {
            this.LoadPipeline();
            this.LoadAssets();
            this.initialized = true;
        }

        private void LoadPipeline()
        {
            // Check supported feature level
            if (!IsSupported())
            {
                MessageBox.Show("Featurelevel not Supported!");
                return;
            }

#if DEBUG
            // Enable the debug layouter (always before device creation)
            if (D3D12GetDebugInterface<ID3D12Debug3>(out var debugController).Success)
            {
                debugController?.EnableDebugLayer();
            }
#endif

            // Create the device
            GIFactory factory = new GIFactory();
            IDXGIAdapter adapter = factory.GetAdapter();

            // Auswahl der GPU möglich (High-Performance for Desktop, Lower-Performance for Notebook, ...)
            if (D3D12CreateDevice(adapter, out ID3D12Device2 tempDevice).Success)
            {
                maxSupportedFeatureLevel = tempDevice.CheckMaxSupportedFeatureLevel();
                string text = $"FeatureLevel: '{maxSupportedFeatureLevel}'; Device: '{adapter.Description.Description}'";
                ((Form1)this.controls[0]).UpdateLabelText(text);

                // create device with max supported feature level
                if (D3D12CreateDevice(adapter, maxSupportedFeatureLevel, out device).Failure)
                {
                    MessageBox.Show("Device not Created");
                    return;
                }

                tempDevice?.Dispose();
            }

            debugDevice = new ID3D12DebugDevice2((IntPtr)device);
            adapter.Dispose();

            // Create Command queue
            // Die commandQueue arbeitet die CommandLists ab
            // Potenziell können mehrere Queues für unterschiedliche Zwecke erstellt werden (Copy, Direct, Compute, ...)
            CommandQueue = device.CreateCommandQueue<ID3D12CommandQueue>(CommandListType.Direct);
            CommandQueue.Name = "Command Queue";

            SwapChainDescription1 swapChainDesc = new()
            {
                BufferCount = BufferCount, // DoubleBuffering
                Width = controls[1].Bounds.Right - controls[1].Bounds.Left,
                Height = controls[1].Bounds.Bottom - controls[1].Bounds.Top,
                Format = Format.R8G8B8A8_UNorm,
                BufferUsage = Usage.RenderTargetOutput, // usage: render buffer content on output monitor
                SwapEffect = SwapEffect.FlipSequential,
                Stereo = false, // No 3D
                SampleDescription = new SampleDescription(1, 0) // No MSAA (not supported by D3D12 here)
            };

            // Create swap chain
            using (IDXGISwapChain1 swapChain = factory.DXGIFactory4.CreateSwapChainForHwnd(CommandQueue, this.controls[1].Handle, swapChainDesc))
            {
                factory.DXGIFactory4.MakeWindowAssociation(controls[1].Handle, WindowAssociationFlags.IgnoreAltEnter);

                SwapChain = swapChain.QueryInterface<IDXGISwapChain3>();
                backbufferIndex = SwapChain.CurrentBackBufferIndex;
            }

            factory.Dispose();

            // configure Direct2D and DirectWrite
            var writeFactory = DWrite.DWriteCreateFactory<IDWriteFactory>();
            var direct2DFactory = D2D1.D2D1CreateFactory<ID2D1Factory>();
            textFormat = writeFactory.CreateTextFormat("Arial", 26.0f);
            var test = writeFactory.GetSystemFontCollection(false);

            textFormat.TextAlignment = TextAlignment.Center;
            textFormat.ParagraphAlignment = ParagraphAlignment.Center;
            var renderTargetProperties = new RenderTargetProperties();
            var hwndRenderTargetProperties = new HwndRenderTargetProperties();
            hwndRenderTargetProperties.Hwnd = controls[2].Handle;
            hwndRenderTargetProperties.PixelSize = new SizeI(controls[2].Right - controls[2].Left, controls[2].Bottom - controls[2].Top);
            hwndRenderTarget = direct2DFactory.CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties);

            // Create a render target view (RTV) descriptor heap
            // A descriptor heap can be thought of as an array of descriptors. Where each descriptor fully describes an object to the GPU.
            rtvHeap = device.CreateDescriptorHeap<ID3D12DescriptorHeap>(new DescriptorHeapDescription(DescriptorHeapType.RenderTargetView, BufferCount));
            rtvDescriptorSize = device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);

            // Create frame resources (a render target view for each frame)
            CpuDescriptorHandle rtvHandle = rtvHeap.GetCPUDescriptorHandleForHeapStart();

            // Create a RTV = Render Targer View for each frame.
            renderTargets = new ID3D12Resource[BufferCount];
            for (int i = 0; i < BufferCount; i++)
            {
                renderTargets[i] = SwapChain.GetBuffer<ID3D12Resource>(i);
                device.CreateRenderTargetView(renderTargets[i], null, rtvHandle);

                rtvHandle += rtvDescriptorSize;
            }

            // Create a command allocator (A command allocator manages the underlying storage for command lists and bundles)
            commandAllocators = new ID3D12CommandAllocator[BufferCount];
            for (int i = 0; i < BufferCount; i++)
            {
                commandAllocators[i] = device.CreateCommandAllocator<ID3D12CommandAllocator>(CommandListType.Direct);
            }
        }

        private unsafe void LoadAssets()
        {
            // Create an empty root signature
            RootSignatureDescription1 rootSignatureDesc = new(RootSignatureFlags.AllowInputAssemblerInputLayout);
            rootSignature = device.CreateRootSignature<ID3D12RootSignature>(rootSignatureDesc);

            // Compile the shaders
            byte[] vertexShaderByteCode = CompileBytecode(DxcShaderStage.Vertex, "PositionColor.hlsl", "VSMain");
            byte[] pixelShaderByteCode = CompileBytecode(DxcShaderStage.Pixel, "PositionColor.hlsl", "PSMain");

            // Create vertex input layout
            // Create a pipeline state object description, then create the object
            PipelineStateStream pipelineStateStream = new PipelineStateStream
            {
                RootSignature = rootSignature,
                VertexShader = new ShaderBytecode(vertexShaderByteCode),
                PixelShader = new ShaderBytecode(pixelShaderByteCode),
                InputLayout = new InputLayoutDescription(VertexPositionColor.InputElements),
                SampleMask = uint.MaxValue,
                PrimitiveTopology = PrimitiveTopologyType.Triangle,
                RasterizerState = RasterizerDescription.CullCounterClockwise, // CullCounterClockwise
                BlendState = BlendDescription.Opaque,
                DepthStencilState = DepthStencilDescription.None,
                RenderTargetFormats = new[] { Format.R8G8B8A8_UNorm },
                DepthStencilFormat = Format.Unknown,
                SampleDescription = new SampleDescription(1, 0)
            };

            // Create pipeline state
            // A pipeline state object maintains the state of all currently set shaders
            // as well as certain fixed function state objects (such as the input assembler,
            // tesselator, rasterizer and output merger).
            pipelineState = device.CreatePipelineState(pipelineStateStream);

            // Create the command list, the close the command list
            commandList = device.CreateCommandList<ID3D12GraphicsCommandList4>(CommandListType.Direct, commandAllocators[0], pipelineState);
            commandList.Close(); // always close command list after using it

            // Create and load the vertex buffers
            // Create the vertex buffer views
            VertexPositionColor[] triangleVertices = new VertexPositionColor[]
            {
                  new VertexPositionColor(new Vector3(0f, 1.0f, 0.0f), new Color4(1.0f, 0.0f, 0.0f, 1.0f)),
                  new VertexPositionColor(new Vector3(1.0f, -1.0f, 0.0f), new Color4(0.0f, 1.0f, 0.0f, 1.0f)),
                  new VertexPositionColor(new Vector3(-1.0f, -1.0f, 0.0f), new Color4(0.0f, 0.0f, 1.0f, 1.0f))
            };

            vertexBufferTriangleSize = triangleVertices.Length * Unsafe.SizeOf<VertexPositionColor>();
            vertexBufferTriangle = device.CreateCommittedResource<ID3D12Resource>(
                new HeapProperties(HeapType.Upload),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)vertexBufferTriangleSize),
                ResourceStates.GenericRead);

            IntPtr bufferData = (IntPtr)vertexBufferTriangle.Map<VertexPositionColor>(0);
            ReadOnlySpan<VertexPositionColor> src = new(triangleVertices);
            MemoryHelpers.CopyMemory(bufferData, src);
            vertexBufferTriangle.Unmap(0);

            VertexPositionColor[] signalVertices = new VertexPositionColor[]
            {
                new VertexPositionColor(new Vector3(-0.7f, -0.5f, 0.0f), colorYellow),
                new VertexPositionColor(new Vector3(-0.5f, -0.2f, 0.0f), colorRed),
                new VertexPositionColor(new Vector3(-0.2f, -0.1f, 0.0f), colorRed),
                new VertexPositionColor(new Vector3(-0.1f, 0.2f, 0.0f), colorRed),
                new VertexPositionColor(new Vector3(0.2f, -0.25f, 0.0f), colorYellow),
                new VertexPositionColor(new Vector3(0.25f, 0.37f, 0.0f), colorYellow),
                new VertexPositionColor(new Vector3(0.37f, -0.5f, 0.0f), colorRed),
                new VertexPositionColor(new Vector3(0.5f, -0.6f, 0.0f), colorRed),
                new VertexPositionColor(new Vector3(0.6f, 0.8f, 0.0f), colorGreen),
                new VertexPositionColor(new Vector3(0.8f, 1.0f, 0.0f), colorGreen)
            };

            vertexBufferSignalSize = signalVertices.Length * Unsafe.SizeOf<VertexPositionColor>();
            vertexBufferSignal = device.CreateCommittedResource<ID3D12Resource>(
                new HeapProperties(HeapType.Upload),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)vertexBufferSignalSize),
                ResourceStates.GenericRead);

            IntPtr bufferData2 = (IntPtr)vertexBufferSignal.Map<VertexPositionColor>(0);
            ReadOnlySpan<VertexPositionColor> src2 = new(signalVertices);
            MemoryHelpers.CopyMemory(bufferData2, src2);
            vertexBufferSignal.Unmap(0);

            VertexPositionColor[] pointVertices = new VertexPositionColor[]
            {
                new VertexPositionColor(new Vector3(-0.8f, 0.8f, 0.0f), colorGreen),
                new VertexPositionColor(new Vector3(-0.8f, 0.7f, 0.0f), colorGreen),
                new VertexPositionColor(new Vector3(-0.8f, 0.6f, 0.0f), colorGreen),
                new VertexPositionColor(new Vector3(-0.8f, 0.5f, 0.0f), colorGreen),
                new VertexPositionColor(new Vector3(-0.8f, 0.4f, 0.0f), colorGreen)
            };

            vertexBufferPointSize = pointVertices.Length * Unsafe.SizeOf<VertexPositionColor>();
            vertexBufferPoint = device.CreateCommittedResource<ID3D12Resource>(
                new HeapProperties(HeapType.Upload),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)vertexBufferPointSize),
                ResourceStates.GenericRead);

            IntPtr bufferData3 = (IntPtr)vertexBufferPoint.Map<VertexPositionColor>(0);
            ReadOnlySpan<VertexPositionColor> src3 = new(pointVertices);
            MemoryHelpers.CopyMemory(bufferData3, src3);
            vertexBufferSignal.Unmap(0);

            // Create a fence and a event handle (A fence is used to synchronize the CPU with the GPU)
            frameFence = device.CreateFence<ID3D12Fence>(); // Initial value == 0
            frameFenceEvent = new AutoResetEvent(false);

            // Wait for the GPU to finish (Check on the fence!)
            WaitForPreviousFrame();
        }

        /// <summary>
        /// Update frame-based values.
        /// </summary>
        public void OnUpdate()
        {
            // TODO: Update with constant buffer here
        }

        /// <summary>
        /// Render the scene. Update everything that should change since the last frame.
        /// Modify the constant, vertex, index buffers, and everything else, as necessary.
        /// </summary>
        public void OnRender()
        {
            // Record all the commands we need to render the scene into the command list.
            PopulateCommandList();

            // Execute the command list.
            // Bei DirectX12 kann es generell mehrere CommandLists geben, die parallel abgearbeitet werden können.
            // In diesem Fall nutzt man die Methode ExecuteCommandLists und zusätzlich Signal(fence, value) und Wait(fence, value),
            // um die Synchronisierung zwischen den Listen sicherzustellen!
            CommandQueue.ExecuteCommandList(commandList);

            // start drawing text with Direct2D and DirectWrite
            hwndRenderTarget.BeginDraw();
            hwndRenderTarget.Transform = Matrix3x2.Identity;
            hwndRenderTarget.Clear(clearColor);

            var blackBrush = hwndRenderTarget.CreateSolidColorBrush(colorYellow);
            var layoutRect = new Rect(0, 0, controls[1].Width, controls[1].Height);

            hwndRenderTarget.DrawText(text, textFormat, layoutRect, blackBrush);
            hwndRenderTarget.EndDraw();

            // Present the frame
            Result result = SwapChain.Present(1, PresentFlags.None);
            if (result.Failure && result.Code == ResultCode.DeviceRemoved) return;

            WaitForPreviousFrame();
        }

        private void PopulateCommandList()
        {
            // Reset the command list allocator (Re-use the memory that is associated with the command allocator.)
            commandAllocators[frameIndex].Reset();

            // Reset the command list
            commandList.Reset(commandAllocators[frameIndex], pipelineState);
            commandList.BeginEvent("Frame");

            // Set the graphic root signature (Sets the graphics root signature to use for the current command list.)
            commandList.SetGraphicsRootSignature(rootSignature);

            // Set the viewport and scissor rectangles
            // (transformation of nomalized device space into screen space)
            commandList.RSSetViewport(new Viewport(controls[1].ClientSize.Width, controls[1].ClientSize.Height));
            // Defines the valid drawing area
            commandList.RSSetScissorRect(controls[1].ClientSize.Width, controls[1].ClientSize.Height);

            // Set a resource barrier, idicating the back buffer is to be used as a render target
            // (Resource barriers are used to manage resource transitions.)
            commandList.ResourceBarrierTransition(renderTargets[backbufferIndex], ResourceStates.Present, ResourceStates.RenderTarget);

            CpuDescriptorHandle rtv = rtvHeap.GetCPUDescriptorHandleForHeapStart();
            rtv += backbufferIndex * rtvDescriptorSize;

            var renderPassDesc = new RenderPassRenderTargetDescription(rtv,
                new RenderPassBeginningAccess(new ClearValue(Format.R8G8B8A8_UNorm, clearColor)),
                new RenderPassEndingAccess(RenderPassEndingAccessType.Preserve)
                );

            commandList.BeginRenderPass(renderPassDesc);

            int stride = Unsafe.SizeOf<VertexPositionColor>();

            commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            commandList.IASetVertexBuffers(0, new VertexBufferView(vertexBufferTriangle.GPUVirtualAddress, vertexBufferTriangleSize, stride));
            commandList.DrawInstanced(3, 1, 0, 0);

            commandList.IASetPrimitiveTopology(PrimitiveTopology.LineStrip);
            commandList.IASetVertexBuffers(0, new VertexBufferView(vertexBufferSignal.GPUVirtualAddress, vertexBufferSignalSize, stride));
            commandList.DrawInstanced(10, 1, 0, 0);

            commandList.IASetPrimitiveTopology(PrimitiveTopology.PointList);
            commandList.IASetVertexBuffers(0, new VertexBufferView(vertexBufferPoint.GPUVirtualAddress, vertexBufferPointSize, stride));
            commandList.DrawInstanced(5, 1, 0, 0);

            commandList.EndRenderPass();

            // Indicate that the back buffer will now be used to present.
            commandList.ResourceBarrierTransition(renderTargets[backbufferIndex], ResourceStates.RenderTarget, ResourceStates.Present);
            commandList.EndEvent();
            commandList.Close();
        }

        private void CreatePointTexture()
        {
            int size = 30;
            Bitmap bitmap = new Bitmap(size, size);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(System.Drawing.Color.Transparent);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Pen pen = new Pen(System.Drawing.Color.White, 1f);

            graphics.FillEllipse(Brushes.White, new Rectangle(0, 0, size - 2, size - 2));

            MemoryStream strm = new MemoryStream();
            bitmap.Save(strm, System.Drawing.Imaging.ImageFormat.Bmp);
            strm.Position = 0;

            ResourceDescription resourceDescription = new ResourceDescription();
            resourceDescription.Dimension = ResourceDimension.Texture2D;
            resourceDescription.Width = (ulong)size;
            resourceDescription.Height = size;
            resourceDescription.DepthOrArraySize = 1;
            resourceDescription.MipLevels = 1;
            resourceDescription.Format = Format.R8G8B8A8_UNorm;
            resourceDescription.SampleDescription = new SampleDescription(1, 0);
            resourceDescription.Layout = TextureLayout.Unknown;

            var texture = device.CreateCommittedResource<ID3D12Resource>(
                new HeapProperties(HeapType.Upload),
                HeapFlags.None,
                resourceDescription,
                ResourceStates.None);
            //Texture texture = Texture.FromStream(device, strm, Usage.None, Pool.Default);

            // Texture texture = Texture.FromStream(device, strm, bitmap.Width, bitmap.Height, 1, Usage.None, Format.A8B8G8R8, Pool.Default, Filter.None, Filter.None, 0);
            bitmap.Dispose();
            strm.Dispose();
            pen.Dispose();
        }

        private static byte[] CompileBytecode(DxcShaderStage stage, string shaderName, string entryPoint)
        {
            string assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
            string shaderSource = File.ReadAllText(Path.Combine(assetsPath, shaderName));

            using (var includeHandler = new ShaderIncludeHandler(assetsPath))
            {
                using IDxcResult results = DxcCompiler.Compile(stage, shaderSource, entryPoint, includeHandler: includeHandler);
                if (results.GetStatus().Failure)
                {
                    throw new Exception(results.GetErrors());
                }

                return results.GetObjectBytecodeArray();
            }
        }

        public void Dispose()
        {
            WaitForPreviousFrame();

            vertexBufferTriangle?.Dispose();

            for (int i = 0; i < BufferCount; i++)
            {
                commandAllocators?[i].Dispose();
                renderTargets?[i].Dispose();
            }
            commandList?.Dispose();

            rtvHeap?.Dispose();
            pipelineState?.Dispose();
            rootSignature?.Dispose();
            SwapChain?.Dispose();
            frameFence?.Dispose();
            CommandQueue?.Dispose();
            device?.Dispose();
            debugDevice?.Dispose();
        }

        private void WaitForPreviousFrame()
        {
            // WAITING FOR THE FRAME TO COMPLETE BEFORE CONTINUING IS NOT BEST PRACTICE.
            // This is code implemented as such for simplicity. More advanced samples 
            // illustrate how to use fences for efficient resource usage.

            // Signal and increment the fence value.
            CommandQueue.Signal(frameFence, ++frameCount);

            // Wait until the previous frame is finished.
            ulong GPUFrameCount = frameFence.CompletedValue; // returns UINT64_MAX if something gone wrong
            if ((frameCount - GPUFrameCount) >= BufferCount)
            {
                frameFence.SetEventOnCompletion(GPUFrameCount + 1, frameFenceEvent);
                frameFenceEvent.WaitOne();
            }

            frameIndex = frameCount % BufferCount;
            backbufferIndex = SwapChain.CurrentBackBufferIndex;
        }

        private void WaitIdle()
        {
            // Wait for the GPU to finish
            CommandQueue.Signal(frameFence, ++frameCount);
            frameFence.SetEventOnCompletion(frameCount, frameFenceEvent);
            frameFenceEvent.WaitOne();
        }
    }
}
