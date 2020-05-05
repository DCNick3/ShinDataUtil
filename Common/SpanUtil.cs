using System;
using System.Runtime.InteropServices;

namespace ShinDataUtil.Common
{
    /// <summary>
    /// Quality of life utilities for converting structures to spans of bytes
    /// </summary>
    public static class SpanUtil
    {
        public static Span<byte> AsBytes<T>(ref T value) where T : struct =>
            MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1));

        public static ReadOnlySpan<byte> AsReadOnlyBytes<T>(ref T value) where T : struct =>
            MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));

    }
}