#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
sb0t Pyxis - Hashlink checker (TCP / UDP)

Comprobá si una sala está online dado un hashlink (arlnk://...) o IP+puerto.

Uso (desde la carpeta tools):
    python hashlink-check.py "arlnk://..."
    python hashlink-check.py 65.21.240.33 54321
    python hashlink-check.py --timeout 3 "arlnk://..."

Solo usa la librería estándar de Python (sin dependencias).
"""

import sys
import zlib
import base64
import socket
import argparse

# Consola pueda imprimir UTF-8 (nombres/temas con acentos o emojis)
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

HASH_IV = 28435
WIDTH = 60


def d67(data, b=HASH_IV):
    """Descifrado byte a byte (espejo de core.Hashlink.d67)."""
    out = bytearray(len(data))
    for i in range(len(data)):
        out[i] = data[i] ^ ((b >> 8) & 0xFF)
        b = ((b + data[i]) * 23219 + 36126) & 0xFFFF
    return bytes(out)


def decode_hashlink(hashlink):
    """Decodifica un arlnk://..., devuelve (ip, port, name)."""
    s = hashlink.strip()
    if s.lower().startswith("arlnk://"):
        s = s[8:]

    if s.upper().startswith("CHATROOM:"):
        s = s[9:]
        ip, rest = s.split(":", 1)
        port, name = rest.split("|", 1)
        return ip, int(port), name

    raw = base64.b64decode(s)
    clear = zlib.decompress(d67(raw))
    off = 32
    ip = ".".join(str(b) for b in clear[off:off + 4])
    off += 4
    port = int.from_bytes(clear[off:off + 2], "little")
    off += 2
    off += 4  # salta IP repetida
    end = clear.find(0, off)
    if end == -1:
        end = len(clear)
    name = clear[off:end].decode("utf-8", errors="replace")
    return ip, port, name


def check_tcp(ip, port, timeout):
    """Puerto TCP abierto/cerrado directamente."""
    try:
        with socket.create_connection((ip, port), timeout=timeout):
            return True, None
    except socket.timeout:
        return False, "timeout (sin respuesta)"  # filtrado / cerrado silencioso
    except ConnectionRefusedError:
        return False, "CONNECTION REFUSED -> puerto cerrado"
    except OSError as e:
        return False, str(e)


OP_SERVERLIST_SENDINFO = 0x02
OP_SERVERLIST_ACKINFO = 0x03


def read_udp_string(buf, off):
    """Lee una string del formato Ares: UInt16 LE largo + UTF8. Devuelve (texto, nuevo_offset)."""
    if off + 2 > len(buf):
        return "", off
    length = int.from_bytes(buf[off:off + 2], "little")
    off += 2
    end = off + length
    text = buf[off:end].decode("utf-8", errors="replace")
    return text, end


def parse_ackinfo(data):
    """Parsea el payload de OP_SERVERLIST_ACKINFO (sin el byte de operación)."""
    info = {}
    off = 0
    if len(data) < 4:
        return None
    info["port"] = int.from_bytes(data[off:off + 2], "little"); off += 2
    info["users"] = int.from_bytes(data[off:off + 2], "little"); off += 2
    info["name"], off = read_udp_string(data, off)
    info["topic"], off = read_udp_string(data, off)
    if off < len(data):
        info["language"] = data[off]; off += 1
    if off < len(data):
        info["version"], off = read_udp_string(data, off)
    # Lista de nodos: 1 byte (cantidad) + N * (IP 4 bytes + puerto UInt16 LE)
    if off < len(data):
        n_nodes = data[off]; off += 1
        info["nodes"] = []
        for _ in range(n_nodes):
            if off + 6 > len(data):
                break
            nip = ".".join(str(b) for b in data[off:off + 4]); off += 4
            nport = int.from_bytes(data[off:off + 2], "little"); off += 2
            info["nodes"].append((nip, nport))
    return info


def check_udp(ip, port, timeout):
    """
    UDP: envía el "fetch" OP_SERVERLIST_SENDINFO (0x02) que el server Ares
    responde con OP_SERVERLIST_ACKINFO (0x03) trajo la info de la sala.
    abierto = el server respondió al fetch.
    """
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.settimeout(timeout)
    try:
        # 1 byte = la operación sendinfo
        s.sendto(bytes([OP_SERVERLIST_SENDINFO]), (ip, port))
        try:
            data, _ = s.recvfrom(65535)
            if len(data) == 0:
                return None, "respuesta vacía", None
            op = data[0]
            if op != OP_SERVERLIST_ACKINFO:
                return True, f"respuesta op={op} (no ACKINFO)", None
            info = parse_ackinfo(data[1:])
            return True, f"respuesta {len(data)} bytes", info
        except socket.timeout:
            return None, "sin respuesta (timeout) - UDP no habilitado o filtrado", None
        except ConnectionRefusedError:
            return False, "ICMP puerto inalcanzable -> UDP cerrado", None
    except OSError as e:
        return False, str(e), None
    finally:
        s.close()


def banner(ip, port, name):
    print("=" * WIDTH)
    print("  sb0t Pyxis - Hashlink status checker")
    print("=" * WIDTH)
    print(f"  IP:       {ip}")
    print(f"  Puerto:   {port}")
    if name:
        print(f"  Nombre:   {name}")
    print("-" * WIDTH)


def main():
    p = argparse.ArgumentParser(description="Comprueba si una sala (hashlink) está online: TCP y UDP.")
    p.add_argument("hashlink", help="Hashlink arlnk://... o dirección IP")
    p.add_argument("port", nargs="?", type=int, default=None,
                   help="Puerto (solo si el primer argumento es una IP, no un hashlink)")
    p.add_argument("--timeout", "-t", type=float, default=3.0,
                   help="Timeout en segundos por cada check (default 3)")
    args = p.parse_args()

    if args.port is not None:
        ip, port, name = args.hashlink, args.port, None
    else:
        try:
            ip, port, name = decode_hashlink(args.hashlink)
        except Exception as e:
            print(f"Error decodificando hashlink: {e}")
            sys.exit(2)

    banner(ip, port, name)

    # TCP
    tcp_ok, tcp_note = check_tcp(ip, port, args.timeout)
    if tcp_ok:
        print(f"  TCP:      {'ABIERTO' :<14} (servidor respondiendo)")
    else:
        print(f"  TCP:      {'CERRADO' :<14} {tcp_note}")
    if not tcp_ok:
        print("-" * WIDTH)
        print("  TCP cerrado -> la sala no está online en este puerto.")
        sys.exit(1)

    # UDP
    udp_ok, udp_note, udp_info = check_udp(ip, port, args.timeout)
    if udp_ok:
        print(f"  UDP:      {'ABIERTO' :<14} {udp_note}")
        if udp_info:
            if "users" in udp_info:
                print(f"  Usuarios: {udp_info['users']}")
            if udp_info.get("name"):
                print(f"  Sala:     {udp_info['name']}")
            if udp_info.get("topic"):
                print(f"  Tema:     {udp_info['topic']}")
            if udp_info.get("version"):
                print(f"  Versión:  {udp_info['version']}")
            # Nodos (servidores de red conocidos) -> clave para aparecer en el browser
            nodes = udp_info.get("nodes")
            if nodes is not None:
                print(f"  Nodos:    {len(nodes)}")
                for n_ip, n_port in nodes:
                    print(f"            - {n_ip}:{n_port}")
                if len(nodes) == 0:
                    print("  >> La sala NO conoce servidores de red (0 nodos).")
                    print("  >> No puede publicarse en el room browser. Revisá la config UDP/nodes.")
                elif len(nodes) < 3:
                    print(f"  >> Pocos nodos ({len(nodes)}). La sala está en la red pero con poca redundancia.")
                else:
                    print("  >> OK: la sala tiene nodos suficientes para el room browser.")
    elif udp_ok is None:
        print(f"  UDP:      {'? (timeout)' :<14} {udp_note}")
        print("  >> No se pudo evaluar nodos de la sala (UDP sin responder).")
    else:
        print(f"  UDP:      {'CERRADO' :<14} {udp_note}")

    print("=" * WIDTH)


if __name__ == "__main__":
    main()
