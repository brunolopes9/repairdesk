"""Servidor MCP (stdio) com as ferramentas do Mender para o assistente.

Contrato: Mender v0.60.0, BASE https://app.lopestech.pt/api/external/assistente, header X-Api-Key (chave com o scope
«assistente», criada pelo Bruno; age em nome dele). Erros em ProblemDetails {title: código, detail: texto PT}.
Config (/data/config.json): mender_url (BASE), mender_chave, mender_header (padrão X-Api-Key).
"""
import json
import os
from pathlib import Path

import requests
from mcp.server.fastmcp import FastMCP

DATA = Path(os.environ.get("ASSISTENTE_DATA", "/data"))
mcp = FastMCP("mender")


def _cfg():
    return json.loads((DATA / "config.json").read_text(encoding="utf-8"))


def _pedir(metodo, caminho, corpo=None, params=None):
    c = _cfg()
    if not c.get("mender_chave") or not c.get("mender_url"):
        return "Ainda não ligado ao Mender (falta a chave de serviço «assistente»)."
    if corpo:
        corpo = {k: v for k, v in corpo.items() if v not in (None, "")}
    try:
        r = requests.request(metodo, c["mender_url"].rstrip("/") + caminho, json=corpo,
                             params={k: v for k, v in (params or {}).items() if v not in (None, "")} or None,
                             timeout=30, headers={c.get("mender_header", "X-Api-Key"): c["mender_chave"]})
    except Exception as e:
        return f"Mender inacessível: {e}"
    if r.status_code >= 400:
        try:
            p = r.json()
            return f"ERRO {r.status_code} ({p.get('title')}): {p.get('detail') or ''}"
        except Exception:
            return f"ERRO {r.status_code}: {r.text[:300]}"
    try:
        d = r.json()
    except Exception:
        return r.text[:8000] or "ok"
    if isinstance(d, dict) and d.get("desfazerId"):
        d["_nota"] = "Diz o resumo ao Bruno e lembra que pode desfazer durante 5 minutos (guarda o desfazerId)."
    return json.dumps(d, ensure_ascii=False)[:40000]


@mcp.tool()
def ver_agora() -> str:
    """O que é agora: rotina do dia (item atual/próximo, feitos, atrasados), prioridades de hoje e agenda de hoje."""
    return _pedir("GET", "/agora")


@mcp.tool()
def ver_rotina(dia: str = "") -> str:
    """Rotina de um dia (AAAA-MM-DD; vazio = hoje). estado: null por fazer, 1 feito, 2 saltado, 3 adiado.
    tipo: 1 refeição, 2 suplemento, 3 meditação, 4 exercício, 5 trading, 6 trabalho, 7 limite, 8 pessoal, 9 peso."""
    return _pedir("GET", "/rotina", params={"dia": dia})


@mcp.tool()
def criar_tarefa(titulo: str, foco: str = "ideia", prazo: str = "", notas: str = "") -> str:
    """Cria uma tarefa. foco: "hoje" (máx. 3 prioridades por dia), "semana", "ideia" (padrão) ou "nenhum".
    prazo: data/hora ISO em UTC (ex.: 2026-11-13T09:00:00Z), opcional. Se der max_hoje, sugere "semana"."""
    return _pedir("POST", "/tarefas", {"titulo": titulo, "foco": foco, "prazo": prazo, "notas": notas})


@mcp.tool()
def marcar_rotina(item: str = "", item_id: str = "", estado: str = "feito", valor: float = 0,
                  motivo: str = "", dia: str = "") -> str:
    """Marca um item da rotina. item: título aproximado (ex.: "almoço", "meditação"); ou item_id (guid).
    estado: "feito" ou "saltado". valor: peso em kg para o «Registar peso» (0 = não se aplica). dia: AAAA-MM-DD (vazio = hoje)."""
    return _pedir("POST", "/rotina", {"item": item, "itemId": item_id, "estado": estado,
                                      "valor": valor or None, "motivo": motivo, "dia": dia})


@mcp.tool()
def ver_agenda(dias: int = 7) -> str:
    """Marcações com clientes nos próximos N dias (horas em UTC: converte para Lisboa)."""
    return _pedir("GET", "/agenda", params={"dias": dias})


@mcp.tool()
def ver_pecas() -> str:
    """Peças por encomendar (cliente, equipamento, descrição, quantidade, custo estimado)."""
    return _pedir("GET", "/pecas")


@mcp.tool()
def ver_financas(ano: int = 0, mes: int = 0) -> str:
    """Finanças do mês (saldo, objetivo, lucro informática, payouts de trading, despesas). Vazio = mês atual."""
    return _pedir("GET", "/financas", params={"ano": ano or None, "mes": mes or None})


@mcp.tool()
def desfazer(desfazer_id: str) -> str:
    """Desfaz uma ação feita há menos de 5 minutos (tarefa apagada, rotina reposta, venda cancelada, despesa apagada), com o desfazerId que ela devolveu."""
    return _pedir("POST", f"/desfazer/{desfazer_id}")


@mcp.tool()
def ver_tarefas(foco: str = "") -> str:
    """Tarefas pendentes (compactas: id, título, prazo em UTC, foco, notas curtas), as com prazo primeiro, por hora.
    foco: "hoje", "semana" ou "ideia" (vazio = todas)."""
    r = _pedir("GET", "/tarefas", params={"foco": foco})
    try:
        lista = json.loads(r)
    except Exception:
        return r
    focos = {0: "nenhum", 1: "hoje", 2: "semana", 3: "ideia"}
    curtas = [{"id": t["id"], "titulo": t["title"], "prazo": t.get("dueAt"), "foco": focos.get(t.get("foco"), t.get("foco")),
               "notas": (t.get("description") or "")[:120]} for t in lista]
    curtas.sort(key=lambda t: (t["prazo"] is None, t["prazo"] or ""))
    return json.dumps(curtas, ensure_ascii=False)


@mcp.tool()
def criar_lembrete(titulo: str, quando: str, avisos_min: str = "30", notas: str = "", foco: str = "nenhum") -> str:
    """Compromisso/lembrete com hora (o calendário do Bruno): o bot avisa no Telegram antes (avisos_min, ex.: "30" ou "60,15")
    e à hora. quando: data/hora ISO em UTC (ex.: 2026-11-16T13:30:00Z). Uma tarefa por compromisso — nunca crie «Aviso: …» à parte."""
    mins = ",".join(x.strip() for x in avisos_min.split(",") if x.strip().isdigit()) or "30"
    nota = (notas.strip() + " " if notas.strip() else "") + f"[avisos: {mins}]"
    return _pedir("POST", "/tarefas", {"titulo": titulo, "foco": foco, "prazo": quando, "notas": nota})


@mcp.tool()
def registar_venda(cliente: str, descricao: str, total: float, telefone: str = "", tipo: str = "", equipamento: str = "",
                   avaria: str = "", custo: float = 0, estado: str = "", notas: str = "") -> str:
    """Regista uma venda/reparação. REGRA DO BRUNO: total = valor FINAL que o cliente paga (nunca somar IVA);
    a venda é toda faturada pelo total (IVA incluído); custo = quanto custou a peça/produto.
    Ex.: "A52, 100 €, custo 60" -> total 100, custo 60.
    tipo: reparacao | produto | servico. estado: orcamento | pronta (omissão) | entregue.
    cliente: nome (aproximado) ou telefone; se houver vários com o mesmo nome a API devolve 409 e é preciso o telefone;
    cliente novo precisa de nome + telefone."""
    return _pedir("POST", "/vendas", {"cliente": cliente, "telefone": telefone, "tipo": tipo, "equipamento": equipamento, "avaria": avaria,
                                       "descricao": descricao, "total": total, "custo": custo or None,
                                       "estado": estado, "notas": notas})


@mcp.tool()
def ver_lista_encomendas() -> str:
    """Lista de compras da oficina: o que falta encomendar (estado 0) e o que já vem a caminho (estado 1)."""
    return _pedir("GET", "/lista-encomendas")


@mcp.tool()
def juntar_lista_encomendas(descricao: str, quantidade: int = 1, fornecedor: str = "", nota: str = "") -> str:
    """Junta um artigo à lista do que é preciso encomendar (ex.: "película Redmi 15C", 5, "LCPhones")."""
    return _pedir("POST", "/lista-encomendas", {"descricao": descricao, "quantidade": quantidade,
                                                 "fornecedor": fornecedor or None, "nota": nota or None})


@mcp.tool()
def ver_faturas() -> str:
    """Faturas de fornecedor pendentes de classificar (só listar; quem classifica compra/despesa é o Bruno no Mender)."""
    return _pedir("GET", "/faturas")


@mcp.tool()
def registar_despesa(descricao: str, valor: float, categoria: str = "", fornecedor: str = "", data: str = "", notas: str = "") -> str:
    """Regista uma despesa. categoria: nome do enum do Mender (ex.: Combustivel, Software). data: AAAA-MM-DD (vazio = hoje)."""
    return _pedir("POST", "/despesas", {"descricao": descricao, "valor": valor, "categoria": categoria, "fornecedor": fornecedor,
                                         "data": data, "notas": notas})


if __name__ == "__main__":
    mcp.run()
