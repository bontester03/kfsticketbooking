# Deploy KFS Booking to AWS — single-EC2 with Docker Compose

A one-evening deploy. The whole stack (API · Postgres · Portal · Admin · Scanner · Caddy reverse proxy with auto-HTTPS) runs on a single EC2 instance. Same `docker-compose` setup you've been using locally, plus a small production overlay (`infra/aws/docker-compose.prod.yml`) and a Caddyfile.

> **Sizing for ~300 simultaneous parents:** `t3.large` (2 vCPU / **8 GB**) is the recommended default — handles the 30-second login burst with sub-2-second response times. Bumping to `t3.xlarge` (4 vCPU / 16 GB) is the bulletproof option for the actual event night (~$120 / mo) — and you can `aws ec2 modify-instance-attribute` it back down to `t3.large` or `t3.medium` after the event (see § 9).
>
> **Cost estimate** for `t3.large`: ≈ **$60 / mo** ($55 instance + $3 EBS + Route 53 zone). `t3.medium` (~$30 / mo) is fine for **testing / a smaller event (< 100 parents)** but marginal for 300 simultaneous logins.

---

## 0. Prerequisites

- An AWS account with at least these IAM permissions: `AmazonEC2FullAccess`, `AmazonRoute53FullAccess` (only if you're managing DNS in AWS). The school already gave you access.
- A domain name with control over its DNS — even a free subdomain works.
- A **Gmail App Password** for `graduation@kfs.sch.sa` (you already have one). Same value as in your local `.env`.

---

## 1. Open AWS CloudShell

Sign in to **<https://console.aws.amazon.com>**. In the top-right toolbar, click the `>_` icon — **CloudShell** opens in a panel at the bottom of your browser.

CloudShell already has `aws` CLI v2 preinstalled and your account identity ready. You won't need to install anything on your laptop.

```bash
aws sts get-caller-identity     # confirms who you're acting as
```

Pick a region. For Saudi PDPL data residency, use **Bahrain `me-south-1`**:

```bash
export AWS_REGION=me-south-1
aws configure set default.region $AWS_REGION
```

---

## 2. Launch the EC2 instance

```bash
# --- pick the latest Ubuntu 22.04 AMI for this region ---
AMI_ID=$(aws ec2 describe-images \
  --owners 099720109477 \
  --filters "Name=name,Values=ubuntu/images/hvm-ssd/ubuntu-jammy-22.04-amd64-server-*" \
            "Name=state,Values=available" \
  --query "sort_by(Images, &CreationDate)[-1].ImageId" \
  --output text)
echo "AMI: $AMI_ID"

# --- security group: SSH (22) + HTTP (80) + HTTPS (443) ---
SG_ID=$(aws ec2 create-security-group \
  --group-name kfs-booking-sg \
  --description "KFS Booking — web + ssh" \
  --query GroupId --output text)

aws ec2 authorize-security-group-ingress --group-id $SG_ID --protocol tcp --port 22  --cidr 0.0.0.0/0
aws ec2 authorize-security-group-ingress --group-id $SG_ID --protocol tcp --port 80  --cidr 0.0.0.0/0
aws ec2 authorize-security-group-ingress --group-id $SG_ID --protocol tcp --port 443 --cidr 0.0.0.0/0

# --- launch a t3.large with 30 GB EBS root volume (sized for 300 simultaneous parents) ---
# Use t3.medium instead for testing / < 100-parent events.
INSTANCE_ID=$(aws ec2 run-instances \
  --image-id $AMI_ID \
  --instance-type t3.large \
  --security-group-ids $SG_ID \
  --block-device-mappings '[{"DeviceName":"/dev/sda1","Ebs":{"VolumeSize":30,"VolumeType":"gp3"}}]' \
  --tag-specifications 'ResourceType=instance,Tags=[{Key=Name,Value=kfs-booking}]' \
  --query 'Instances[0].InstanceId' --output text)

echo "Instance: $INSTANCE_ID"

# --- wait for it to be running and grab its public IP ---
aws ec2 wait instance-running --instance-ids $INSTANCE_ID
PUBLIC_IP=$(aws ec2 describe-instances --instance-ids $INSTANCE_ID \
  --query 'Reservations[0].Instances[0].PublicIpAddress' --output text)
echo "Public IP: $PUBLIC_IP"
```

**Write down `$INSTANCE_ID` and `$PUBLIC_IP`** — you'll need them.

> **Why t3.medium?** Builds the API/web Docker images comfortably (`t2.micro` runs out of RAM during pnpm + dotnet builds). Once images exist, the runtime fits in much less; you can downsize later.

---

## 3. Point DNS at the instance

You need **three hostnames** — one per app. Choose any prefix you like:

| App | Suggested hostname |
|---|---|
| Portal | `kfs.<your-domain>` |
| Admin | `admin.kfs.<your-domain>` |
| Scanner | `scan.kfs.<your-domain>` |

Add **three A records** all pointing at `$PUBLIC_IP`.

**If your domain is in Route 53:**
```bash
ZONE_ID=$(aws route53 list-hosted-zones-by-name --dns-name your-domain.com \
  --query 'HostedZones[0].Id' --output text | sed 's|/hostedzone/||')

for SUB in kfs admin.kfs scan.kfs; do
  aws route53 change-resource-record-sets --hosted-zone-id $ZONE_ID \
    --change-batch "{\"Changes\":[{\"Action\":\"UPSERT\",\"ResourceRecordSet\":{\"Name\":\"$SUB.your-domain.com\",\"Type\":\"A\",\"TTL\":300,\"ResourceRecords\":[{\"Value\":\"$PUBLIC_IP\"}]}}]}"
done
```

**If your domain is elsewhere (GoDaddy, Cloudflare, …)** — log in to their dashboard and add the three A records there. Give DNS ~5 minutes to propagate.

Confirm with:
```bash
dig +short kfs.your-domain.com         # should print $PUBLIC_IP
```

---

## 4. SSH into the instance (browser, no key needed)

You can use **EC2 Instance Connect** — no SSH key on your laptop. In the EC2 console: select the instance → **Connect** → **EC2 Instance Connect** → **Connect**. A terminal opens in your browser.

Or, from CloudShell:
```bash
aws ec2-instance-connect ssh --instance-id $INSTANCE_ID
```

Everything in section 5+ runs on the **EC2**, not in CloudShell.

---

## 5. Install Docker on the EC2

```bash
# Update + install Docker + Compose v2 plugin
sudo apt-get update -y
sudo apt-get install -y docker.io docker-compose-v2 git
sudo usermod -aG docker ubuntu
newgrp docker            # picks up the group without a re-login
docker --version
docker compose version
```

---

## 6. Clone the repo and configure

```bash
cd ~
git clone https://github.com/bontester03/kfsticketbooking.git kfs
cd kfs

# Copy the production env template and edit it
cp infra/aws/.env.production.example .env
nano .env       # set hostnames, JWT/QR secrets, postgres pw, email app password
```

Set every `CHANGE_THIS_*` value to a real long random string. Easy generator inside the EC2:
```bash
openssl rand -hex 32     # 64-char hex — perfect for JWT_SECRET / QR_SIGNING_KEY
openssl rand -hex 16     # shorter for POSTGRES_PASSWORD
```

The `KFS_HOST_*` values must match the DNS records from step 3.

---

## 7. First build + boot

```bash
docker compose \
  -f docker-compose.yml \
  -f infra/aws/docker-compose.prod.yml \
  up -d --build
```

Builds run on the EC2 — should take ~5–8 minutes the first time, then almost instant on subsequent restarts.

Watch logs while Caddy negotiates the TLS certs (this takes ~30 s on first launch):
```bash
docker compose logs -f caddy
```

You're looking for lines like `certificate obtained successfully` for each of your three hostnames. If Caddy can't reach Let's Encrypt: check DNS has propagated (`dig`), and that the security group really lets port 80 in from `0.0.0.0/0` (Let's Encrypt validates over HTTP).

---

## 8. Smoke test

Open in a browser:

| URL | What you should see |
|---|---|
| `https://kfs.<your-domain>` | Sign-in landing page with the live event banner |
| `https://kfs.<your-domain>/api/v1/public/event` | JSON with event name + seats remaining |
| `https://admin.kfs.<your-domain>` | Admin console login (admin@kfs.sch.sa / `SUPER_ADMIN_PASSWORD`) |
| `https://scan.kfs.<your-domain>` | Gate scanner token entry page |

The lock icon should appear in all three — confirming Caddy got valid Let's Encrypt certs.

Sign into admin → **Guest** tab → copy the **Gate scanner link** (it includes the event scanner token). That's the URL you give the iPad gate staff.

---

## 9. Useful day-to-day commands

```bash
# tail logs
docker compose -f docker-compose.yml -f infra/aws/docker-compose.prod.yml logs -f api

# restart one service (e.g. after pulling new code)
git pull
docker compose -f docker-compose.yml -f infra/aws/docker-compose.prod.yml up -d --build api

# stop everything
docker compose -f docker-compose.yml -f infra/aws/docker-compose.prod.yml down

# database backup (run it from the EC2 then download via SCP / S3)
docker exec kfs-postgres pg_dump -U kfs kfs > backup-$(date +%F).sql

# database restore
cat backup-2026-06-04.sql | docker exec -i kfs-postgres psql -U kfs -d kfs
```

---

## 10. (Optional) Move the DB to RDS later

The `postgres` container is fine for the event. If you want a managed DB later:

1. Create an RDS PostgreSQL 16 instance in the same VPC (Free Tier: `db.t4g.micro`).
2. Open security-group port 5432 from the EC2 to RDS.
3. `pg_dump | psql` the data to RDS.
4. Change `ConnectionStrings__Default` in `.env` to the RDS endpoint.
5. Remove the `postgres` service from your effective compose (or stop it).

---

## 11. (Optional) Move blobs to S3 later

The QR / printable PDFs are currently served from the EC2 disk under `/app/wwwroot`. For redundancy you can later swap to S3:

- Create an S3 bucket in the same region.
- Implement `S3BlobStorage : IBlobStorage` (parallel to the existing `AzureBlobStorage` and `LocalDiskBlobStorage`). The interface is already in place.
- Set `Storage__Provider=S3` and the bucket name in env vars.

Not needed for the event. The current local-disk store survives reboots because the API's `wwwroot` is a Docker named volume.

---

## 12. Tearing it all down

```bash
# from CloudShell
aws ec2 terminate-instances --instance-ids $INSTANCE_ID
aws ec2 delete-security-group --group-id $SG_ID
# Route 53 records (if you used them) — delete via the console, or:
# aws route53 change-resource-record-sets ... DELETE
```

EBS volume is deleted with the instance. The DNS records do NOT delete themselves — clean them up if you no longer need them.

---

## Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| `docker compose up --build` runs out of memory and kills `dotnet restore` | Instance too small. `t3.medium` (4 GB) is the minimum. |
| Caddy logs say `could not get certificate: x509: certificate signed by unknown authority` or similar | DNS hasn't propagated yet. `dig <host>` from the EC2; wait 5 minutes; retry `docker restart kfs-caddy`. |
| Caddy logs say `connection refused` to portal/admin/scanner | The web containers haven't finished building. Wait for them, then `docker restart kfs-caddy`. |
| Iframe browser opens the scanner but the camera tile is black | The iPad / phone must be on **HTTPS** (it is via Caddy) AND have internet to fetch `jsQR` from CDN. Restrictive school Wi-Fi can block `cdn.jsdelivr.net`. |
| Booking confirmation emails not arriving | Check `docker logs kfs-api | grep -i smtp`. Almost always either app-password typo or Gmail's "less-secure apps" still off for that OU. |
| QR images broken on the portal (`<img>` with red cross) | `Storage__PublicBaseUrl` must match the portal's public URL (set automatically by the prod compose to `https://${KFS_HOST_PORTAL}`). |

---

## What this differs from the Azure setup

| Concern | Azure (UAE North / UK South split) | This AWS setup |
|---|---|---|
| Compute | App Service Linux | One EC2 (Docker Compose) |
| Static frontends | Azure Static Web Apps × 3 | nginx containers fronted by Caddy |
| Database | Postgres Flexible Server | Postgres in a container (move to RDS later) |
| Blob storage | Azure Blob | Local disk on the EC2 (move to S3 later) |
| Secrets | Key Vault | `.env` on the EC2 (move to Secrets Manager later) |
| HTTPS | Managed by App Service / SWA | Caddy + Let's Encrypt |
| Data residency | UAE North | Bahrain `me-south-1` |
| Monthly cost | ~$50–60 (B1 + Burstable Postgres + storage) | ~$30–40 |

Pragmatic for a school event. If you outgrow it, swap pieces in (`RDS`, `S3`, `App Runner`) without touching the application code — the abstractions (`IBlobStorage`, env-driven config) make each swap a localised change.
