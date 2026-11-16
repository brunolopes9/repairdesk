"""Assistente do Mender (LopesTech) no Telegram: texto ou voz -> Claude (subscrição Max via `claude -p`) -> ferramentas do Mender.

- Só responde ao chat_id do Bruno (config: dono_chat_id).
- Voz: faster-whisper no servidor.
- Claude: `claude -p` com CLAUDE_CODE_OAUTH_TOKEN (nunca créditos da API), modelo sonnet, sessão mantida durante o dia (--resume).
- Ferramentas: servidor MCP local "mender" (mender_mcp.py). Nada de Bash/ficheiros/web.
- Fila de 1 (um pedido de cada vez), teto diário de pedidos, e deteção de limite partilhada (limite-<DATA>)
  com o estúdio Cortex e a rotina diária da LopesTech.
"""
import json
import os
import re
import subprocess
import threading
import time
import traceback
from datetime import datetime, timedelta, timezone
from pathlib import Path
from zoneinfo import ZoneInfo

import requests

LISBOA = ZoneInfo("Europe/Lisbon")
DATA = Path(os.environ.get("ASSISTENTE_DATA", "/data"))
CFG_F = DATA / "config.json"
ALERTAS = [Path(p) for p in os.environ.get("ALERTAS", "/alertas/estudio.json:/alertas/lopestech.json").split(":")]

SYSTEM = """És o assistente pessoal do Bruno Lopes dentro do Mender (o sistema de gestão da LopesTech: reparações, loja,
rotina, tarefas, agenda). Falas português de Portugal, direto e curto: é uma conversa de telemóvel no Telegram, muitas
vezes por voz (o texto que recebes pode vir de uma transcrição automática, com erros; interpreta com bom senso).

Regras:
- Usa SEMPRE as ferramentas do Mender para ler e registar coisas: nunca respondas de memória sobre a rotina, agenda,
  tarefas ou se a ligação ao Mender funciona; chama a ferramenta outra vez em cada pedido. Nunca inventes dados: se uma ferramenta falhar ou não estiver
  ligada, diz isso numa frase.
- Ações que escrevem (criar tarefa, marcar rotina, registar peso) faz logo quando o pedido é claro, e confirma no fim
  em uma linha com o que ficou registado (o Mender permite desfazer durante 5 minutos).
- Se faltar algo essencial (ex.: prazo ambíguo), pergunta numa frase curta.
- Finanças: só mostras ao Bruno (este chat é só dele).
- Depois de criar/marcar, termina com «↩️ diz "desfaz" nos próximos 5 min para anular». Se ele pedir para desfazer,
  usa a ferramenta desfazer com o desfazerId da última ação.
- As horas da agenda vêm em UTC: mostra em hora de Lisboa. Prazos para criar_tarefa vão em UTC.
- Erros do Mender trazem um texto em português (detail): mostra-o tal como vem.
- VENDAS (regra do Bruno): total = valor FINAL que o cliente paga (nunca acrescentes IVA); fatura = o que vai na fatura (com IVA);
  o resto fica por fora; custo = custo da peça. Ex.: "A52, 100 €, 70 fatura, custo 60" -> total 100, fatura 70, custo 60.
  Se faltar o telefone de um cliente novo ou houver vários com o mesmo nome, pergunta o telefone.
- Faturas de fornecedor: só listas; quem as classifica é o Bruno no Mender.
- LEMBRETES (novo): qualquer tarefa com prazo (data e hora) recebe aviso aqui no Telegram 30 min antes e outro à hora.
  Para calendário/compromissos («dentista às 15h», «sair de casa às 13h30») usa criar_lembrete com a hora e, se ele pedir
  outra antecedência, avisos_min (ex.: "60,15"). Uma tarefa por compromisso — NUNCA cries tarefas extra «Aviso: …».
  Nunca digas que não consegues avisar: consegues. Para ver o dia usa ver_tarefas (vem com o prazo de cada uma).
- Datas e horas em Lisboa. Hoje é {hoje}.
- Respostas sem markdown pesado (o Telegram mostra texto simples); listas curtas com "•".
"""


def cfg():
    return json.loads(CFG_F.read_text(encoding="utf-8"))


def ler_json(f, padrao):
    try:
        return json.loads(Path(f).read_text(encoding="utf-8"))
    except Exception:
        return padrao


def gravar_json(f, d):
    Path(f).parent.mkdir(parents=True, exist_ok=True)
    tmp = Path(str(f) + ".tmp")
    tmp.write_text(json.dumps(d, ensure_ascii=False, indent=1), encoding="utf-8")
    tmp.replace(f)


def hoje():
    return datetime.now(LISBOA).strftime("%Y-%m-%d")


# ------------------------------------------------------------------ Telegram
class Telegram:
    def __init__(self, token):
        self.base = f"https://api.telegram.org/bot{token}"
        self.fich = f"https://api.telegram.org/file/bot{token}"

    def api(self, metodo, timeout=60, **dados):
        r = requests.post(f"{self.base}/{metodo}", data=dados, timeout=timeout).json()
        if not r.get("ok"):
            raise RuntimeError(f"Telegram {metodo}: {r.get('description')}")
        return r["result"]

    def updates(self, offset):
        r = requests.get(f"{self.base}/getUpdates", params={"offset": offset, "timeout": 25,
                         "allowed_updates": json.dumps(["message"])}, timeout=45).json()
        if not r.get("ok"):
            raise RuntimeError(f"getUpdates: {r.get('description')}")
        return r["result"]

    def texto(self, chat, t):
        for i in range(0, max(1, len(t)), 3900):
            self.api("sendMessage", chat_id=chat, text=t[i:i + 3900] or "👍", disable_web_page_preview="true")

    def a_escrever(self, chat):
        try:
            self.api("sendChatAction", chat_id=chat, action="typing", timeout=10)
        except Exception:
            pass

    def descarregar(self, file_id, destino):
        info = self.api("getFile", file_id=file_id)
        r = requests.get(f"{self.fich}/{info['file_path']}", timeout=300)
        r.raise_for_status()
        Path(destino).write_bytes(r.content)
        return Path(destino)


# ------------------------------------------------------------------ voz
_whisper = None


def transcrever(ficheiro):
    """faster-whisper com array numpy (o av.open dá problemas com alguns ficheiros)."""
    global _whisper
    import numpy as np
    from faster_whisper import WhisperModel
    if _whisper is None:
        _whisper = WhisperModel("small", device="cpu", compute_type="int8", cpu_threads=2)
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(ficheiro), "-ac", "1", "-ar", "16000", "-f", "f32le", "-"],
                         capture_output=True, check=True).stdout
    segs, _ = _whisper.transcribe(np.frombuffer(raw, dtype=np.float32), language="pt", vad_filter=True)
    return " ".join(s.text.strip() for s in segs).strip()


# ------------------------------------------------------------------ limites
class Limite(RuntimeError):
    pass


def limite_ativo():
    chave = f"limite-{hoje()}"
    return any(chave in ler_json(f, {}) for f in ALERTAS)


def marcar_limite():
    chave = f"limite-{hoje()}"
    for f in ALERTAS:
        try:
            d = ler_json(f, {})
            if chave not in d:
                d[chave] = time.time()
                f.write_text(json.dumps(d))          # escrita no sítio (ficheiro montado: não usar rename)
        except Exception:
            traceback.print_exc()


def contar_pedido(c):
    f = DATA / "uso.json"
    u = ler_json(f, {})
    n = u.get(hoje(), 0)
    if n >= c.get("teto_dia", 60):
        raise Limite(f"cheguei ao teto de {c.get('teto_dia', 60)} pedidos de hoje (para poupar a subscrição). Amanhã volto.")
    gravar_json(f, {hoje(): n + 1})


# ------------------------------------------------------------------ Claude
def perguntar(c, texto):
    if limite_ativo():
        raise Limite("a subscrição Claude chegou ao limite hoje; volto quando renovar.")
    contar_pedido(c)
    sess = ler_json(DATA / "sessao.json", {})
    mcp = {"mcpServers": {"mender": {"command": "python", "args": ["/app/mender_mcp.py"],
                                      "env": {"ASSISTENTE_DATA": str(DATA)}}}}
    gravar_json(DATA / "mcp.json", mcp)
    cmd = ["claude", "-p", "--output-format", "json", "--model", c.get("modelo", "sonnet"),
           "--mcp-config", str(DATA / "mcp.json"), "--strict-mcp-config",
           "--allowedTools", "mcp__mender",
           "--disallowedTools", "Bash,Edit,Write,Read,Glob,Grep,WebSearch,WebFetch,Task,NotebookEdit",
           "--append-system-prompt", SYSTEM.replace("{hoje}", datetime.now(LISBOA).strftime("%A, %d/%m/%Y %H:%M"))]
    if sess.get("dia") == hoje() and sess.get("id"):
        cmd += ["--resume", sess["id"]]
    env = dict(os.environ, CLAUDE_CODE_OAUTH_TOKEN=c["claude_oauth_token"])
    env.pop("ANTHROPIC_API_KEY", None)
    r = subprocess.run(cmd, input=texto, capture_output=True, text=True, timeout=600, env=env, cwd="/tmp")
    saida = (r.stdout or "") + (r.stderr or "")
    if re.search(r"usage limit|limit reached|5-hour limit|weekly limit|resets? (at|in)", saida, re.I):
        marcar_limite()
        raise Limite("a subscrição Claude chegou ao limite; volto quando renovar.")
    if r.returncode != 0 and "--resume" in cmd and re.search(r"session|conversation", saida, re.I):
        gravar_json(DATA / "sessao.json", {})          # sessão perdida: recomeça sem ela
        return perguntar(c, texto)
    if r.returncode != 0:
        raise RuntimeError(saida[-500:])
    out = json.loads(r.stdout)
    if out.get("session_id"):
        gravar_json(DATA / "sessao.json", {"dia": hoje(), "id": out["session_id"]})
    if out.get("is_error"):
        raise RuntimeError(str(out.get("result"))[:500])
    return (out.get("result") or "").strip() or "👍"


def registar(entrada, resposta):
    with open(DATA / "conversa.jsonl", "a", encoding="utf-8") as f:
        f.write(json.dumps({"quando": datetime.now(LISBOA).isoformat(timespec="seconds"),
                            "bruno": entrada, "resposta": resposta}, ensure_ascii=False) + "\n")


# ------------------------------------------------------------------ lembretes (Sprint 677)
AVISO_RX = re.compile(r"\[avisos?:\s*([\d,\s]+)\]", re.I)


def _tarefas_mender(c):
    r = requests.get(c["mender_url"].rstrip("/") + "/tarefas", timeout=30,
                     headers={c.get("mender_header", "X-Api-Key"): c["mender_chave"]})
    r.raise_for_status()
    return r.json()


def _quando(t):
    """dueAt vem em UTC (com ou sem «Z»)."""
    v = (t.get("dueAt") or "").replace("Z", "+00:00")
    if not v:
        return None
    q = datetime.fromisoformat(v)
    return q if q.tzinfo else q.replace(tzinfo=timezone.utc)


def _antecedencias(t):
    m = AVISO_RX.search(t.get("description") or "")
    mins = {int(x) for x in m.group(1).replace(" ", "").split(",") if x.isdigit()} if m else {30}
    return sorted(mins | {0}, reverse=True)


def _limpo(texto):
    return AVISO_RX.sub("", texto or "").strip()


def lembretes(tg, c):
    """Fio à parte: a cada minuto vê as tarefas com prazo e avisa no Telegram. Guarda o que já avisou (nunca repete)."""
    f = DATA / "avisos.json"
    while True:
        try:
            c = cfg()
            enviados = ler_json(f, {})
            agora = datetime.now(timezone.utc)
            tarefas = _tarefas_mender(c)
            chat = c["dono_chat_id"]
            mudou = False
            for t in tarefas:
                q = _quando(t)
                # «Aviso: …» eram tarefas-lembrete feitas à mão pelo bot antigo: a tarefa principal já avisa.
                if not q or (t.get("title") or "").lower().startswith("aviso"):
                    continue
                for ant in _antecedencias(t):
                    alvo = q - timedelta(minutes=ant)
                    chave = f"{t['id']}|{t['dueAt']}|{ant}"
                    if chave in enviados or not (alvo <= agora < alvo + timedelta(minutes=15)):
                        continue
                    hora = q.astimezone(LISBOA).strftime("%H:%M")
                    titulo = t.get("title") or "Tarefa"
                    nota = _limpo(t.get("description"))
                    msg = (f"🔔 Agora ({hora}): {titulo}" if ant == 0 else
                           f"⏰ Daqui a {ant} min ({hora}): {titulo}" if ant < 120 else
                           f"⏰ Às {hora} ({ant // 60} h): {titulo}")
                    if nota and len(nota) < 200:
                        msg += f"\n{nota}"
                    tg.texto(chat, msg)
                    enviados[chave] = agora.isoformat()
                    mudou = True
            # Resumo do dia: uma vez, entre as 07:30 e o meio-dia (hora de Lisboa).
            local = agora.astimezone(LISBOA)
            chave_dia = f"resumo|{local:%Y-%m-%d}"
            if chave_dia not in enviados and (7, 30) <= (local.hour, local.minute) < (12, 0):
                hoje_l = [t for t in tarefas if _quando(t) and _quando(t).astimezone(LISBOA).date() == local.date()
                          and not (t.get("title") or "").lower().startswith("aviso")]
                hoje_l.sort(key=_quando)
                if hoje_l:
                    linhas = "\n".join(f"• {_quando(t).astimezone(LISBOA):%H:%M} {t['title']}" for t in hoje_l)
                    tg.texto(chat, f"☀️ Bom dia! Hoje tens:\n{linhas}\n\nAviso-te 30 min antes de cada uma e à hora.")
                enviados[chave_dia] = agora.isoformat()
                mudou = True
            if mudou:
                limite = (agora - timedelta(days=4)).isoformat()
                gravar_json(f, {k: v for k, v in enviados.items() if v >= limite})
        except Exception:
            traceback.print_exc()
        time.sleep(60)


# ------------------------------------------------------------------ ciclo
def tratar(tg, c, msg):
    chat = msg["chat"]["id"]
    if str(chat) != str(c.get("dono_chat_id")):
        return                                        # só o Bruno
    tg.a_escrever(chat)
    if msg.get("voice") or msg.get("audio"):
        v = msg.get("voice") or msg.get("audio")
        f = tg.descarregar(v["file_id"], DATA / "voz.ogg")
        texto = transcrever(f)
        if not texto:
            tg.texto(chat, "Não percebi o áudio. Podes repetir?")
            return
        tg.texto(chat, f"🎙️ «{texto}»")
        tg.a_escrever(chat)
    else:
        texto = (msg.get("text") or "").strip()
    if not texto:
        return
    if texto in ("/start", "/ajuda"):
        tg.texto(chat, "Olá Bruno. Fala ou escreve: «o que tenho agora?», «já almocei», «peso 82,4», «tarefa: ligar ao fornecedor amanhã», «agenda da semana», «peças por encomendar», «finanças do mês». /nova recomeça a conversa.")
        return
    if texto == "/nova":
        gravar_json(DATA / "sessao.json", {})
        tg.texto(chat, "Conversa nova. 👍")
        return
    try:
        resposta = perguntar(c, texto)
    except Limite as e:
        resposta = "⏸️ " + str(e)
    except Exception as e:
        traceback.print_exc()
        resposta = f"⚠️ Erro: {str(e)[:300]}"
    registar(texto, resposta)
    tg.texto(chat, resposta)


def main():
    while True:
        c = cfg()
        if c.get("telegram_token") and c.get("claude_oauth_token") and c.get("dono_chat_id"):
            break
        print("à espera de telegram_token / claude_oauth_token / dono_chat_id em config.json", flush=True)
        time.sleep(60)
    tg = Telegram(c["telegram_token"])
    print("assistente ligado", flush=True)
    if c.get("mender_chave"):
        threading.Thread(target=lembretes, args=(tg, c), daemon=True, name="lembretes").start()
    est = ler_json(DATA / "estado.json", {"offset": 0})
    while True:
        try:
            for u in tg.updates(est["offset"]):
                est["offset"] = u["update_id"] + 1
                gravar_json(DATA / "estado.json", est)     # antes de tratar: nunca repete um pedido
                if u.get("message"):
                    tratar(tg, cfg(), u["message"])        # fila de 1: um de cada vez, por ordem
        except Exception:
            traceback.print_exc()
            time.sleep(10)


if __name__ == "__main__":
    main()
