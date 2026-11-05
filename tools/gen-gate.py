#!/usr/bin/env python3
"""
重新生成 MultiInstanceGate 的常量（换验证码时用）。

用法：
    python tools/gen-gate.py Qy123456

输出的 SeedParts / HashParts 直接替换 MultiInstanceGate.cs 里对应的数组即可。
掩码和段序保持不变，避免改动还原逻辑。
"""
import base64
import hashlib
import sys

# 与 MultiInstanceGate.cs 保持一致
SEED_MASK = [0x9E, 0x37, 0xC5, 0x6B, 0x12, 0xE8, 0x4D, 0xA3]
SEED_ORDER = [3, 7, 0, 5, 2, 6, 1, 4]
HASH_MASK = [0x5A, 0xA5, 0x3C, 0xC3]
HASH_ORDER = [2, 0, 3, 1]
ITERATIONS = 600_000


def pack(raw: bytes, mask, order):
    """
    把明文按 mask/order 打包成 base64 段数组。

    Rebuild 的还原逻辑：
        buffer[i*8 + j] = parts[order[i]][j] ^ mask[i]
    所以打包时：
        parts[order[i]] = base64(buffer[i*8 : i*8+8] ^ mask[i])
    """
    segments = len(raw) // 8
    parts = [None] * segments
    for i in range(segments):
        seg = bytes(raw[i * 8 + j] ^ mask[i] for j in range(8))
        parts[order[i]] = base64.b64encode(seg).decode()
    return parts


def main():
    if len(sys.argv) < 2:
        print("用法: python tools/gen-gate.py <验证码>")
        return 1

    code = sys.argv[1]
    # 种子是固定值（改动它等于换掉整个门禁），沿用当前已部署的种子
    seed_b64 = (
        "5env4yPIwQ8=QDhLRV7yEWI=xUJOpX25tM8=eURYO+sXOx0="
        "12Fte33JO+8=ilr3oztvn60=iDn422fdopI=6Jfft3+fP3k="
    )
    # 上面是「打包后」的形态，需要先还原出真实种子
    parts = [seed_b64[i * 12:(i + 1) * 12] for i in range(8)]
    seed = bytearray(64)
    for i in range(8):
        seg = base64.b64decode(parts[SEED_ORDER[i]])
        for j in range(8):
            seed[i * 8 + j] = seg[j] ^ SEED_MASK[i]

    # PBKDF2-SHA256
    dk = hashlib.pbkdf2_hmac("sha256", code.encode(), bytes(seed), ITERATIONS, 32)

    seed_parts = pack(bytes(seed), SEED_MASK, SEED_ORDER)
    hash_parts = pack(dk, HASH_MASK, HASH_ORDER)

    print(f"验证码: {code}")
    print()
    print("SeedParts:")
    for i in range(0, len(seed_parts), 4):
        print("        " + ", ".join(f'"{p}"' for p in seed_parts[i:i + 4]) + ",")
    print()
    print("HashParts:")
    print("        " + ", ".join(f'"{p}"' for p in hash_parts) + ",")
    return 0


if __name__ == "__main__":
    sys.exit(main())
