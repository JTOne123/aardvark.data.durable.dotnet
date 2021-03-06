﻿/*
    MIT License

    Copyright (c) 2020 Aardworx GmbH (https://aardworx.com). All rights reserved.

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

#if NETSTANDARD2_0

using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Aardvark.Data
{
    public static partial class DurableCodec
    {
        #region Encode

        private static readonly Action<BinaryWriter, object> EncodeDurableMapWithoutHeader =
            (s, o) =>
            {
                var xs = (IEnumerable<KeyValuePair<Durable.Def, object>>)o;
                var count = xs.Count();
                s.Write(count);
                foreach (var x in xs) Encode(s, x.Key, x.Value);
            };

        private static void PadToNextMultipleOf(this Stream s, int numberOfBytes)
        {
            var m = (int)(s.Position % numberOfBytes);
            if (m > 0)
            {
                var size = numberOfBytes - m;
                var buffer = new byte[size];
                s.Write(buffer, 0, size);
            }
        }
        private static readonly Action<BinaryWriter, object> EncodeDurableMap16WithoutHeader =
            (bw, o) =>
            {
                var s = bw.BaseStream;

                var xs = (IEnumerable<KeyValuePair<Durable.Def, object>>)o;

            // number of entries (int + padding)
            var count = xs.Count();
                bw.Write(count); // 4 bytes
            s.PadToNextMultipleOf(16);
#if DEBUG
            if (s.Position % 16 != 0) throw new Exception("Invariant 75944da3-3efa-4a0d-935d-c020d5ca7d56.");
#endif

            // entries (+ padding after each entry)
            foreach (var x in xs)
                {
                    Encode(bw, x.Key, x.Value);
                    s.PadToNextMultipleOf(16);
#if DEBUG
                if (s.Position % 16 != 0) throw new Exception("Invariant 9212ca74-a0a4-406f-9f7b-262e2e516918.");
#endif
            }
            };

        private static readonly Action<BinaryWriter, object> EncodeGZipped =
            (s, o) =>
            {
                var gzipped = (DurableGZipped)o;
                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);
                EncodeGuid(bw, gzipped.Def.Id);
                Encode(bw, gzipped.Def, gzipped.Value);
                bw.Flush();

                var buffer = ms.ToArray();

                var bufferGZipped = buffer.GZipCompress();
                s.Write(buffer.Length);
                s.Write(bufferGZipped.Length);
                s.Write(bufferGZipped, 0, bufferGZipped.Length);
            };

        private static readonly Action<BinaryWriter, object> EncodeGuid = (s, o) => s.Write(((Guid)o).ToByteArray(), 0, 16);
        private static readonly Action<BinaryWriter, object> EncodeGuidArray = (s, o) => EncodeArray(s, (Guid[])o);
        private static readonly Action<BinaryWriter, object> EncodeInt8 = (s, o) => s.Write((sbyte)o);
        private static readonly Action<BinaryWriter, object> EncodeInt8Array = (s, o) => EncodeArray(s, (sbyte[])o);
        private static readonly Action<BinaryWriter, object> EncodeUInt8 = (s, o) => s.Write((byte)o);
        private static readonly Action<BinaryWriter, object> EncodeUInt8Array = (s, o) => EncodeArray(s, (byte[])o);
        private static readonly Action<BinaryWriter, object> EncodeInt16 = (s, o) => s.Write((short)o);
        private static readonly Action<BinaryWriter, object> EncodeInt16Array = (s, o) => EncodeArray(s, (short[])o);
        private static readonly Action<BinaryWriter, object> EncodeUInt16 = (s, o) => s.Write((ushort)o);
        private static readonly Action<BinaryWriter, object> EncodeUInt16Array = (s, o) => EncodeArray(s, (ushort[])o);
        private static readonly Action<BinaryWriter, object> EncodeInt32 = (s, o) => s.Write((int)o);
        private static readonly Action<BinaryWriter, object> EncodeInt32Array = (s, o) => EncodeArray(s, (int[])o);
        private static readonly Action<BinaryWriter, object> EncodeUInt32 = (s, o) => s.Write((uint)o);
        private static readonly Action<BinaryWriter, object> EncodeUInt32Array = (s, o) => EncodeArray(s, (uint[])o);
        private static readonly Action<BinaryWriter, object> EncodeInt64 = (s, o) => s.Write((long)o);
        private static readonly Action<BinaryWriter, object> EncodeInt64Array = (s, o) => EncodeArray(s, (long[])o);
        private static readonly Action<BinaryWriter, object> EncodeUInt64 = (s, o) => s.Write((ulong)o);
        private static readonly Action<BinaryWriter, object> EncodeUInt64Array = (s, o) => EncodeArray(s, (ulong[])o);
        private static readonly Action<BinaryWriter, object> EncodeFloat32 = (s, o) => s.Write((float)o);
        private static readonly Action<BinaryWriter, object> EncodeFloat32Array = (s, o) => EncodeArray(s, (float[])o);
        private static readonly Action<BinaryWriter, object> EncodeFloat64 = (s, o) => s.Write((double)o);
        private static readonly Action<BinaryWriter, object> EncodeFloat64Array = (s, o) => EncodeArray(s, (double[])o);
        private static readonly Action<BinaryWriter, object> EncodeStringUtf8 = (s, o) => EncodeArray(s, Encoding.UTF8.GetBytes((string)o));
        private static readonly Action<BinaryWriter, object> EncodeStringUtf8Array = (s, o) =>
        {
            var xs = (string[])o;
            s.Write(xs.Length);
            foreach (var x in xs) EncodeArray(s, Encoding.UTF8.GetBytes(x));
        };

        private static readonly Action<BinaryWriter, object> EncodeCell =
            (s, o) => { var x = (Cell)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); s.Write(x.Exponent); };
        private static readonly Action<BinaryWriter, object> EncodeCellArray =
            (s, o) => EncodeArray(s, (Cell[])o);

        private static readonly Action<BinaryWriter, object> EncodeCell2d =
            (s, o) => { var x = (Cell2d)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Exponent); };
        private static readonly Action<BinaryWriter, object> EncodeCell2dArray =
            (s, o) => EncodeArray(s, (Cell2d[])o);

        private static readonly Action<BinaryWriter, object> EncodeV2i =
            (s, o) => { var x = (V2i)o; s.Write(x.X); s.Write(x.Y); };
        private static readonly Action<BinaryWriter, object> EncodeV2iArray =
            (s, o) => EncodeArray(s, (V2i[])o);
        private static readonly Action<BinaryWriter, object> EncodeV3i =
            (s, o) => { var x = (V3i)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); };
        private static readonly Action<BinaryWriter, object> EncodeV3iArray =
            (s, o) => EncodeArray(s, (V3i[])o);
        private static readonly Action<BinaryWriter, object> EncodeV4i =
            (s, o) => { var x = (V4i)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); s.Write(x.W); };
        private static readonly Action<BinaryWriter, object> EncodeV4iArray =
            (s, o) => EncodeArray(s, (V4i[])o);

        private static readonly Action<BinaryWriter, object> EncodeV2l =
            (s, o) => { var x = (V2l)o; s.Write(x.X); s.Write(x.Y); };
        private static readonly Action<BinaryWriter, object> EncodeV2lArray =
            (s, o) => EncodeArray(s, (V2l[])o);
        private static readonly Action<BinaryWriter, object> EncodeV3l =
            (s, o) => { var x = (V3l)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); };
        private static readonly Action<BinaryWriter, object> EncodeV3lArray =
            (s, o) => EncodeArray(s, (V3l[])o);
        private static readonly Action<BinaryWriter, object> EncodeV4l =
            (s, o) => { var x = (V4l)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); s.Write(x.W); };
        private static readonly Action<BinaryWriter, object> EncodeV4lArray =
            (s, o) => EncodeArray(s, (V4l[])o);

        private static readonly Action<BinaryWriter, object> EncodeV2f =
            (s, o) => { var x = (V2f)o; s.Write(x.X); s.Write(x.Y); };
        private static readonly Action<BinaryWriter, object> EncodeV2fArray =
            (s, o) => EncodeArray(s, (V2f[])o);
        private static readonly Action<BinaryWriter, object> EncodeV3f =
            (s, o) => { var x = (V3f)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); };
        private static readonly Action<BinaryWriter, object> EncodeV3fArray =
            (s, o) => EncodeArray(s, (V3f[])o);
        private static readonly Action<BinaryWriter, object> EncodeV4f =
            (s, o) => { var x = (V4f)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); s.Write(x.W); };
        private static readonly Action<BinaryWriter, object> EncodeV4fArray =
            (s, o) => EncodeArray(s, (V4f[])o);


        private static readonly Action<BinaryWriter, object> EncodeV2d =
            (s, o) => { var x = (V2d)o; s.Write(x.X); s.Write(x.Y); };
        private static readonly Action<BinaryWriter, object> EncodeV2dArray =
            (s, o) => EncodeArray(s, (V2d[])o);
        private static readonly Action<BinaryWriter, object> EncodeV3d =
            (s, o) => { var x = (V3d)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); };
        private static readonly Action<BinaryWriter, object> EncodeV3dArray =
            (s, o) => EncodeArray(s, (V3d[])o);
        private static readonly Action<BinaryWriter, object> EncodeV4d =
            (s, o) => { var x = (V4d)o; s.Write(x.X); s.Write(x.Y); s.Write(x.Z); s.Write(x.W); };
        private static readonly Action<BinaryWriter, object> EncodeV4dArray =
            (s, o) => EncodeArray(s, (V4d[])o);


        private static readonly Action<BinaryWriter, object> EncodeBox2f =
            (s, o) => { var x = (Box2f)o; EncodeV2f(s, x.Min); EncodeV2f(s, x.Max); };
        private static readonly Action<BinaryWriter, object> EncodeBox2fArray =
            (s, o) => EncodeArray(s, (Box2f[])o);
        private static readonly Action<BinaryWriter, object> EncodeBox3f =
            (s, o) => { var x = (Box3f)o; EncodeV3f(s, x.Min); EncodeV3f(s, x.Max); };
        private static readonly Action<BinaryWriter, object> EncodeBox3fArray =
            (s, o) => EncodeArray(s, (Box3f[])o);

        private static readonly Action<BinaryWriter, object> EncodeBox2d =
            (s, o) => { var x = (Box2d)o; EncodeV2d(s, x.Min); EncodeV2d(s, x.Max); };
        private static readonly Action<BinaryWriter, object> EncodeBox2dArray =
            (s, o) => EncodeArray(s, (Box2d[])o);
        private static readonly Action<BinaryWriter, object> EncodeBox3d =
            (s, o) => { var x = (Box3d)o; EncodeV3d(s, x.Min); EncodeV3d(s, x.Max); };
        private static readonly Action<BinaryWriter, object> EncodeBox3dArray =
            (s, o) => EncodeArray(s, (Box3d[])o);


        private static readonly Action<BinaryWriter, object> EncodeM22f =
            (s, o) => { var x = (M22f)o; s.Write(x.M00); s.Write(x.M01); s.Write(x.M10); s.Write(x.M11); };
        private static readonly Action<BinaryWriter, object> EncodeM22fArray =
            (s, o) => EncodeArray(s, (M22f[])o);
        private static readonly Action<BinaryWriter, object> EncodeM33f =
            (s, o) => { var x = (M33f)o; s.Write(x.M00); s.Write(x.M01); s.Write(x.M02); s.Write(x.M10); s.Write(x.M11); s.Write(x.M12); s.Write(x.M20); s.Write(x.M21); s.Write(x.M22); };
        private static readonly Action<BinaryWriter, object> EncodeM33fArray =
            (s, o) => EncodeArray(s, (M33f[])o);
        private static readonly Action<BinaryWriter, object> EncodeM44f =
            (s, o) => { var x = (M44f)o; s.Write(x.M00); s.Write(x.M01); s.Write(x.M02); s.Write(x.M03); s.Write(x.M10); s.Write(x.M11); s.Write(x.M12); s.Write(x.M13); s.Write(x.M20); s.Write(x.M21); s.Write(x.M22); s.Write(x.M23); s.Write(x.M30); s.Write(x.M31); s.Write(x.M32); s.Write(x.M33); };
        private static readonly Action<BinaryWriter, object> EncodeM44fArray =
            (s, o) => EncodeArray(s, (M44f[])o);

        private static readonly Action<BinaryWriter, object> EncodeM22d =
            (s, o) => { var x = (M22d)o; s.Write(x.M00); s.Write(x.M01); s.Write(x.M10); s.Write(x.M11); };
        private static readonly Action<BinaryWriter, object> EncodeM22dArray =
            (s, o) => EncodeArray(s, (M22d[])o);
        private static readonly Action<BinaryWriter, object> EncodeM33d =
            (s, o) => { var x = (M33d)o; s.Write(x.M00); s.Write(x.M01); s.Write(x.M02); s.Write(x.M10); s.Write(x.M11); s.Write(x.M12); s.Write(x.M20); s.Write(x.M21); s.Write(x.M22); };
        private static readonly Action<BinaryWriter, object> EncodeM33dArray =
            (s, o) => EncodeArray(s, (M33d[])o);
        private static readonly Action<BinaryWriter, object> EncodeM44d =
            (s, o) => { var x = (M44d)o; s.Write(x.M00); s.Write(x.M01); s.Write(x.M02); s.Write(x.M03); s.Write(x.M10); s.Write(x.M11); s.Write(x.M12); s.Write(x.M13); s.Write(x.M20); s.Write(x.M21); s.Write(x.M22); s.Write(x.M23); s.Write(x.M30); s.Write(x.M31); s.Write(x.M32); s.Write(x.M33); };
        private static readonly Action<BinaryWriter, object> EncodeM44dArray =
            (s, o) => EncodeArray(s, (M44d[])o);


        private static readonly Action<BinaryWriter, object> EncodeC3b =
            (s, o) => { var x = (C3b)o; s.Write(x.R); s.Write(x.G); s.Write(x.B); };
        private static readonly Action<BinaryWriter, object> EncodeC3bArray =
            (s, o) => EncodeArray(s, (C3b[])o);
        private static readonly Action<BinaryWriter, object> EncodeC4b =
            (s, o) => { var x = (C4b)o; s.Write(x.R); s.Write(x.G); s.Write(x.B); s.Write(x.A); };
        private static readonly Action<BinaryWriter, object> EncodeC4bArray =
            (s, o) => EncodeArray(s, (C4b[])o);
        private static readonly Action<BinaryWriter, object> EncodeC3f =
            (s, o) => { var x = (C3f)o; s.Write(x.R); s.Write(x.G); s.Write(x.B); };
        private static readonly Action<BinaryWriter, object> EncodeC3fArray =
            (s, o) => EncodeArray(s, (C3f[])o);
        private static readonly Action<BinaryWriter, object> EncodeC4f =
            (s, o) => { var x = (C4f)o; s.Write(x.R); s.Write(x.G); s.Write(x.B); s.Write(x.A); };
        private static readonly Action<BinaryWriter, object> EncodeC4fArray =
            (s, o) => EncodeArray(s, (C4f[])o);

        private static unsafe void EncodeArray<T>(BinaryWriter s, params T[] xs) where T : struct
        {
            var gc = GCHandle.Alloc(xs, GCHandleType.Pinned);
            var size = xs.Length * Marshal.SizeOf<T>();
            var dst = new byte[size];
            try
            {
                Marshal.Copy(gc.AddrOfPinnedObject(), dst, 0, size);
                s.Write(xs.Length);
                s.Write(dst);
            }
            finally
            {
                gc.Free();
            }
        }

        private static void Encode(BinaryWriter stream, Durable.Def def, object x)
        {
            if (def.Type != Durable.Primitives.Unit.Id)
            {
                if (s_encoders.TryGetValue(def.Type, out var encoder))
                {
                    EncodeGuid(stream, def.Id);
                    ((Action<BinaryWriter, object>)encoder)(stream, x);
                }
                else
                {
                    var unknownDef = Durable.Get(def.Type);
                    throw new InvalidOperationException(
                        $"Unknown definition {unknownDef}. Invariant 4619ee6d-81e7-4212-b813-bfe2178d906f."
                        );
                }
            }
            else
            {
                if (s_encoders.TryGetValue(def.Id, out var encoder))
                {
                    ((Action<BinaryWriter, object>)encoder)(stream, x);
                }
                else
                {
                    var unknownDef = Durable.Get(def.Id);
                    throw new InvalidOperationException(
                        $"Unknown definition {unknownDef}. Invariant 0de99f99-a339-421b-ac5d-4f55b71342de."
                        );
                }
            }
        }

        /// <summary>
        /// Serializes value x to byte array. 
        /// Can be deserialized with Deserialize.
        /// </summary>
        public static byte[] Serialize<T>(Durable.Def def, T x)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            if (def.Type == Durable.Primitives.Unit.Id)
            {
                // encode type of primitive value, so we can roundtrip with Deserialize
                // (since it is not encoded by the Encode function called below)
                EncodeGuid(bw, def.Id);
            }

            Encode(bw, def, x);
            bw.Flush();
            return ms.ToArray();
        }

        /// <summary>
        /// Serializes value x to stream. 
        /// Can be deserialized with Deserialize.
        /// </summary>
        public static void Serialize<T>(BinaryWriter stream, Durable.Def def, T x)
        {
            if (def.Type == Durable.Primitives.Unit.Id)
            {
                // encode type of primitive value, so we can roundtrip with Deserialize
                // (since it is not encoded by the Encode function called below)
                EncodeGuid(stream, def.Id);
            }

            Serialize(stream, def, x);
        }

        #endregion

        #region Decode

        private static readonly Func<BinaryReader, object> DecodeDurableMapWithoutHeader =
            s =>
            {
                var count = s.ReadInt32();
                var entries = new KeyValuePair<Durable.Def, object>[count];
                for (var i = 0; i < count; i++)
                {
                    var e = Decode(s);
                    entries[i] = new KeyValuePair<Durable.Def, object>(e.Item1, e.Item2);
                }
                return ImmutableDictionary.CreateRange(entries);
            };


        private static void SkipToNextMultipleOf(this Stream s, int numberOfBytes)
        {
            var m = (int)(s.Position % numberOfBytes);
            if (m > 0) s.Seek(numberOfBytes - m, SeekOrigin.Current);
        }
        private static readonly Func<BinaryReader, object> DecodeDurableMap16WithoutHeader =
            br =>
            {
                var s = br.BaseStream;

                var count = br.ReadInt32();
                s.SkipToNextMultipleOf(16);
#if DEBUG
            if (s.Position % 16 != 0) throw new Exception("Invariant a28bd69e-9807-4a66-971c-c3cfa46eebad.");
#endif

            var map = ImmutableDictionary<Durable.Def, object>.Empty;
                for (var i = 0; i < count; i++)
                {
                    var (def, o) = Decode(br);
                    map = map.Add(def, o);
                    s.SkipToNextMultipleOf(16);
#if DEBUG
                if (s.Position % 16 != 0) throw new Exception("Invariant 078e73a4-b743-46f5-acc2-79c22e9a1d89.");
#endif
            }
                return map;
            };

        private static readonly Func<BinaryReader, object> DecodeGZipped =
            s =>
            {
                var uncompressedBufferSize = s.ReadInt32();
                var compressedBufferSize = s.ReadInt32();
                var compressedBuffer = s.ReadBytes(compressedBufferSize);

                var uncompressedBuffer = compressedBuffer.GZipDecompress(uncompressedBufferSize);

                using var ms = new MemoryStream(uncompressedBuffer);
                using var br = new BinaryReader(ms);

                var (def, o) = Decode(br);
                return new DurableGZipped(def, o);
            };

        private static readonly Func<BinaryReader, object> DecodeGuid = s => new Guid(s.ReadBytes(16));

        private static readonly Func<BinaryReader, object> DecodeStringUtf8 = s => Encoding.UTF8.GetString(DecodeArray<byte>(s));
        private static readonly Func<BinaryReader, object> DecodeStringUtf8Array = s =>
        {
            var count = s.ReadInt32();
            var xs = new string[count];
            for (var i = 0; i < count; i++)
                xs[i] = Encoding.UTF8.GetString(DecodeArray<byte>(s));
            return xs;
        };

        private static readonly Func<BinaryReader, object> DecodeInt8 = s => s.ReadSByte();
        private static readonly Func<BinaryReader, object> DecodeInt8Array = s => DecodeArray<sbyte>(s);

        private static readonly Func<BinaryReader, object> DecodeUInt8 = s => s.ReadByte();
        private static readonly Func<BinaryReader, object> DecodeUInt8Array = s => DecodeArray<byte>(s);

        private static readonly Func<BinaryReader, object> DecodeInt16 = s => s.ReadInt16();
        private static readonly Func<BinaryReader, object> DecodeInt16Array = s => DecodeArray<short>(s);

        private static readonly Func<BinaryReader, object> DecodeUInt16 = s => s.ReadUInt16();
        private static readonly Func<BinaryReader, object> DecodeUInt16Array = s => DecodeArray<ushort>(s);

        private static readonly Func<BinaryReader, object> DecodeInt32 = s => s.ReadInt32();
        private static readonly Func<BinaryReader, object> DecodeInt32Array = s => DecodeArray<int>(s);

        private static readonly Func<BinaryReader, object> DecodeUInt32 = s => s.ReadUInt32();
        private static readonly Func<BinaryReader, object> DecodeUInt32Array = s => DecodeArray<uint>(s);

        private static readonly Func<BinaryReader, object> DecodeInt64 = s => s.ReadInt64();
        private static readonly Func<BinaryReader, object> DecodeInt64Array = s => DecodeArray<long>(s);

        private static readonly Func<BinaryReader, object> DecodeUInt64 = s => s.ReadUInt64();
        private static readonly Func<BinaryReader, object> DecodeUInt64Array = s => DecodeArray<ulong>(s);

        private static readonly Func<BinaryReader, object> DecodeFloat32 = s => s.ReadSingle();
        private static readonly Func<BinaryReader, object> DecodeFloat32Array = s => DecodeArray<float>(s);

        private static readonly Func<BinaryReader, object> DecodeFloat64 = s => s.ReadDouble();
        private static readonly Func<BinaryReader, object> DecodeFloat64Array = s => DecodeArray<double>(s);

        private static readonly Func<BinaryReader, object> DecodeCell = s => new Cell(s.ReadInt64(), s.ReadInt64(), s.ReadInt64(), s.ReadInt32());
        private static readonly Func<BinaryReader, object> DecodeCellArray = s => DecodeArray<Cell>(s);

        private static readonly Func<BinaryReader, object> DecodeCell2d = s => new Cell2d(s.ReadInt64(), s.ReadInt64(), s.ReadInt32());
        private static readonly Func<BinaryReader, object> DecodeCell2dArray = s => DecodeArray<Cell2d>(s);

        private static readonly Func<BinaryReader, object> DecodeV2i = s => new V2i(s.ReadInt32(), s.ReadInt32());
        private static readonly Func<BinaryReader, object> DecodeV2iArray = s => DecodeArray<V2i>(s);
        private static readonly Func<BinaryReader, object> DecodeV3i = s => new V3i(s.ReadInt32(), s.ReadInt32(), s.ReadInt32());
        private static readonly Func<BinaryReader, object> DecodeV3iArray = s => DecodeArray<V3i>(s);
        private static readonly Func<BinaryReader, object> DecodeV4i = s => new V4i(s.ReadInt32(), s.ReadInt32(), s.ReadInt32(), s.ReadInt32());
        private static readonly Func<BinaryReader, object> DecodeV4iArray = s => DecodeArray<V4i>(s);

        private static readonly Func<BinaryReader, object> DecodeV2l = s => new V2l(s.ReadInt64(), s.ReadInt64());
        private static readonly Func<BinaryReader, object> DecodeV2lArray = s => DecodeArray<V2l>(s);
        private static readonly Func<BinaryReader, object> DecodeV3l = s => new V3l(s.ReadInt64(), s.ReadInt64(), s.ReadInt64());
        private static readonly Func<BinaryReader, object> DecodeV3lArray = s => DecodeArray<V3l>(s);
        private static readonly Func<BinaryReader, object> DecodeV4l = s => new V4l(s.ReadInt64(), s.ReadInt64(), s.ReadInt64(), s.ReadInt64());
        private static readonly Func<BinaryReader, object> DecodeV4lArray = s => DecodeArray<V4l>(s);

        private static readonly Func<BinaryReader, object> DecodeV2f = s => new V2f(s.ReadSingle(), s.ReadSingle());
        private static readonly Func<BinaryReader, object> DecodeV2fArray = s => DecodeArray<V2f>(s);
        private static readonly Func<BinaryReader, object> DecodeV3f = s => new V3f(s.ReadSingle(), s.ReadSingle(), s.ReadSingle());
        private static readonly Func<BinaryReader, object> DecodeV3fArray = s => DecodeArray<V3f>(s);
        private static readonly Func<BinaryReader, object> DecodeV4f = s => new V4f(s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle());
        private static readonly Func<BinaryReader, object> DecodeV4fArray = s => DecodeArray<V4f>(s);

        private static readonly Func<BinaryReader, object> DecodeV2d = s => new V2d(s.ReadDouble(), s.ReadDouble());
        private static readonly Func<BinaryReader, object> DecodeV2dArray = s => DecodeArray<V2d>(s);
        private static readonly Func<BinaryReader, object> DecodeV3d = s => new V3d(s.ReadDouble(), s.ReadDouble(), s.ReadDouble());
        private static readonly Func<BinaryReader, object> DecodeV3dArray = s => DecodeArray<V3d>(s);
        private static readonly Func<BinaryReader, object> DecodeV4d = s => new V4d(s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble());
        private static readonly Func<BinaryReader, object> DecodeV4dArray = s => DecodeArray<V4d>(s);

        private static readonly Func<BinaryReader, object> DecodeBox2f = s => new Box2f((V2f)DecodeV2f(s), (V2f)DecodeV2f(s));
        private static readonly Func<BinaryReader, object> DecodeBox2fArray = s => DecodeArray<Box2f>(s);
        private static readonly Func<BinaryReader, object> DecodeBox2d = s => new Box2d((V2d)DecodeV2d(s), (V2d)DecodeV2d(s));
        private static readonly Func<BinaryReader, object> DecodeBox2dArray = s => DecodeArray<Box2d>(s);

        private static readonly Func<BinaryReader, object> DecodeBox3f = s => new Box3f((V3f)DecodeV3f(s), (V3f)DecodeV3f(s));
        private static readonly Func<BinaryReader, object> DecodeBox3fArray = s => DecodeArray<Box3f>(s);
        private static readonly Func<BinaryReader, object> DecodeBox3d = s => new Box3d((V3d)DecodeV3d(s), (V3d)DecodeV3d(s));
        private static readonly Func<BinaryReader, object> DecodeBox3dArray = s => DecodeArray<Box3d>(s);

        private static readonly Func<BinaryReader, object> DecodeM22f = s => new M22f(s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle());
        private static readonly Func<BinaryReader, object> DecodeM22fArray = s => DecodeArray<M22f>(s);
        private static readonly Func<BinaryReader, object> DecodeM33f = s => new M33f(s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle());
        private static readonly Func<BinaryReader, object> DecodeM33fArray = s => DecodeArray<M33f>(s);
        private static readonly Func<BinaryReader, object> DecodeM44f = s => new M44f(s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle());
        private static readonly Func<BinaryReader, object> DecodeM44fArray = s => DecodeArray<M44f>(s);

        private static readonly Func<BinaryReader, object> DecodeM22d = s => new M22d(s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble());
        private static readonly Func<BinaryReader, object> DecodeM22dArray = s => DecodeArray<M22d>(s);
        private static readonly Func<BinaryReader, object> DecodeM33d = s => new M33d(s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble());
        private static readonly Func<BinaryReader, object> DecodeM33dArray = s => DecodeArray<M33d>(s);
        private static readonly Func<BinaryReader, object> DecodeM44d = s => new M44d(s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble(), s.ReadDouble());
        private static readonly Func<BinaryReader, object> DecodeM44dArray = s => DecodeArray<M44d>(s);

        private static readonly Func<BinaryReader, object> DecodeC3b = s => new C3b(s.ReadByte(), s.ReadByte(), s.ReadByte());
        private static readonly Func<BinaryReader, object> DecodeC3bArray = s => DecodeArray<C3b>(s);

        private static readonly Func<BinaryReader, object> DecodeC4b = s => new C4b(s.ReadByte(), s.ReadByte(), s.ReadByte(), s.ReadByte());
        private static readonly Func<BinaryReader, object> DecodeC4bArray = s => DecodeArray<C4b>(s);

        private static readonly Func<BinaryReader, object> DecodeC3f = s => new C3f(s.ReadSingle(), s.ReadSingle(), s.ReadSingle());
        private static readonly Func<BinaryReader, object> DecodeC3fArray = s => DecodeArray<C3f>(s);

        private static readonly Func<BinaryReader, object> DecodeC4f = s => new C4f(s.ReadSingle(), s.ReadSingle(), s.ReadSingle(), s.ReadSingle());
        private static readonly Func<BinaryReader, object> DecodeC4fArray = s => DecodeArray<C4f>(s);

        private static unsafe T[] DecodeArray<T>(BinaryReader s) where T : struct
        {
            var count = s.ReadInt32();
            var size = count * Marshal.SizeOf<T>();
            var buffer = s.ReadBytes(size);
            var xs = new T[count];
            var gc = GCHandle.Alloc(xs, GCHandleType.Pinned);
            try
            {
                Marshal.Copy(buffer, 0, gc.AddrOfPinnedObject(), size);
                return xs;
            }
            finally
            {
                gc.Free();
            }
        }

        private static readonly Func<BinaryReader, object> DecodeGuidArray = s => DecodeArray<Guid>(s);

        private static (Durable.Def, object) Decode(BinaryReader stream)
        {
            var key = (Guid)DecodeGuid(stream);
            if (!Durable.TryGet(key, out var def))
            {
                stream.BaseStream.Position -= 16;
                def = Durable.Get(Durable.Primitives.DurableMap.Id);
            }

            if (def.Type != Durable.Primitives.Unit.Id)
            {
                if (s_decoders.TryGetValue(def.Type, out var decoder))
                {
                    var o = ((Func<BinaryReader, object>)decoder)(stream);
                    return (def, o);
                }
                else
                {
                    var unknownDef = Durable.Get(def.Type);
                    throw new InvalidOperationException(
                        $"Unknown definition {unknownDef}. Invariant bcdd79e9-06dd-42d1-9906-9974d49e8dd8."
                        );
                }
            }
            else
            {
                if (s_decoders.TryGetValue(def.Id, out var decoder))
                {
                    var o = ((Func<BinaryReader, object>)decoder)(stream);
                    return (def, o);
                }
                else
                {
                    var unknownDef = Durable.Get(def.Id);
                    throw new InvalidOperationException(
                        $"Unknown definition {unknownDef}. Invariant 9d4570d5-b9ef-404f-a247-9b571cd4f1f6."
                        );
                }
            }
        }

        /// <summary>
        /// Deserializes value from byte array.
        /// </summary>
        public static (Durable.Def, object) Deserialize(byte[] buffer)
        {
            using var ms = new MemoryStream(buffer);
            using var br = new BinaryReader(ms);
            return Decode(br);
        }

        /// <summary>
        /// Deserializes value from byte array.
        /// </summary>
        public static (Durable.Def, object) Deserialize(BinaryReader stream)
            => Decode(stream);

        #endregion
    }
}

#endif