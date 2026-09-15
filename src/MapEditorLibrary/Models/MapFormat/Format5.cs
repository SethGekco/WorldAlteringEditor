using CNCMaps.FileFormats.VirtualFileSystem;

// Intentionally has different namespace from folder structure due to being imported code
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace CNCMaps.FileFormats.Encodings;
#pragma warning restore IDE0130 // Namespace does not match folder structure

// basec on code from XCC mixer

public class Format5
{
    public static unsafe uint DecodeInto(byte[] src, byte[] dest, int format = 5)
    {
        fixed (byte* pr = src, pw = dest)
        {
            byte* r = pr, w = pw;
            byte* w_end = w + dest.Length;
            byte* r_end = pr + src.Length;

            while (w < w_end)
            {
                // stride-2048 fork: also bound the read cursor. When the destination buffer is larger
                // than the encoded data (e.g. a vanilla 512-stride overlay pack decoded into an
                // extended-stride buffer), the loop previously ran past the end of the source array
                // and read undefined memory as block headers.
                if (r + 4 > r_end)
                    break;

                ushort size_in = *(ushort*)r;
                r += 2;
                uint size_out = *(ushort*)r;
                r += 2;

                if (r + size_in > r_end)
                    break;

                if (size_in == 0 || size_out == 0)
                    break;

                if (format == 80)
                    Format80.DecodeInto(r, w);
                else
                    MiniLZO.MiniLZO.Decompress(r, size_in, w, ref size_out);
                r += size_in;
                w += size_out;
            }
            return (uint)(w - pw);
        }
    }

    public static byte[] EncodeSection(byte[] s)
    {
        return MiniLZO.MiniLZO.Compress(s);
    }

    public static byte[] Encode(byte[] source, int format)
    {
        var dest = new byte[source.Length * 2];
        var src = new MemoryFile(source);

        int w = 0;
        while (!src.Eof)
        {
            var cb_in = (short)Math.Min(src.Remaining, 8192);
            var chunk_in = src.Read(cb_in);
            var chunk_out = format == 80 ? Format80.Encode(chunk_in) : EncodeSection(chunk_in);
            uint cb_out = (ushort)chunk_out.Length;

            Array.Copy(BitConverter.GetBytes(cb_out), 0, dest, w, 2);
            w += 2;
            Array.Copy(BitConverter.GetBytes(cb_in), 0, dest, w, 2);
            w += 2;
            Array.Copy(chunk_out, 0, dest, w, chunk_out.Length);
            w += chunk_out.Length;
        }
        Array.Resize(ref dest, w);
        return dest;
    }
}