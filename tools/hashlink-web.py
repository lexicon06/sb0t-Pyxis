#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
sb0t Pyxis - Hashlink checker WEB

Frontend web para el checker de hashlink: poné IP + puerto y/o un hashlink
(opcional) y te muestra TCP, UDP, usuarios, sala, tema, versión y nodos.

Solo usa la librería estándar de Python. Reusa la lógica de hashlink-check.py.

Uso:
    python hashlink-web.py                 # http://127.0.0.1:8080
    python hashlink-web.py --port 9000
    python hashlink-web.py --host 0.0.0.0 --port 8080
"""

import os
import re
import sys
import json
import argparse
import importlib.util
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs

HERE = os.path.dirname(os.path.abspath(__file__))

# ---- Cargar el checker existente (hashlink-check.py tiene guion, se carga por ruta)
_spec = importlib.util.spec_from_file_location(
    "hashlink_check", os.path.join(HERE, "hashlink-check.py"))
hc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(hc)

COLOR_RE = re.compile(r"(\x03|\x05)[0-9]{2}")
CTRL_RE = re.compile(r"[\x00-\x1f\x7f]")


def strip_colors(s):
    if not s:
        return ""
    s = COLOR_RE.sub("", s)
    s = CTRL_RE.sub("", s)
    return s.strip()


def do_check(ip, port, timeout):
    """Corre TCP + UDP y arma el resultado serializable."""
    result = {
        "ip": ip,
        "port": port,
        "tcp": {"open": False, "note": ""},
        "udp": {"state": "closed", "note": ""},
        "info": None,
        "node_status": None,
        "error": None,
    }

    # TCP
    tcp_ok, tcp_note = hc.check_tcp(ip, port, timeout)
    result["tcp"] = {"open": bool(tcp_ok), "note": tcp_note or ""}

    if not tcp_ok:
        return result

    # UDP
    udp_ok, udp_note, udp_info = hc.check_udp(ip, port, timeout)

    if udp_ok is None:
        result["udp"] = {"state": "timeout", "note": udp_note or ""}
    elif udp_ok:
        result["udp"] = {"state": "open", "note": udp_note or ""}
    else:
        result["udp"] = {"state": "closed", "note": udp_note or ""}

    if udp_info:
        nodes = udp_info.get("nodes")
        info = {
            "users": udp_info.get("users"),
            "name": udp_info.get("name", ""),
            "topic": strip_colors(udp_info.get("topic", "")),
            "version": udp_info.get("version", ""),
            "language": udp_info.get("language"),
            "nodes": [[n[0], n[1]] for n in nodes] if nodes is not None else None,
        }
        result["info"] = info

        if nodes is not None:
            if len(nodes) == 0:
                result["node_status"] = "none"
            elif len(nodes) < 3:
                result["node_status"] = "few"
            else:
                result["node_status"] = "ok"

    return result


PAGE = r"""<!DOCTYPE html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>sb0t Pyxis - Hashlink Checker</title>
<style>
  :root{
    --bg:#0f1116; --card:#171a21; --card2:#1e222b; --line:#2a2f3a;
    --txt:#e7ebf2; --mut:#8b93a3; --acc:#4f8cff; --ok:#2ecc71; --bad:#ff5c5c; --warn:#ffb020;
  }
  *{box-sizing:border-box}
  body{margin:0;font-family:system-ui,-apple-system,Segoe UI,Roboto,Arial,sans-serif;
       background:radial-gradient(1200px 600px at 20% -10%,#1a2233,#0f1116);color:var(--txt);min-height:100vh}
  .wrap{max-width:880px;margin:0 auto;padding:32px 18px 60px}
  h1{font-size:22px;margin:0 0 4px}
  .sub{color:var(--mut);font-size:13px;margin-bottom:22px}
  .card{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:18px;margin-bottom:16px}
  label{display:block;font-size:12px;color:var(--mut);margin:0 0 6px;text-transform:uppercase;letter-spacing:.04em}
  input{width:100%;background:var(--card2);border:1px solid var(--line);border-radius:10px;color:var(--txt);
        padding:11px 12px;font-size:14px;outline:none}
  input:focus{border-color:var(--acc)}
  .row{display:flex;gap:12px;flex-wrap:wrap}
  .row .col{flex:1;min-width:160px}
  button{margin-top:14px;width:100%;background:var(--acc);color:#fff;border:0;border-radius:10px;
         padding:12px;font-size:15px;font-weight:600;cursor:pointer}
  button:disabled{opacity:.6;cursor:default}
  .grid{display:grid;grid-template-columns:repeat(2,1fr);gap:10px}
  .stat{background:var(--card2);border:1px solid var(--line);border-radius:10px;padding:12px}
  .stat .k{font-size:11px;color:var(--mut);text-transform:uppercase;letter-spacing:.05em}
  .stat .v{font-size:16px;margin-top:4px;word-break:break-word}
  .pill{display:inline-block;font-size:12px;font-weight:700;padding:3px 9px;border-radius:999px}
  .p-ok{background:rgba(46,204,113,.15);color:var(--ok)}
  .p-bad{background:rgba(255,92,92,.15);color:var(--bad)}
  .p-warn{background:rgba(255,176,32,.15);color:var(--warn)}
  .p-mut{background:rgba(139,147,163,.15);color:var(--mut)}
  .nodes{list-style:none;padding:0;margin:8px 0 0}
  .nodes li{background:var(--card2);border:1px solid var(--line);border-radius:8px;padding:8px 10px;
            margin-bottom:6px;font-family:ui-monospace,Consolas,monospace;font-size:13px}
  .msg{margin-top:10px;font-size:13px}
  .err{color:var(--bad)}
  .hidden{display:none}
  a{color:var(--acc)}
</style>
</head>
<body>
<div class="wrap">
  <h1>sb0t Pyxis · Hashlink Checker</h1>
  <div class="sub">Verificá si una sala está online y si tiene nodos para el room browser.</div>

  <div class="card">
    <div class="row">
      <div class="col">
        <label>IP</label>
        <input id="ip" placeholder="45.58.168.217" autocomplete="off">
      </div>
      <div class="col" style="max-width:160px">
        <label>Puerto</label>
        <input id="port" placeholder="12000" autocomplete="off">
      </div>
    </div>
    <div style="margin-top:12px">
      <label>Hashlink (opcional)</label>
      <input id="hashlink" placeholder="arlnk://..." autocomplete="off">
    </div>
    <button id="go">Comprobar</button>
  </div>

  <div id="out" class="hidden"></div>
</div>

<script>
const $ = s => document.querySelector(s);
const out = $("#out");
let busy = false;

function esc(s){ return (s==null?"":String(s)).replace(/[&<>"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c])); }

async function check(){
  if (busy) return;
  const ip = $("#ip").value.trim();
  const port = $("#port").value.trim();
  const hashlink = $("#hashlink").value.trim();
  if (!hashlink && (!ip || !port)){ alert("Poné IP y puerto, o un hashlink."); return; }

  busy = true; $("#go").disabled = true; $("#go").textContent = "Comprobando...";
  out.classList.remove("hidden");
  out.innerHTML = '<div class="card"><span class="pill p-mut">consultando...</span></div>';

  try{
    const q = new URLSearchParams({ip, port, hashlink});
    const r = await fetch("/api/check?" + q.toString());
    const data = await r.json();
    render(data);
  }catch(e){
    out.innerHTML = '<div class="card err">Error: ' + esc(e.message) + '</div>';
  }finally{
    busy = false; $("#go").disabled = false; $("#go").textContent = "Comprobar";
  }
}

function pill(state){
  if (state === "open") return '<span class="pill p-ok">ABIERTO</span>';
  if (state === "timeout") return '<span class="pill p-warn">SIN RESPUESTA</span>';
  if (state === "closed") return '<span class="pill p-bad">CERRADO</span>';
  return '<span class="pill p-mut">' + esc(state) + '</span>';
}

function render(d){
  let html = "";

  html += '<div class="card"><div class="grid">';
  html += '<div class="stat"><div class="k">IP</div><div class="v">' + esc(d.ip) + ':' + esc(d.port) + '</div></div>';
  html += '<div class="stat"><div class="k">Nombre (hashlink)</div><div class="v">' + (d.name ? esc(d.name) : '<span class="mut">—</span>') + '</div></div>';
  html += '<div class="stat"><div class="k">TCP</div><div class="v">' + pill(d.tcp.open ? "open" : "closed") + '</div></div>';
  html += '<div class="stat"><div class="k">UDP</div><div class="v">' + pill(d.udp.state) + '</div></div>';
  html += '</div>';

  if (!d.tcp.open){
    html += '<div class="msg err">TCP cerrado → la sala no está online en este puerto. ' + esc(d.tcp.note) + '</div>';
  } else if (d.udp.state === "timeout"){
    html += '<div class="msg">UDP sin responder (' + esc(d.udp.note) + ') → no se pueden evaluar los nodos.</div>';
  } else if (d.udp.state === "closed"){
    html += '<div class="msg err">UDP cerrado: ' + esc(d.udp.note) + '</div>';
  }
  html += '</div>';

  if (d.info){
    const i = d.info;
    html += '<div class="card"><div class="grid">';
    html += '<div class="stat"><div class="k">Usuarios</div><div class="v">' + (i.users==null?"—":esc(i.users)) + '</div></div>';
    html += '<div class="stat"><div class="k">Sala</div><div class="v">' + esc(i.name || "—") + '</div></div>';
    html += '<div class="stat"><div class="k">Versión</div><div class="v">' + esc(i.version || "—") + '</div></div>';
    html += '<div class="stat"><div class="k">Nodos</div><div class="v">' + (i.nodes==null?"—":i.nodes.length) + '</div></div>';
    html += '</div>';
    if (i.topic){
      html += '<div class="stat" style="margin-top:10px"><div class="k">Tema</div><div class="v">' + esc(i.topic) + '</div></div>';
    }

    if (i.nodes && i.nodes.length){
      html += '<label style="margin-top:14px">Nodos</label><ul class="nodes">';
      for (const n of i.nodes) html += '<li>' + esc(n[0]) + ':' + esc(n[1]) + '</li>';
      html += '</ul>';
    }

    if (d.node_status === "none"){
      html += '<div class="msg err">>> 0 nodos: la sala no puede publicarse en el room browser.</div>';
    } else if (d.node_status === "few"){
      html += '<div class="msg" style="color:var(--warn)">>> Pocos nodos: está en la red pero con poca redundancia.</div>';
    } else if (d.node_status === "ok"){
      html += '<div class="msg" style="color:var(--ok)">>> OK: nodos suficientes para el room browser.</div>';
    }
    html += '</div>';
  }

  out.innerHTML = html;
}

$("#go").addEventListener("click", check);
["ip","port","hashlink"].forEach(id => {
  $("#"+id).addEventListener("keydown", e => { if (e.key === "Enter") check(); });
});
</script>
</body>
</html>
"""


class Handler(BaseHTTPRequestHandler):
    timeout_sec = 3.0

    def _send(self, code, body, ctype):
        data = body.encode("utf-8") if isinstance(body, str) else body
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        u = urlparse(self.path)

        if u.path in ("/", "/index.html"):
            self._send(200, PAGE, "text/html; charset=utf-8")
            return

        if u.path == "/api/check":
            q = parse_qs(u.query)
            ip = (q.get("ip", [""])[0] or "").strip()
            port = (q.get("port", [""])[0] or "").strip()
            hashlink = (q.get("hashlink", [""])[0] or "").strip()
            name = None

            if hashlink:
                try:
                    ip, port, name = hc.decode_hashlink(hashlink)
                except Exception as e:
                    self._send(200, json.dumps({"error": "Hashlink inválido: " + str(e)}),
                               "application/json; charset=utf-8")
                    return
            else:
                if not ip or not port:
                    self._send(200, json.dumps({"error": "Falta IP/puerto o hashlink"}),
                               "application/json; charset=utf-8")
                    return
                try:
                    port = int(port)
                except ValueError:
                    self._send(200, json.dumps({"error": "Puerto inválido"}),
                               "application/json; charset=utf-8")
                    return

            try:
                result = do_check(ip, port, self.timeout_sec)
                result["name"] = name
                self._send(200, json.dumps(result), "application/json; charset=utf-8")
            except Exception as e:
                self._send(200, json.dumps({"error": str(e)}),
                           "application/json; charset=utf-8")
            return

        self._send(404, "not found", "text/plain; charset=utf-8")

    def log_message(self, fmt, *args):
        sys.stderr.write("[web] " + (fmt % args) + "\n")


def main():
    p = argparse.ArgumentParser(description="Frontend web para hashlink-check.")
    p.add_argument("--host", default="127.0.0.1", help="Host a escuchar (default 127.0.0.1)")
    p.add_argument("--port", type=int, default=8080, help="Puerto web (default 8080)")
    p.add_argument("--timeout", "-t", type=float, default=3.0, help="Timeout por check (default 3s)")
    args = p.parse_args()

    Handler.timeout_sec = args.timeout

    httpd = ThreadingHTTPServer((args.host, args.port), Handler)
    print("=" * 60)
    print("  sb0t Pyxis - Hashlink Checker WEB")
    print("=" * 60)
    print(f"  Abrí en el navegador:  http://{args.host}:{args.port}")
    print("  (Ctrl+C para detener)")
    print("=" * 60)

    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\nDetenido.")
    finally:
        httpd.server_close()


if __name__ == "__main__":
    main()
