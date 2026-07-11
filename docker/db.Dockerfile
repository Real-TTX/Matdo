# ============================================================
#  Matdo – Postgres für die Public/Homelab-Compose.
#  Die Zugangsdaten sind hier fest hinterlegt, damit die
#  docker-compose.public.yml OHNE environment-Variablen auskommt.
#  Die Datenbank ist nur intern erreichbar (kein veröffentlichter Port).
# ============================================================
FROM postgres:17-alpine
ENV POSTGRES_USER=matdo
ENV POSTGRES_PASSWORD=matdo
ENV POSTGRES_DB=matdo
ENV TZ=Europe/Berlin
