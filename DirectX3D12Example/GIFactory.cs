using Vortice.DXGI;

namespace DirectX3D12Example
{
    internal class GIFactory : IDisposable
    {
        private readonly IDXGIFactory4 factory4;

        public GIFactory()
        {
            #if DEBUG
            const bool validation = true;
            #endif

            this.factory4 = DXGI.CreateDXGIFactory2<IDXGIFactory4>(validation);

            if (this.factory4 == null)
            {
                throw new NullReferenceException();
            }
        }

        public IDXGIFactory4 DXGIFactory4 => this.factory4;

        /// <summary>
        /// Retrieve the desired adapter. Will give most performant or default adapter instead.
        /// </summary>
        /// <returns></returns>
        public IDXGIAdapter GetAdapter()
        {
            for (int adapterIndex = 0; this.factory4.EnumAdapters1(adapterIndex, out IDXGIAdapter1? adapter).Success; adapterIndex++)
            {
                AdapterDescription1 desc = adapter.Description1;

                // Don't select the Basic Render Driver adapter.
                if ((desc.Flags & AdapterFlags.Software) != AdapterFlags.None)
                {
                    adapter.Dispose();
                    continue;
                }

                return adapter;
            }

            // try get WARP adapter
            if (this.factory4.EnumWarpAdapter(out IDXGIAdapter? warpAdapter).Success)
            {
                if (warpAdapter != null)
                {
                    return warpAdapter;
                }
            }

            throw new NullReferenceException();
        }

        /// <summary>
        /// Check if tearing is supported.
        /// </summary>
        /// <returns>True if allowed.</returns>
        public bool CheckTearingSupport()
        {
            var factory5 = DXGI.CreateDXGIFactory2<IDXGIFactory5>(false);
            bool tearingAllowed = factory5.PresentAllowTearing;
            factory5.Dispose();

            return tearingAllowed;
        }

        public void Dispose()
        {
            this.factory4.Dispose();
        }
    }
}
