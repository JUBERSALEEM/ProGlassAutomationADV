# Pure-Python PNG (RGBA) -> multi-size ICO builder.
# No external deps (uses zlib/struct from stdlib).
# Decodes the source PNG, nearest-neighbor downsamples to icon sizes,
# re-encodes each as PNG, and packs them into a single .ico.
import struct, zlib, sys

def read_png(path):
    with open(path, 'rb') as f:
        data = f.read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n', 'not a png'
    pos = 8
    idat = b''
    w = h = bitdepth = colortype = None
    plte = None
    while pos < len(data):
        length = struct.unpack('>I', data[pos:pos+4])[0]
        ctype = data[pos+4:pos+8]
        chunk = data[pos+8:pos+8+length]
        if ctype == b'IHDR':
            w, h, bitdepth, colortype = struct.unpack('>IIBB', chunk[:10])
        elif ctype == b'IDAT':
            idat += chunk
        elif ctype == b'IEND':
            break
        pos += 12 + length
    assert bitdepth == 8, f'unsupported bitdepth {bitdepth}'
    # channels by color type
    ch = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[colortype]
    raw = zlib.decompress(idat)
    stride = w * ch
    # unfilter scanlines
    out = bytearray()
    prev = bytearray(stride)
    i = 0
    for y in range(h):
        ft = raw[i]; i += 1
        line = bytearray(raw[i:i+stride]); i += stride
        if ft == 1:  # Sub
            for x in range(ch, stride):
                line[x] = (line[x] + line[x-ch]) & 0xFF
        elif ft == 2:  # Up
            for x in range(stride):
                line[x] = (line[x] + prev[x]) & 0xFF
        elif ft == 3:  # Average
            for x in range(stride):
                a = line[x-ch] if x >= ch else 0
                line[x] = (line[x] + ((a + prev[x]) >> 1)) & 0xFF
        elif ft == 4:  # Paeth
            for x in range(stride):
                a = line[x-ch] if x >= ch else 0
                b = prev[x]
                c = prev[x-ch] if x >= ch else 0
                p = a + b - c
                pa, pb, pc = abs(p-a), abs(p-b), abs(p-c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[x] = (line[x] + pr) & 0xFF
        out += line
        prev = line
    # convert to RGBA
    px = []
    for idx in range(0, len(out), ch):
        if ch >= 3:
            r, g, b = out[idx], out[idx+1], out[idx+2]
            a = out[idx+3] if ch == 4 else 255
        elif ch == 2:
            r = g = b = out[idx]; a = out[idx+1]
        else:
            r = g = b = out[idx]; a = 255
        px.append((r, g, b, a))
    return w, h, px

def downsample(px, w, h, nw, nh):
    res = []
    for y in range(nh):
        sy = y * h // nh
        for x in range(nw):
            sx = x * w // nw
            res.append(px[sy*w + sx])
    return res

def write_png(w, h, px):
    def chunk(ctype, data):
        c = ctype + data
        return struct.pack('>I', len(data)) + c + struct.pack('>I', zlib.crc32(c) & 0xFFFFFFFF)
    ihdr = struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)
    raw = bytearray()
    for y in range(h):
        raw.append(0)  # filter None
        for x in range(w):
            r, g, b, a = px[y*w + x]
            raw += bytes((r, g, b, a))
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', ihdr)
            + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
            + chunk(b'IEND', b''))

def build_ico(sizes, src_png, out_ico):
    w, h, px = read_png(src_png)
    entries = []
    blobs = []
    for s in sizes:
        spx = downsample(px, w, h, s, s)
        blobs.append(write_png(s, s, spx))
    # ICO header
    out = struct.pack('<HHH', 0, 1, len(sizes))
    offset = 6 + 16 * len(sizes)
    for i, s in enumerate(sizes):
        bw = 0 if s >= 256 else s
        bh = 0 if s >= 256 else s
        out += struct.pack('<BBBBHHII', bw, bh, 0, 0, 1, 32,
                           len(blobs[i]), offset)
        offset += len(blobs[i])
    out += b''.join(blobs)
    with open(out_ico, 'wb') as f:
        f.write(out)
    print(f'wrote {out_ico} ({len(out)} bytes) with sizes {sizes}')

if __name__ == '__main__':
    src = sys.argv[1] if len(sys.argv) > 1 else 'Resources/proglass_icon_256.png'
    dst = sys.argv[2] if len(sys.argv) > 2 else 'favicon.ico'
    build_ico([256, 64, 48, 32, 16], src, dst)
