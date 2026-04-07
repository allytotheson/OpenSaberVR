import socket

# --- Config ---
UDP_IP = "0.0.0.0"   # listen on all interfaces
UDP_PORT = 5000      # match your sender port

# --- Create socket ---
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((UDP_IP, UDP_PORT))

print(f"Listening on {UDP_IP}:{UDP_PORT}...")

# --- Receive loop ---
while True:
    data, addr = sock.recvfrom(1024)  # buffer size
    message = data.decode('utf-8', errors='ignore')
    
    print(f"From {addr}: {message}")