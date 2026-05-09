// Copyright 2026 Kelvin Nishikawa
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
//
//     http://www.apache.org/licenses/LICENSE-2.0

#if WACS_SENTIS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Sentis;
using Wacs.WASI.NN;
using Wacs.WASI.NN.Types;
using SentisTensor = Unity.Sentis.Tensor;
using WasiTensor = Wacs.WASI.NN.Types.Tensor;

namespace Wacs.WASI.NN.Sentis
{
    /// <summary>
    /// One inference session against a <see cref="SentisGraph"/>.
    /// Owns a <see cref="Worker"/> that holds per-call scheduling
    /// state; concurrent contexts each have their own Worker so
    /// the host can fan out parallel inferences against the same
    /// loaded model.
    ///
    /// <para>Sentis Workers are not thread-safe across
    /// <c>Schedule</c> calls and hold internal command-buffer
    /// state — every <see cref="IBackendContext"/> instance
    /// constructs its own Worker. The Worker is disposed when
    /// the context is dropped (the WIT
    /// <c>[resource-drop]graph-execution-context</c> binding
    /// triggers it when the guest releases the handle).</para>
    ///
    /// <para>Type support in v0: only <see cref="TensorType.FP32"/>
    /// and <see cref="TensorType.I32"/> are wired through, which
    /// covers the typical ONNX classifier / regressor /
    /// segmentation models. FP16, BF16, FP64, U8, I64 throw
    /// <see cref="ErrorCode.UnsupportedOperation"/> at lift —
    /// Sentis 2.x supports float and int element types directly;
    /// other element types need either Sentis-side casts in the
    /// model or host-side conversion that v0 doesn't ship.</para>
    /// </summary>
    internal sealed class SentisContext : IBackendContext
    {
        private readonly SentisGraph _graph;
        private readonly Worker _worker;
        private bool _disposed;

        public SentisContext(SentisGraph graph)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _worker = new Worker(graph.Model, graph.BackendType);
        }

        public IReadOnlyList<NamedTensor> Compute(IReadOnlyList<NamedTensor> inputs)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SentisContext));

            // We hold uploaded input tensors until after the
            // outputs are downloaded. Sentis Schedule() on
            // GPUCompute is async — the GPU keeps reading the
            // input buffers until DownloadToArray syncs, so
            // disposing inputs between Schedule and Peek is a
            // use-after-free.
            var uploadedInputs = new List<SentisTensor>(inputs.Count);
            try
            {
                var seen = new HashSet<string>();
                for (int i = 0; i < inputs.Count; i++)
                {
                    var nt = inputs[i];
                    if (!seen.Add(nt.Name))
                        throw new WasiNNException(
                            ErrorCode.InvalidArgument,
                            $"duplicate input name '{nt.Name}'");
                    var st = BuildSentisTensor(nt.Tensor);
                    uploadedInputs.Add(st);
                    _worker.SetInput(nt.Name, st);
                }

                _worker.Schedule();

                // Sentis 2.x exposes outputs through the model's
                // declared outputs list — preserve declaration
                // order to match what guests expect.
                var modelOutputs = _graph.Model.outputs;
                var output = new List<NamedTensor>(modelOutputs.Count);
                for (int i = 0; i < modelOutputs.Count; i++)
                {
                    string name = modelOutputs[i].name;
                    SentisTensor t = _worker.PeekOutput(name);
                    output.Add(new NamedTensor(name, MaterializeSentisTensor(t)));
                }
                return output;
            }
            catch (Exception ex) when (ex is not WasiNNException)
            {
                throw new WasiNNException(
                    ErrorCode.RuntimeError,
                    $"Sentis Schedule failed: {ex.Message}",
                    backendData: ex.ToString(),
                    innerException: ex);
            }
            finally
            {
                // PeekOutput tensors are owned by the worker;
                // we only dispose what we uploaded.
                for (int i = 0; i < uploadedInputs.Count; i++)
                    uploadedInputs[i].Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _worker.Dispose();
        }

        // ----------------------------------------------------
        //   Tensor lift / materialize
        // ----------------------------------------------------

        // Build a Sentis Tensor from our typed wasi-nn Tensor.
        // Reinterpret the byte buffer as the target element type
        // and hand it to the Sentis ctor; the ctor copies into
        // the default backend allocation.
        private static SentisTensor BuildSentisTensor(WasiTensor t)
        {
            var shape = ToSentisShape(t.Dimensions);
            var data = t.Data.Span;

            return t.Type switch
            {
                TensorType.FP32 => new Tensor<float>(shape, Cast<float>(data)),
                TensorType.I32 => new Tensor<int>(shape, Cast<int>(data)),
                TensorType.FP16 => throw new WasiNNException(
                    ErrorCode.UnsupportedOperation,
                    "SentisBackend v0 does not yet wire FP16 inputs; "
                    + "Sentis exposes float/int element types directly "
                    + "and would need a host-side Half conversion."),
                TensorType.BF16 => throw new WasiNNException(
                    ErrorCode.UnsupportedOperation,
                    "SentisBackend v0 does not yet wire BF16 inputs."),
                TensorType.FP64 => throw new WasiNNException(
                    ErrorCode.UnsupportedOperation,
                    "SentisBackend v0 does not yet wire FP64 inputs; "
                    + "Sentis 2.x has no double element type — the model "
                    + "must declare float inputs."),
                TensorType.U8 => throw new WasiNNException(
                    ErrorCode.UnsupportedOperation,
                    "SentisBackend v0 does not yet wire U8 inputs; "
                    + "Sentis 2.x has no byte element type."),
                TensorType.I64 => throw new WasiNNException(
                    ErrorCode.UnsupportedOperation,
                    "SentisBackend v0 does not yet wire I64 inputs; "
                    + "Sentis 2.x has no long element type — the model "
                    + "must declare int inputs."),
                _ => throw new WasiNNException(
                    ErrorCode.InvalidArgument,
                    $"unknown TensorType {t.Type}"),
            };
        }

        // Reinterpret the byte buffer as a typed array. Length
        // is checked at the wasi-nn Tensor constructor so the
        // cast can't overrun.
        private static T[] Cast<T>(ReadOnlySpan<byte> bytes) where T : unmanaged
        {
            var span = MemoryMarshal.Cast<byte, T>(bytes);
            var arr = new T[span.Length];
            span.CopyTo(arr);
            return arr;
        }

        private static TensorShape ToSentisShape(uint[] dims)
        {
            // Sentis TensorShape is int-indexed; wasi-nn carries
            // dimensions as uint per the WIT spec. Cast through
            // checked() so a dim > int.MaxValue raises rather
            // than silently wrapping into a negative shape Sentis
            // would interpret as a dynamic dim.
            var s = new int[dims.Length];
            for (int i = 0; i < dims.Length; i++)
                s[i] = checked((int)dims[i]);
            return new TensorShape(s);
        }

        // Pull bytes + dims + type out of a Sentis Tensor and
        // build our wasi-nn Tensor. The Sentis Tensor is owned
        // by the worker — we have to download data before the
        // next Schedule overwrites it.
        private static WasiTensor MaterializeSentisTensor(SentisTensor t)
        {
            var shape = t.shape;
            var dims = new uint[shape.rank];
            for (int i = 0; i < shape.rank; i++)
            {
                int d = shape[i];
                if (d < 0)
                    throw new WasiNNException(
                        ErrorCode.RuntimeError,
                        $"output tensor has dynamic dimension at index {i}; "
                        + "wasi-nn requires concrete shapes");
                dims[i] = (uint)d;
            }

            return t.dataType switch
            {
                DataType.Float => MaterializeFloat((Tensor<float>)t, dims),
                DataType.Int => MaterializeInt((Tensor<int>)t, dims),
                _ => throw new WasiNNException(
                    ErrorCode.UnsupportedOperation,
                    $"SentisBackend v0 does not map output dataType {t.dataType} "
                    + "to a wasi-nn tensor-type."),
            };
        }

        private static WasiTensor MaterializeFloat(Tensor<float> t, uint[] dims)
        {
            float[] data = t.DownloadToArray();
            var bytes = new byte[data.Length * sizeof(float)];
            MemoryMarshal.AsBytes(data.AsSpan()).CopyTo(bytes);
            return new WasiTensor(dims, TensorType.FP32, bytes);
        }

        private static WasiTensor MaterializeInt(Tensor<int> t, uint[] dims)
        {
            int[] data = t.DownloadToArray();
            var bytes = new byte[data.Length * sizeof(int)];
            MemoryMarshal.AsBytes(data.AsSpan()).CopyTo(bytes);
            return new WasiTensor(dims, TensorType.I32, bytes);
        }
    }
}
#endif
