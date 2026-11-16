# LopesTech · assistente do Mender no Telegram (voz + Claude pela subscrição Max)
FROM python:3.12-slim-bookworm
ENV PYTHONUNBUFFERED=1 TZ=Europe/Lisbon ASSISTENTE_DATA=/data HF_HOME=/data/hf
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg tzdata nodejs npm \
 && rm -rf /var/lib/apt/lists/*
RUN npm install -g @anthropic-ai/claude-code && claude --version
WORKDIR /app
COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt
COPY *.py ./
VOLUME ["/data"]
CMD ["python", "assistente.py"]
