using SharpGen.Runtime;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.Dxc;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D12.D3D12;
using static Vortice.DXGI.DXGI;
using ResultCode = Vortice.DXGI.ResultCode;

namespace DirectX3D12Example
{
    public partial class D3D12GraphicsDevice
    {
        #region Constants and Fields

        private const int BufferCount = 2;

        private Control[] controls;
        private IDXGIFactory4? DXGIFactory;
        private ID3D12Device2? tempDevice;
        private ID3D12Device2? device;
        private ID3D12DescriptorHeap? rtvHeap;
        private int rtvDescriptorSize;
        private ID3D12Resource[]? renderTargets;
        private ID3D12CommandAllocator[]? commandAllocators;

        private ID3D12RootSignature? rootSignature;
        private ID3D12PipelineState? pipelineState;

        private ID3D12GraphicsCommandList4? commandList;

        private ID3D12Resource? vertexBuffer;

        private ID3D12Fence? frameFence;
        private AutoResetEvent? frameFenceEvent;
        private ulong frameCount;
        private ulong frameIndex;
        private int backbufferIndex;
        private bool validation = false;
        private bool useWarpDevice = false;
        private FeatureLevel maxSupportedFeatureLevel;
        public bool initialized = false;

        #endregion

        #region Properties

        public bool IsTearingSupported { get; set; }

        public ID3D12CommandQueue? commandQueue { get; set; }

        public IDXGISwapChain3? SwapChain { get; set; }

        #endregion

        #region Constructor

        public D3D12GraphicsDevice(Control[] controls)
        {
            #if DEBUG
            validation = true;
            #endif

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

            // Enable the debug layouter (always before device creation)
            if (validation && D3D12GetDebugInterface(out ID3D12Debug? debug).Success)
            {
                debug!.EnableDebugLayer();
                debug!.Dispose();
            }

            // Create the device
            DXGIFactory = CreateDXGIFactory2<IDXGIFactory4>(validation);
            if (useWarpDevice)
            {
                if (DXGIFactory.EnumWarpAdapter(out IDXGIAdapter? adapter).Success)
                {
                    D3D12CreateDevice(adapter, out device);
                    
                    string text = $"Using device: '{adapter.Description.Description}'";
                    ((Form1)this.controls[0]).UpdateLabelText(text);

                    adapter?.Dispose();
                }
            }
            else
            {
                // Auswahl der GPU möglich (High-Performance for Desktop, Lower-Performance for Notebook, ...)
                for (int adapterIndex = 0; DXGIFactory.EnumAdapters1(adapterIndex, out IDXGIAdapter1 adapter).Success; adapterIndex++)
                {
                    AdapterDescription1 desc = adapter.Description1;
                    var output = adapter.GetOutput(0); // monitor informations (resolution, name, HDR support, etc.)

                    // Don't select the Basic Render Driver adapter.
                    if ((desc.Flags & AdapterFlags.Software) != AdapterFlags.None)
                    {
                        adapter.Dispose();
                        continue;
                    }

                    if (D3D12CreateDevice(adapter, out tempDevice).Success)
                    {
                        maxSupportedFeatureLevel = this.tempDevice!.CheckMaxSupportedFeatureLevel();
                        string text = $"Supported FeatureLevel: '{maxSupportedFeatureLevel}'";
                        ((Form1)this.controls[0]).UpdateLabelText(text);

                        // create device with max supported feature level
                        if (D3D12CreateDevice(adapter, maxSupportedFeatureLevel, out device).Failure)
                        {
                            MessageBox.Show("Device not Created");
                            return;
                        }

                        adapter.Dispose();
                        tempDevice?.Dispose();
                        break;
                    }
                }
            }

            // Create Command queue
            // Die commandQueue arbeitet die CommandLists ab
            // Potenziell können mehrere Queues für unterschiedliche Zwecke erstellt werden (Copy, Direct, Compute, ...)
            commandQueue = device!.CreateCommandQueue<ID3D12CommandQueue>(CommandListType.Direct);
            commandQueue.Name = "Command Queue";

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
            using (IDXGISwapChain1 swapChain = DXGIFactory.CreateSwapChainForHwnd(commandQueue, this.controls[1].Handle, swapChainDesc))
            {
                DXGIFactory.MakeWindowAssociation(controls[1].Handle, WindowAssociationFlags.IgnoreAltEnter);

                SwapChain = swapChain.QueryInterface<IDXGISwapChain3>();
                backbufferIndex = SwapChain.CurrentBackBufferIndex;
            }

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

        private void LoadAssets()
        {
            // Create an empty root signature
            RootSignatureDescription1 rootSignatureDesc = new(RootSignatureFlags.AllowInputAssemblerInputLayout);
            rootSignature = device!.CreateRootSignature<ID3D12RootSignature>(rootSignatureDesc);

            InputElementDescription[] inputElementDescs = new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 12, 0)
            };

            // Compile the shaders
            byte[] vertexShaderByteCode = CompileBytecode(DxcShaderStage.Vertex, "Triangle.hlsl", "VSMain");
            byte[] pixelShaderByteCode = CompileBytecode(DxcShaderStage.Pixel, "Triangle.hlsl", "PSMain");

            // Create vertex input layout
            // Create a pipeline state object description, then create the object
            PipelineStateStream pipelineStateStream = new PipelineStateStream
            {
                RootSignature = rootSignature,
                VertexShader = new ShaderBytecode(vertexShaderByteCode),
                PixelShader = new ShaderBytecode(pixelShaderByteCode),
                InputLayout = new InputLayoutDescription(inputElementDescs),
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
            commandList = device.CreateCommandList<ID3D12GraphicsCommandList4>(CommandListType.Direct, commandAllocators![0], pipelineState);
            commandList.Close();

            // Create and load the vertex buffers
            // Create the vertex buffer views
            VertexPositionColor[] triangleVertices = new VertexPositionColor[]
            {
                  new VertexPositionColor(new Vector3(0f, 0.5f, 0.0f), new Color4(1.0f, 0.0f, 0.0f, 1.0f)),
                  new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.0f), new Color4(0.0f, 1.0f, 0.0f, 1.0f)),
                  new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.0f), new Color4(0.0f, 0.0f, 1.0f, 1.0f))
            };

            int vertexBufferSize = 3 * Unsafe.SizeOf<VertexPositionColor>();

            vertexBuffer = device.CreateCommittedResource<ID3D12Resource>(
                new HeapProperties(HeapType.Upload),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)vertexBufferSize),
                ResourceStates.GenericRead);

            unsafe
            {
                IntPtr bufferData = (IntPtr)vertexBuffer.Map<VertexPositionColor>(0);
                ReadOnlySpan<VertexPositionColor> src = new(triangleVertices);
                MemoryHelpers.CopyMemory(bufferData, src);
                vertexBuffer.Unmap(0);
            }

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
            // TODO
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
            commandQueue!.ExecuteCommandList(commandList!);

            // Present the frame
            Result result = SwapChain!.Present(1, PresentFlags.None);
            if (result.Failure && result.Code == ResultCode.DeviceRemoved) return;

            WaitForPreviousFrame();
        }

        private void PopulateCommandList()
        {
            // Reset the command list allocator (Re-use the memory that is associated with the command allocator.)
            commandAllocators![frameIndex].Reset();

            // Reset the command list
            commandList!.Reset(commandAllocators[frameIndex], pipelineState);
            commandList.BeginEvent("Frame");

            // Set the graphic root signature (Sets the graphics root signature to use for the current command list.)
            commandList.SetGraphicsRootSignature(rootSignature);

            // Set the viewport and scissor rectangles
            commandList.RSSetViewport(new Viewport(controls[1].ClientSize.Width, controls[1].ClientSize.Height));
            commandList.RSSetScissorRect(controls[1].ClientSize.Width, controls[1].ClientSize.Height);

            // Set a resource barrier, idicating the back buffer is to be used as a render target
            // (Resource barriers are used to manage resource transitions.)
            commandList.ResourceBarrierTransition(renderTargets![backbufferIndex], ResourceStates.Present, ResourceStates.RenderTarget);

            CpuDescriptorHandle rtv = rtvHeap!.GetCPUDescriptorHandleForHeapStart();
            rtv += backbufferIndex * rtvDescriptorSize;

            Color4 clearColor = new Color4(0.0f, 0.2f, 0.4f, 1.0f);

            var renderPassDesc = new RenderPassRenderTargetDescription(rtv,
                new RenderPassBeginningAccess(new ClearValue(Format.R8G8B8A8_UNorm, clearColor)),
                new RenderPassEndingAccess(RenderPassEndingAccessType.Preserve)
                );

            commandList.BeginRenderPass(renderPassDesc);

            commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            int stride = Unsafe.SizeOf<VertexPositionColor>();
            int vertexBufferSize = 3 * stride;
            commandList.IASetVertexBuffers(0, new VertexBufferView(vertexBuffer!.GPUVirtualAddress, vertexBufferSize, stride));
            commandList.DrawInstanced(3, 1, 0, 0);

            commandList.EndRenderPass();

            // Indicate that the back buffer will now be used to present.
            commandList.ResourceBarrierTransition(renderTargets[backbufferIndex], ResourceStates.RenderTarget, ResourceStates.Present);
            commandList.EndEvent();
            commandList.Close();
        }

        internal void ChangeDevice(bool checkState)
        {
            this.useWarpDevice = checkState;
        }

        private static byte[] CompileBytecode(DxcShaderStage stage, string shaderName, string entryPoint)
        {
            string assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
            string shaderSource = File.ReadAllText(Path.Combine(assetsPath, shaderName));

            using (var includeHandler = new ShaderIncludeHandler(assetsPath))
            {
                using IDxcResult? results = DxcCompiler.Compile(stage, shaderSource, entryPoint, includeHandler: includeHandler);
                if (results!.GetStatus().Failure)
                {
                    throw new Exception(results!.GetErrors());
                }

                return results.GetObjectBytecodeArray();
            }
        }

        public void Dispose()
        {
            WaitForPreviousFrame();

            vertexBuffer?.Dispose();

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
            commandQueue?.Dispose();
            device?.Dispose();
            DXGIFactory?.Dispose();
        }

        private void WaitForPreviousFrame()
        {
            // WAITING FOR THE FRAME TO COMPLETE BEFORE CONTINUING IS NOT BEST PRACTICE.
            // This is code implemented as such for simplicity. More advanced samples 
            // illustrate how to use fences for efficient resource usage.

            // Signal and increment the fence value.
            commandQueue!.Signal(frameFence, ++frameCount);

            // Wait until the previous frame is finished.
            ulong GPUFrameCount = frameFence!.CompletedValue; // returns UINT64_MAX if something gone wrong
            if ((frameCount - GPUFrameCount) >= BufferCount)
            {
                frameFence.SetEventOnCompletion(GPUFrameCount + 1, frameFenceEvent);
                frameFenceEvent!.WaitOne();
            }

            frameIndex = frameCount % BufferCount;
            backbufferIndex = SwapChain!.CurrentBackBufferIndex;
        }

        private void WaitIdle()
        {
            // Wait for the GPU to finish
            commandQueue!.Signal(frameFence, ++frameCount);
            frameFence!.SetEventOnCompletion(frameCount, frameFenceEvent);
            frameFenceEvent!.WaitOne();
        }
    }
}
