// Copyright 2026 Kelvin Nishikawa
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
//
//     http://www.apache.org/licenses/LICENSE-2.0

#if WACS_SENTIS
using System;
using System.Collections.Generic;
using Unity.Sentis;
using Wacs.WASI.NN;
using Wacs.WASI.NN.Types;

namespace Wacs.WASI.NN.Sentis
{
    /// <summary>
    /// <see cref="IBackend"/> implementation for
    /// <see cref="GraphEncoding.ONNX"/> backed by Unity Sentis.
    /// Sentis is Unity-only (depends on UnityEngine + the SRP
    /// compute pipeline), so this backend is gated on the
    /// <c>com.unity.sentis</c> package being present in the
    /// Unity project. The asmdef defines <c>WACS_SENTIS</c> only
    /// when Sentis ≥ 2.0.0 is installed; without the define this
    /// file compiles to nothing.
    ///
    /// <para>Lifetime: each <see cref="LoadGraph"/> call calls
    /// <see cref="ModelLoader.Load(byte[])"/> once. Multiple
    /// contexts minted from one graph each construct a fresh
    /// <see cref="Worker"/>; Sentis Workers are not thread-safe
    /// and hold per-instance scheduling state, so contexts can
    /// not share one. The Model itself is reusable across
    /// Workers and is owned by the SentisGraph wrapper.</para>
    ///
    /// <para><see cref="ExecutionTarget"/> mapping:
    /// <c>CPU → BackendType.CPU</c>,
    /// <c>GPU → BackendType.GPUCompute</c>,
    /// <c>TPU → throw UnsupportedOperation</c> (Sentis has no
    /// TPU backend). Embedders that want <c>BackendType.GPUPixel</c>
    /// or a non-default Sentis backend pass an explicit
    /// <see cref="BackendType"/> through the constructor and
    /// the wasi-nn target enum is ignored.</para>
    /// </summary>
    public sealed class SentisBackend : IBackend
    {
        private readonly BackendType? _overrideBackend;

        /// <summary>
        /// Create the backend with default target mapping
        /// (wasi-nn ExecutionTarget.CPU/GPU → Sentis CPU/GPUCompute).
        /// </summary>
        public SentisBackend() : this(null) { }

        /// <summary>
        /// Force every graph onto a specific Sentis
        /// <see cref="BackendType"/>, ignoring the guest's
        /// <see cref="ExecutionTarget"/> request. Useful when
        /// the host wants to pin inference to GPUPixel for
        /// fragment-shader execution or to a particular
        /// platform-supported backend.
        /// </summary>
        public SentisBackend(BackendType? overrideBackend)
        {
            _overrideBackend = overrideBackend;
        }

        public IReadOnlyCollection<GraphEncoding> SupportedEncodings { get; }
            = new[] { GraphEncoding.ONNX };

        public IBackendGraph LoadGraph(
            IReadOnlyList<ReadOnlyMemory<byte>> builders,
            ExecutionTarget target)
        {
            if (builders.Count == 0)
                throw new WasiNNException(
                    ErrorCode.InvalidArgument,
                    "graph.load received an empty builder list");

            // ONNX is a single self-contained protobuf; the
            // canonical case is one builder. Multi-builder input
            // gets concatenated defensively (no real ONNX guests
            // we know of split a model across builders, but the
            // spec allows it for backends like OpenVINO).
            byte[] modelBytes = ConcatBuilders(builders);

            BackendType backendType = _overrideBackend ?? MapTarget(target);

            Model model;
            try
            {
                model = ModelLoader.Load(modelBytes);
            }
            catch (Exception ex)
            {
                throw new WasiNNException(
                    ErrorCode.RuntimeError,
                    $"Sentis ModelLoader.Load failed: {ex.Message}",
                    backendData: ex.ToString(),
                    innerException: ex);
            }

            return new SentisGraph(model, backendType);
        }

        public IBackendGraph LoadGraphByName(string name, ExecutionTarget target)
        {
            // Sentis has no internal named-model registry. Route
            // to NotFound so the orchestrator's
            // NamedModelResolver path is the only way to resolve
            // names (it looks up bytes/encoding/target on the
            // host side and calls back into LoadGraph).
            throw new WasiNNException(
                ErrorCode.NotFound,
                "SentisBackend has no internal named-model registry; "
                + "configure WasiNNConfiguration.NamedModelResolver "
                + "to map names to (encoding, builders) pairs.");
        }

        private static BackendType MapTarget(ExecutionTarget target) => target switch
        {
            ExecutionTarget.CPU => BackendType.CPU,
            ExecutionTarget.GPU => BackendType.GPUCompute,
            ExecutionTarget.TPU => throw new WasiNNException(
                ErrorCode.UnsupportedOperation,
                "SentisBackend does not support ExecutionTarget.TPU; "
                + "Sentis only ships CPU / GPUCompute / GPUPixel backends."),
            _ => throw new WasiNNException(
                ErrorCode.InvalidArgument,
                $"unknown ExecutionTarget {target}"),
        };

        private static byte[] ConcatBuilders(IReadOnlyList<ReadOnlyMemory<byte>> builders)
        {
            if (builders.Count == 1)
                return builders[0].ToArray();
            int total = 0;
            for (int i = 0; i < builders.Count; i++)
                total += builders[i].Length;
            var concat = new byte[total];
            int offset = 0;
            for (int i = 0; i < builders.Count; i++)
            {
                builders[i].Span.CopyTo(concat.AsSpan(offset));
                offset += builders[i].Length;
            }
            return concat;
        }
    }
}
#endif
