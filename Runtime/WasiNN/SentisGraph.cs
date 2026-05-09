// Copyright 2026 Kelvin Nishikawa
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
//
//     http://www.apache.org/licenses/LICENSE-2.0

#if WACS_SENTIS
using System;
using Unity.Sentis;
using Wacs.WASI.NN;

namespace Wacs.WASI.NN.Sentis
{
    /// <summary>
    /// Wraps a Sentis <see cref="Model"/> plus the
    /// <see cref="BackendType"/> chosen at load time. The Model
    /// is a heavy resource (parsed graph + weights pinned in
    /// native memory) and is reused across every
    /// <see cref="SentisContext"/> minted from this graph.
    ///
    /// <para>Per-graph state, not per-context. Sentis Models
    /// are immutable and reusable; the per-call mutable state
    /// (input tensors, schedule iterator, output buffers) lives
    /// on <see cref="Worker"/> instances owned by the
    /// contexts.</para>
    ///
    /// <para>Sentis 2.x <see cref="Model"/> is not
    /// <see cref="IDisposable"/> in the public API — its
    /// constant tensors are GC-collected with the Model
    /// itself. Workers minted off this graph hold the heavy
    /// per-call resources and dispose individually when their
    /// contexts are dropped.</para>
    /// </summary>
    internal sealed class SentisGraph : IBackendGraph
    {
        // Internal so SentisContext can construct workers
        // against this model without a public accessor on the
        // SPI surface.
        internal Model Model { get; }
        internal BackendType BackendType { get; }

        private bool _disposed;

        public SentisGraph(Model model, BackendType backendType)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            BackendType = backendType;
        }

        public IBackendContext CreateContext()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SentisGraph));
            return new SentisContext(this);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // Sentis Model is not IDisposable; weights are
            // released when the Model becomes unreachable.
            // Per-context Worker disposal happens when each
            // SentisContext is dropped — that runs ahead of
            // this Dispose under the SPI contract.
        }
    }
}
#endif
