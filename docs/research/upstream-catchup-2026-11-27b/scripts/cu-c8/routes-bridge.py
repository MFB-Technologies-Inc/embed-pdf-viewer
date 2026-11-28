#!/usr/bin/env python3
"""CU-C8 (2026-11-27b): upstream's signature routes for the Swift tests.

platforms/android/tests/signature-routes.mjs is Node, and Node does not run on
the Mac host, so it runs in the catch-up container, and this bridge gives the
Mac a loopback port that reaches it. Each connection to the host port opens a
`docker exec -i <container> python3` that connects to the helper's port inside
the container and copies bytes both ways.

  routes-bridge.py <container> <checkout in the container>

Prints `ROUTES http://127.0.0.1:<port>` once both ends listen, then serves
until killed. The checkout needs packages/core/signature/dist built and its
node_modules installed.
"""
import socket
import subprocess
import sys
import threading

CONTAINER, CHECKOUT = sys.argv[1], sys.argv[2]
PIPE = (
    "import socket,sys,threading\n"
    "s=socket.create_connection(('127.0.0.1',int(sys.argv[1])))\n"
    "def up():\n"
    "  while (b:=sys.stdin.buffer.read1(65536)): s.sendall(b)\n"
    "  s.shutdown(socket.SHUT_WR)\n"
    "threading.Thread(target=up,daemon=True).start()\n"
    "while (b:=s.recv(65536)): sys.stdout.buffer.write(b); sys.stdout.buffer.flush()\n"
)

helper = subprocess.Popen(
    ["docker", "exec", "-i", "-w", CHECKOUT, CONTAINER,
     "node", "platforms/android/tests/signature-routes.mjs", "."],
    stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
line = helper.stdout.readline().strip()
if not line.startswith("READY "):
    sys.exit(f"helper said {line!r}")
inner = line.split()[1]


def serve(conn):
    proc = subprocess.Popen(["docker", "exec", "-i", CONTAINER, "python3", "-c", PIPE, inner],
                            stdin=subprocess.PIPE, stdout=subprocess.PIPE, bufsize=0)

    def down():
        while b := proc.stdout.read(65536):
            conn.sendall(b)
        conn.close()

    threading.Thread(target=down, daemon=True).start()
    while b := conn.recv(65536):
        proc.stdin.write(b)
    proc.stdin.close()


server = socket.socket()
server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
server.bind(("127.0.0.1", 0))
server.listen(16)
print(f"ROUTES http://127.0.0.1:{server.getsockname()[1]} (container port {inner})", flush=True)
while True:
    conn, _ = server.accept()
    threading.Thread(target=serve, args=(conn,), daemon=True).start()
