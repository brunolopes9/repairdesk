#!/usr/bin/env bash
#
# RepairDesk — Hetzner Cloud server provisioning
# Target: Ubuntu 24.04 LTS, CCX13 (Dedicated vCPU)
# Run as root on a freshly-created server. Idempotent — re-runs are safe.
#
# What this does:
#   1. apt update + upgrade
#   2. Creates non-root user `deploy` with sudo, copies SSH key from root
#   3. Installs Docker Engine + Compose plugin (via official Docker repo)
#   4. Installs Caddy (HTTPS reverse proxy with auto Let's Encrypt)
#   5. Installs fail2ban (SSH brute-force protection)
#   6. Configures UFW firewall: only 22, 80, 443
#   7. Disables root SSH login + password-auth
#   8. Tunes system limits for SQL Server + Redis
#
# After this script, log out and reconnect as `deploy@<IP>`.

set -euo pipefail

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

log() { echo -e "${GREEN}[setup]${NC} $*"; }
warn() { echo -e "${YELLOW}[warn]${NC} $*"; }
err() { echo -e "${RED}[err]${NC} $*" >&2; }

if [[ $EUID -ne 0 ]]; then
    err "Run as root (or with sudo)."
    exit 1
fi

if ! grep -qE 'VERSION_ID="24\.04"' /etc/os-release; then
    warn "This script targets Ubuntu 24.04. Detected:"
    cat /etc/os-release | grep -E '^(NAME|VERSION_ID)='
    read -rp "Continue anyway? [y/N] " yn
    [[ $yn =~ ^[Yy]$ ]] || exit 1
fi

# ---------------------------------------------------------------------------
log "Step 1/8 — apt update + upgrade"
# ---------------------------------------------------------------------------
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get upgrade -y
apt-get install -y curl wget gnupg2 ca-certificates lsb-release \
    software-properties-common apt-transport-https unattended-upgrades \
    htop ncdu jq vim tmux git ufw fail2ban

# Enable automatic security updates
dpkg-reconfigure -plow unattended-upgrades || true

# ---------------------------------------------------------------------------
log "Step 2/8 — create non-root user 'deploy' with sudo"
# ---------------------------------------------------------------------------
if ! id -u deploy >/dev/null 2>&1; then
    useradd -m -s /bin/bash deploy
    usermod -aG sudo deploy
    # Allow deploy to sudo without password (acceptable for solo ops; tighten if shared)
    echo "deploy ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/deploy
    chmod 0440 /etc/sudoers.d/deploy
    log "Created user 'deploy'"
else
    log "User 'deploy' already exists, skipping"
fi

# Copy authorized_keys from root to deploy
if [[ -f /root/.ssh/authorized_keys ]]; then
    mkdir -p /home/deploy/.ssh
    cp /root/.ssh/authorized_keys /home/deploy/.ssh/authorized_keys
    chown -R deploy:deploy /home/deploy/.ssh
    chmod 700 /home/deploy/.ssh
    chmod 600 /home/deploy/.ssh/authorized_keys
    log "Copied SSH authorized_keys to deploy user"
else
    warn "/root/.ssh/authorized_keys not found — you must add deploy's SSH key manually before disabling root login!"
fi

# ---------------------------------------------------------------------------
log "Step 3/8 — install Docker Engine + Compose plugin"
# ---------------------------------------------------------------------------
if ! command -v docker >/dev/null 2>&1; then
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
        -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
        > /etc/apt/sources.list.d/docker.list
    apt-get update -y
    apt-get install -y docker-ce docker-ce-cli containerd.io \
        docker-buildx-plugin docker-compose-plugin
    systemctl enable --now docker
    log "Docker installed: $(docker --version)"
else
    log "Docker already installed: $(docker --version)"
fi
usermod -aG docker deploy
log "Added 'deploy' to docker group (re-login needed to take effect)"

# ---------------------------------------------------------------------------
log "Step 4/8 — install Caddy (HTTPS reverse proxy)"
# ---------------------------------------------------------------------------
if ! command -v caddy >/dev/null 2>&1; then
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' \
        | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' \
        | tee /etc/apt/sources.list.d/caddy-stable.list >/dev/null
    apt-get update -y
    apt-get install -y caddy
    log "Caddy installed: $(caddy version)"
else
    log "Caddy already installed: $(caddy version)"
fi

# Initial stub Caddyfile — Bruno replaces with real config after deploy
if [[ ! -f /etc/caddy/Caddyfile.original ]]; then
    cp /etc/caddy/Caddyfile /etc/caddy/Caddyfile.original
fi
cat > /etc/caddy/Caddyfile <<'EOF'
# Stub — replace with deploy/hetzner/Caddyfile.app.lopestech.pt after first deploy
:80 {
    respond "RepairDesk server provisioned. Replace /etc/caddy/Caddyfile." 503
}
EOF
systemctl reload caddy || systemctl restart caddy
mkdir -p /var/log/caddy
chown caddy:caddy /var/log/caddy

# ---------------------------------------------------------------------------
log "Step 5/8 — configure fail2ban"
# ---------------------------------------------------------------------------
cat > /etc/fail2ban/jail.local <<'EOF'
[DEFAULT]
bantime = 1h
findtime = 10m
maxretry = 5

[sshd]
enabled = true
EOF
systemctl enable --now fail2ban

# ---------------------------------------------------------------------------
log "Step 6/8 — UFW firewall (22, 80, 443 only)"
# ---------------------------------------------------------------------------
ufw --force reset
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp  comment 'SSH'
ufw allow 80/tcp  comment 'HTTP (Caddy → 443 redirect)'
ufw allow 443/tcp comment 'HTTPS (Caddy)'
ufw --force enable
ufw status verbose

# ---------------------------------------------------------------------------
log "Step 7/8 — harden SSH (disable root, disable password auth)"
# ---------------------------------------------------------------------------
SSHD_CONFIG="/etc/ssh/sshd_config"
# Backup
cp "$SSHD_CONFIG" "${SSHD_CONFIG}.bak-$(date +%Y%m%d-%H%M%S)"

# Set hardened values
sed -i 's/^#\?PermitRootLogin.*/PermitRootLogin no/' "$SSHD_CONFIG"
sed -i 's/^#\?PasswordAuthentication.*/PasswordAuthentication no/' "$SSHD_CONFIG"
sed -i 's/^#\?PubkeyAuthentication.*/PubkeyAuthentication yes/' "$SSHD_CONFIG"
sed -i 's/^#\?ChallengeResponseAuthentication.*/ChallengeResponseAuthentication no/' "$SSHD_CONFIG"
sed -i 's/^#\?KbdInteractiveAuthentication.*/KbdInteractiveAuthentication no/' "$SSHD_CONFIG"

# Validate and restart
sshd -t
systemctl restart ssh

# ---------------------------------------------------------------------------
log "Step 8/8 — system tuning (vm.max_map_count for SQL Server, sysctl)"
# ---------------------------------------------------------------------------
cat > /etc/sysctl.d/99-repairdesk.conf <<'EOF'
# SQL Server in Docker requires bumped max_map_count
vm.max_map_count = 262144
# Redis: allow overcommit (silence kernel warning)
vm.overcommit_memory = 1
# General network tuning
net.core.somaxconn = 1024
EOF
sysctl --system >/dev/null

# Disable transparent huge pages (Redis recommends)
if [[ ! -f /etc/systemd/system/disable-thp.service ]]; then
    cat > /etc/systemd/system/disable-thp.service <<'EOF'
[Unit]
Description=Disable Transparent Huge Pages (THP)
DefaultDependencies=no
After=sysinit.target local-fs.target

[Service]
Type=oneshot
ExecStart=/bin/sh -c 'echo never | tee /sys/kernel/mm/transparent_hugepage/enabled > /dev/null; echo never | tee /sys/kernel/mm/transparent_hugepage/defrag > /dev/null'

[Install]
WantedBy=basic.target
EOF
    systemctl daemon-reload
    systemctl enable --now disable-thp
fi

# ---------------------------------------------------------------------------
log "Done. Summary:"
echo
echo "  User:                  deploy (sudo, no password)"
echo "  Docker:                $(docker --version | head -1)"
echo "  Docker Compose:        $(docker compose version | head -1)"
echo "  Caddy:                 $(caddy version)"
echo "  Firewall:              22, 80, 443 only"
echo "  Root SSH:              disabled"
echo "  Password auth SSH:     disabled (key-only)"
echo
echo "Next:"
echo "  1. Log out and reconnect as: ssh deploy@$(hostname -I | awk '{print $1}')"
echo "  2. Clone RepairDesk repo to /opt/repairdesk"
echo "  3. Fill .env.production"
echo "  4. docker compose -f docker-compose.prod.yml --env-file .env.production up -d"
echo "  5. Replace /etc/caddy/Caddyfile with deploy/hetzner/Caddyfile.app.lopestech.pt"
echo "  6. sudo systemctl reload caddy"
