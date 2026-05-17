# Backup + Disaster Recovery runbook

Atualizado: 2026-05-16  
Projeto: RepairDesk SaaS - oficinas de reparacao em Portugal  
Fundador: Bruno Lopes / LopesTech

> Runbook operacional para SaaS 1-100 lojas. Nao substitui auditoria de seguranca, parecer juridico RGPD ou SLA contratual com provider. Precos e links devem ser reconfirmados antes de contratar. Onde faltar preco publico/contrato final, usar `{{confirmar}}`.

## Decisao curta

**Implementar esta semana:**

1. **SQL Server:** backup hot nativo, diario full + log backup horario, encriptado antes de sair do servidor.
2. **Off-site EU:** Backblaze B2 EU Central ou Hetzner Storage Box Alemanha/Finlandia como segunda localizacao; preferencia inicial: **Backblaze B2 EU Central com restic** pela simplicidade S3-compatible e preco por uso.
3. **Fotos Cloudflare R2:** bucket `eu`, privado, versioning/bucket lock/lifecycle onde disponivel; replicar export mensal para off-site se as fotos forem criticas.
4. **Snapshots do servidor:** ativar snapshot automatico diario no provider primario, mas tratar como recuperacao rapida, nao como unico backup.
5. **Teste de restore:** mensal, com checklist e screenshot/log guardado.

Objetivo inicial:

| Metrica | Beta 1-5 lojas | 10-100 lojas |
|---|---:|---:|
| RPO DB | <= 1 hora | <= 15-60 min |
| RTO DB crash | 2-4 horas | 1-2 horas |
| RTO servidor perdido | 4-8 horas | 2-4 horas com infra preparada |
| RPO fotos | <= 24 horas | <= 24 horas, ou menor se fotos forem core |
| Restore test | Mensal | Mensal + teste trimestral completo |

Leitura honesta: sem backup testado, nao ha backup. Ha apenas esperanca cara.

## 1. Estrategia 3-2-1

Regra 3-2-1 para o RepairDesk:

| Copia | Onde fica | Conteudo | Objetivo | Retencao |
|---|---|---|---|---|
| 1. Producao | Servidor/app + SQL Server + R2 | Dados vivos | Operacao diaria | N/A |
| 2. Local primario | Snapshot automatico do provider + backups `.bak` locais curtos | VM/volume + ultimos backups DB | Restore rapido quando o servidor ainda existe | 2-7 dias |
| 3. Off-site EU | B2 EU Central / Hetzner Storage Box / iDrive e2 EU | Backups encriptados DB, dumps config, manifests, exports essenciais | Recuperar se o provider/conta/servidor primario desaparecer | 30 dias diarios + 12 mensais |

Nao contar como backup unico:

- volume Docker local;
- snapshot no mesmo provider sem off-site;
- ficheiro `.bak` no mesmo disco;
- repositorio GitHub;
- export manual "quando me lembro".

## 2. SQL Server

### 2.1 Hot vs cold backup

**Usar backup hot nativo do SQL Server.** SQL Server suporta backup online com a base em uso; nao e necessario parar a app para fazer full/differential/log backup.

Evitar cold backup por defeito:

- parar container/app causa downtime;
- copiar ficheiros MDF/LDF de volume Docker pode gerar copia inconsistente;
- snapshot de volume pode funcionar em alguns casos, mas exige quiesce/coordenacao e teste.

### 2.2 Frequencia recomendada

Para SaaS com dados de clientes reais:

| Tipo | Frequencia | Retencao local | Retencao off-site | Nota |
|---|---:|---:|---:|---|
| Full backup | Diario, 02:00 Europe/Lisbon | 2-3 dias | 30 dias + 12 mensais | Base para restore simples. |
| Transaction log backup | Horario | 24-48 horas | 7-14 dias | Exige recovery model FULL. |
| Snapshot servidor | Diario | 2-7 dias | N/A | Recuperacao rapida, nao substitui DB backup. |
| Restore test | Mensal | N/A | N/A | Obrigatorio antes de beta real. |

Alternativa beta minima se o Bruno ainda nao quiser log backups: full diario + differential a cada 6 horas. Mas a recomendacao para producao e **FULL recovery model + log backup horario**, porque perder 24h de reparacoes/faturas/fotos/IMEIs e comercialmente inaceitavel.

### 2.3 Comandos base SQL Server

Exemplo T-SQL para preparar recovery model:

```sql
ALTER DATABASE [RepairDesk] SET RECOVERY FULL;
GO
```

Full backup:

```sql
BACKUP DATABASE [RepairDesk]
TO DISK = N'/var/opt/mssql/backups/RepairDesk_full_YYYYMMDD_HHMMSS.bak'
WITH COMPRESSION, CHECKSUM, STATS = 10;
GO
```

Log backup:

```sql
BACKUP LOG [RepairDesk]
TO DISK = N'/var/opt/mssql/backups/RepairDesk_log_YYYYMMDD_HHMMSS.trn'
WITH COMPRESSION, CHECKSUM, STATS = 10;
GO
```

Verificacao imediata do ficheiro:

```sql
RESTORE VERIFYONLY
FROM DISK = N'/var/opt/mssql/backups/RepairDesk_full_YYYYMMDD_HHMMSS.bak'
WITH CHECKSUM;
GO
```

Nota: encriptacao nativa do backup SQL Server e possivel com certificado/chave no SQL Server, mas aumenta a carga operacional porque e preciso guardar o certificado e private key com enorme cuidado. Para o MVP, a abordagem mais segura/simples e:

1. gerar `.bak`/`.trn` com `CHECKSUM`;
2. enviar para off-site com ferramenta que encripta client-side, como **restic**;
3. guardar a password/chave do restic fora do servidor, em 1Password/Bitwarden.

### 2.4 Backup job simples

MVP aceitavel:

- um container/servico `backup-runner` no mesmo Docker network;
- corre `sqlcmd` para gerar full/log backup;
- corre `restic backup` para enviar para off-site;
- apaga ficheiros locais antigos;
- envia alerta por email/Discord/ntfy se falhar.

Nao construir plataforma propria. O job deve ter menos de uma pagina de script e ser substituivel por managed backups quando o hosting mudar.

Pseudo-fluxo diario:

```text
02:00  gerar full .bak com CHECKSUM
02:10  RESTORE VERIFYONLY
02:15  restic backup /backups/sql --tag sql-full --host repairdesk-prod
02:30  restic forget --keep-daily 30 --keep-monthly 12 --prune
02:45  enviar resumo OK/falha
```

Pseudo-fluxo horario:

```text
HH:05 gerar .trn
HH:07 RESTORE VERIFYONLY
HH:10 restic backup /backups/sql --tag sql-log --host repairdesk-prod
HH:15 enviar alerta se falhar
```

## 3. Fotos em Cloudflare R2

Decisao ja alinhada com `Contexto/14-Storage-Fotos.md`: **Cloudflare R2 Standard, bucket privado em jurisdicao `eu`**.

Configuracao minima:

| Item | Valor |
|---|---|
| Bucket | `repairdesk-prod-media` |
| Jurisdicao | `eu` |
| Public access | Desligado |
| Acesso app | API token so para bucket/ambiente |
| URLs | Sempre signed URLs com TTL curto |
| CORS | So dominios RepairDesk |
| Metadata | `tenantId`, `repairId`, `retentionUntil`, `deleted=false/true` quando aplicavel |

Versioning/lifecycle:

- Ativar versioning se disponivel no plano/regiao no momento da implementacao.
- Ativar bucket lock/retention para proteger contra apagamento/ransomware onde fizer sentido. Cuidado: lock mal configurado tambem impede apagamento RGPD dentro do prazo.
- Lifecycle:
  - uploads pendentes: apagar apos 24-48h;
  - `trash/` ou `deleted=true`: hard-delete apos 30-60 dias;
  - fotos fechadas: reter 24 meses apos `Entregue`/`Cancelado`, salvo configuracao da loja;
  - backups/export mensais: manter 12 meses.

Importante RGPD: se versioning estiver ativo, pedido de apagamento tem de apagar **todas as versoes** do objeto, exceto se existir fundamento legal para conservar prova minima. Documentar isto no DPA.

### Off-site de fotos

R2 ja e storage duravel, mas nao protege contra:

- conta Cloudflare comprometida;
- bug da app que apaga objetos;
- token com permissao excessiva;
- erro humano em lifecycle.

Para 1-10 lojas: export mensal de manifest + objetos ativos criticos para off-site e suficiente.

Para 10-100 lojas: job semanal de `rclone sync --backup-dir` ou replication provider-to-provider para B2/Hetzner, com credenciais separadas e permissao de escrita sem delete sempre que possivel.

## 4. Logs

Retencao recomendada:

| Tipo de log | Conteudo | Retencao | Backup? |
|---|---|---:|---|
| App logs tecnicos | erros, endpoint, requestId, tenantId/userId | 30 dias | Nao, so centralizar se houver incidente. |
| Web/server logs | IP, user agent, URL sem query sensivel | 30-90 dias | Nao por defeito. |
| Security logs | login, falhas login, reset password, roles, MFA | 12 meses | Sim/export mensal. |
| Audit logs negocio | criar/editar/apagar reparacao/fatura/export/admin | 24 meses | Sim, ficam na DB. |
| Breach records | incidentes, decisoes, notificacoes | 5 anos | Sim, fora da app tambem. |

Regra: logs nao devem conter passwords, tokens, NIFs, IMEIs, telefones completos, notas de reparacao ou payloads de cliente. Logs com dados pessoais aumentam o blast radius do backup.

## 5. Config + secrets

### 5.1 Onde guardar

Recomendacao:

| Item | Onde guardar | Regra |
|---|---|---|
| Secrets producao | 1Password Business ou Bitwarden Teams | MFA obrigatorio; acesso Bruno + emergency contact. |
| `.env` producao | Nunca em Git; no servidor ou secret manager | Permissoes restritas. |
| `.env.example` | Git | Sem valores reais. |
| Backup encryption key/restic password | 1Password/Bitwarden, cofre separado | Nao guardar no mesmo servidor que os backups. |
| Recovery kit | Cofre partilhado com pessoa de confianca | Instrucoes minimas para Bruno doente/inacessivel. |
| Certificados SQL backup, se usados | Cofre separado + copia offline | Sem isto, backups SQL encriptados podem ficar irrecuperaveis. |

Nao fazer:

- secrets no GitHub;
- password de backup em `.env` versionado;
- chave de encriptacao no mesmo bucket onde estao backups;
- conta Cloudflare/hosting/GitHub sem MFA.

### 5.2 Emergency access

Antes de ter lojas pagantes:

- criar cofre "RepairDesk Emergency";
- incluir contactos de hosting, Cloudflare, Backblaze/Hetzner, GitHub, dominio, email;
- incluir runbook restore resumido;
- dar acesso de emergencia a uma pessoa de confianca ou contabilista/advogado tecnico designado;
- documentar criterio: pessoa so atua se Bruno estiver inacessivel por 48h e houver incidente critico.

## 6. Encriptacao

| Camada | Decisao |
|---|---|
| Em transito | HTTPS/TLS 1.2+; preferir TLS 1.3 onde provider suportar. |
| DB em repouso | Disco/volume encriptado no provider se disponivel; avaliar TDE quando houver hosting final. |
| Backups DB | Encriptacao client-side com restic antes de off-site. |
| Fotos R2 | Server-side encryption provider; bucket privado; signed URLs. |
| Chaves | Separacao de deveres: credencial DB nao deve permitir ler backup off-site; credencial backup nao deve permitir administrar DB. |
| MFA | Obrigatorio em Cloudflare, hosting, GitHub, email, storage, password manager. |

Separacao minima de credenciais:

- `repairdesk_app_db_user`: app normal, sem permissao de backup/restore.
- `repairdesk_backup_db_user`: permissao minima para backup.
- `repairdesk_restore_db_admin`: usado so em emergencia/teste.
- `backup_storage_key`: so escreve/le backups no bucket off-site.
- `cloudflare_r2_media_key`: so media bucket, nao backups.

## 7. Off-site EU - opcoes e custos

Precos publicos vistos em 2026-05-16, sem IVA e com cambio a confirmar.

| Provider | Regiao EU | Preco publico | Vantagem | Cuidado |
|---|---|---:|---|---|
| **Backblaze B2** | EU Central/Amsterdam | USD 6,95/TB-mes; egress gratis ate 3x storage medio; API calls gratis/sem custo relevante desde 2026-05-01 | S3-compatible, barato por GB, bom para restic/rclone | Confirmar DPA, regiao e faturacao; egress acima do limite tem custo. |
| **Hetzner Storage Box** | Alemanha/Finlandia | planos mudam; 1 TB historicamente poucos EUR/mes, `{{confirmar}}` no checkout | EU, barato, bom para Borg/restic via SSH/SFTP | Nao e object storage S3 puro; operacao um pouco mais manual. |
| **iDrive e2** | EU disponivel em regioes selecionadas | 1 TB anual com promos fortes; preco normal depende de plano, `{{confirmar}}` | S3-compatible, barato em anual | Promocoes confundem custo real; validar DPA/regiao. |
| **Cloudflare R2 segundo bucket** | `eu` | USD 0,015/GB-mes | Simples se ja ha Cloudflare | Nao e verdadeira diversidade se a conta Cloudflare for comprometida. |

Estimativa RepairDesk para DB backups, sem fotos:

| Cenario | DB ativa | Backup off-site estimado | Custo B2 aprox. |
|---|---:|---:|---:|
| 1-5 lojas | 1-5 GB | 30-80 GB | < USD 1/mes |
| 10-30 lojas | 10-30 GB | 150-500 GB | USD 1-4/mes |
| 100 lojas | 100-250 GB | 1-3 TB | USD 7-21/mes |

O custo real no inicio e irrelevante comparado com perder uma loja. O custo principal e tempo de configuracao/teste.

## 8. Teste de restore

### 8.1 Frequencia

| Teste | Frequencia | Tempo alvo |
|---|---:|---:|
| Verify backup file | A cada backup | automatico |
| Restore DB para ambiente temporario | Mensal | 30-60 min |
| Restore completo servidor/app+DB+fotos | Trimestral | 2-4h |
| Simulacro Bruno inacessivel | Semestral | 1h |

### 8.2 Procedimento mensal exacto

1. Criar VM/container temporario em ambiente dev/staging.
2. Restaurar ultimo full backup.
3. Aplicar log backups ate ao ponto mais recente escolhido.
4. Apontar API local/staging para DB restaurada.
5. Entrar com utilizador teste.
6. Validar 5 registos:
   - loja/tenant;
   - cliente;
   - reparacao aberta;
   - reparacao fechada;
   - fatura/recibo ou documento operacional, se existir.
7. Validar contagens:
   - numero de tenants;
   - numero de reparacoes;
   - numero de clientes;
   - ultima reparacao criada antes do backup.
8. Validar que fotos de 2 reparacoes abrem por signed URL, se storage estiver ativo.
9. Registar:
   - data/hora;
   - backup usado;
   - tempo de restore;
   - erros encontrados;
   - print/log de sucesso;
   - decisao: aprovado/reprovado.
10. Apagar ambiente temporario e dados restaurados.

Checklist mensal:

```text
[ ] Recebi alertas OK dos backups diarios nos ultimos 7 dias
[ ] Off-site contem backups dos ultimos 7 dias
[ ] Restic/rclone consegue listar snapshots/ficheiros
[ ] Restore DB mensal executado
[ ] CHECKDB/validacao basica sem erros
[ ] App abriu com DB restaurada
[ ] 5 registos validos confirmados
[ ] Fotos testadas, se aplicavel
[ ] RTO medido: ____ min
[ ] RPO medido: ____ min/h
[ ] Log do teste guardado em /Contexto ou cofre operacional
[ ] Falhas viraram tickets
```

## 9. Runbooks por cenario

### Cenario 1 - DB corrompida / SQL Server crash

Sinais:

- app devolve erros 500 em massa;
- SQL Server nao arranca;
- `DBCC CHECKDB` reporta corrupcao;
- logs indicam I/O error ou database suspect.

Passos:

1. Declarar incidente interno e registar hora.
2. Colocar app em maintenance mode/read-only, se possivel.
3. Parar writes da app.
4. Tirar copia do estado atual antes de mexer, se o disco ainda responde.
5. Identificar ultimo full backup valido e logs subsequentes.
6. Restaurar para DB nova/staging.
7. Aplicar logs ate ao ponto antes da corrupcao, se souber.
8. Validar com `DBCC CHECKDB`, contagens e login app.
9. Trocar connection string para DB restaurada ou substituir DB antiga.
10. Confirmar app com 2-3 fluxos criticos.
11. Comunicar lojas se houve downtime ou perda de dados.
12. Post-mortem em 48h.

Tempo estimado: 2-4h no MVP.  
RPO esperado: <= 1h se log backups horarios estiverem ok.

### Cenario 2 - Servidor inteiro perdido / provider crash / conta suspensa

Sinais:

- VM desapareceu;
- provider indisponivel;
- conta bloqueada;
- disco irrecuperavel.

Passos:

1. Confirmar se e incidente do provider ou da conta.
2. Abrir ticket urgente no provider primario.
3. Criar novo servidor em provider EU alternativo.
4. Instalar Docker/Docker Compose e dependencias.
5. Obter secrets no 1Password/Bitwarden emergency vault.
6. Fazer pull do codigo/imagem Docker.
7. Restaurar ultimo backup DB off-site.
8. Configurar DNS temporario ou reduzir TTL e apontar para novo servidor.
9. Configurar certificados TLS.
10. Validar login, tenants, reparacoes e storage fotos.
11. Reabrir app.
12. Comunicar clientes com timeline e impacto.

Tempo estimado: 4-8h se o runbook estiver praticado; 1-2 dias se for a primeira vez.

Preparacao necessaria antes:

- dominio/DNS em conta com MFA;
- imagens Docker reprodutiveis;
- `.env.example` atualizado;
- lista de secrets;
- off-site independente do provider primario.

### Cenario 3 - Foto storage hijacked / ransomware / apagamento em massa

Sinais:

- muitas fotos desaparecem;
- objetos substituidos por conteudo estranho;
- custos/operacoes disparam;
- credencial R2 comprometida.

Passos:

1. Revogar imediatamente tokens R2 suspeitos.
2. Colocar uploads de fotos em pausa.
3. Verificar audit logs Cloudflare/R2.
4. Desativar jobs de lifecycle/delete ate entender o impacto.
5. Confirmar se DB metadata ainda esta integra.
6. Restaurar objetos por versioning/bucket lock se disponivel.
7. Se nao houver versioning suficiente, restaurar a partir do off-site semanal/mensal.
8. Rodar todas as credenciais de storage.
9. Validar 20 fotos aleatorias em tenants afetados.
10. Avaliar se houve acesso/divulgacao de dados pessoais.
11. Ativar processo RGPD se houver risco para titulares.

Tempo estimado: 2-8h para conter; restore pode demorar horas/dias conforme volume.

Mitigacao previa:

- token app sem permissao de delete quando possivel;
- delete via backend controlado, nao direto do cliente;
- lifecycle conservador;
- versioning/lock para janela curta;
- off-site separado.

### Cenario 4 - Bruno doente / inacessivel por X dias

Gatilho:

- Bruno inacessivel por 48h e ha incidente P1;
- ou Bruno comunica indisponibilidade planeada.

Responsavel backup:

- Nome: `{{definir}}`
- Telefone: `{{definir}}`
- Email: `{{definir}}`
- Acesso: emergency vault com break-glass.

Passos:

1. Confirmar emergencia por 2 canais.
2. Aceder ao emergency vault.
3. Ler este runbook.
4. Confirmar estado de backups e app.
5. Se houver incidente, seguir cenario correspondente.
6. Contactar clientes apenas com template aprovado.
7. Nao fazer deploys nao urgentes.
8. Registar todas as acoes.

Preparacao:

- pessoa designada aceita a responsabilidade;
- teste semestral de acesso;
- lista de fornecedores e contratos;
- cartao/forma de pagamento secundaria para manter hosting ativo.

### Cenario 5 - Conta GitHub comprometida / perda de codigo fonte

Riscos:

- atacante altera codigo;
- secrets vazam;
- repo apagado;
- CI/CD publica imagem maliciosa.

Passos:

1. Revogar sessoes GitHub e tokens.
2. Rodar secrets potencialmente expostos.
3. Desativar deploy automatico.
4. Verificar ultimos commits, GitHub Actions e packages/images.
5. Restaurar repo a partir de clone local confiavel ou backup Git mirror.
6. Regenerar deploy keys.
7. Reativar CI/CD so depois de review.
8. Verificar producao contra imagem/commit esperado.
9. Se houve acesso a dados pessoais por secrets, iniciar processo breach.

Mitigacao previa:

- MFA obrigatorio;
- branch protection;
- required reviews para main quando houver equipa;
- secrets fora do repo;
- mirror privado off-site mensal, por exemplo `git bundle` encriptado no backup;
- export de GitHub issues/docs criticos se dependerem do GitHub.

## 10. Contactos de incidente

Preencher antes de beta:

| Entidade | Quando contactar | Contacto |
|---|---|---|
| Hosting primario | VM down, snapshots, conta suspensa | `{{definir}}` |
| Cloudflare | DNS, R2, conta, WAF, tokens | `{{definir}}` |
| Backblaze/Hetzner/iDrive | Off-site indisponivel | `{{definir}}` |
| GitHub | Conta/repo/Actions comprometidos | https://support.github.com/ |
| CNPD | Violacao de dados notificavel | https://www.cnpd.pt/DataBreach/DataBreach_pt.aspx |
| CNCS / CERT.PT | Incidente de ciberseguranca relevante | https://www.cncs.gov.pt/ |
| Clientes/lojas | Downtime, perda, breach | email operacional + app status |
| Advogado/DPO externo | Breach, responsabilidade, comunicacao | `{{definir}}` |

Nota sobre "ANSP": o termo deve ser confirmado. Para ciberseguranca em Portugal, a referencia operacional e o **CNCS/CERT.PT**; para dados pessoais, **CNPD**; para autoridades policiais, PSP/GNR/PJ conforme natureza criminal.

## 11. Comunicacao a clientes

### 11.1 Downtime sem perda de dados

```text
Assunto: RepairDesk - incidente tecnico em resolucao

Ola {nome},

Estamos a resolver um incidente tecnico que esta a afetar o acesso ao RepairDesk desde {hora}. Neste momento nao temos indicios de perda de dados nem de acesso nao autorizado.

Medida em curso: {resumo simples}.
Proxima atualizacao: ate {hora}.

Pedimos desculpa pelo impacto na operacao da loja. Vamos manter-vos informados ate a situacao ficar resolvida.

Bruno Lopes
LopesTech / RepairDesk
```

### 11.2 Restore com possivel perda limitada

```text
Assunto: RepairDesk - recuperacao de dados em curso

Ola {nome},

Tivemos um incidente tecnico que obrigou a restaurar a base de dados a partir de backup. Estamos a validar os dados da tua loja.

Estado atual:
- sistema: {online/em manutencao}
- janela potencialmente afetada: entre {hora} e {hora}
- dados potencialmente afetados: {ex.: reparacoes criadas/alteradas nesse periodo}

Estamos a trabalhar para reduzir o impacto e vamos enviar nova atualizacao ate {hora}. Se tiveres registos feitos nesse periodo, guarda-os por favor para reconfirmacao.

Bruno Lopes
LopesTech / RepairDesk
```

### 11.3 Violacao de dados pessoais potencial

```text
Assunto: RepairDesk - aviso preliminar sobre incidente de seguranca

Ola {nome},

Detetamos um incidente de seguranca que pode ter afetado dados tratados no RepairDesk. Ainda estamos a investigar, mas preferimos avisar-te cedo.

O que sabemos neste momento:
- data/hora detetada: {data/hora}
- lojas potencialmente afetadas: {todas/algumas/a confirmar}
- tipos de dados potencialmente afetados: {clientes, reparacoes, contactos, fotos, etc.}
- medidas ja tomadas: {credenciais revogadas, sistema isolado, backups verificados, etc.}

Estamos a avaliar o risco e a preparar a informacao necessaria para cumprimento do RGPD, incluindo notificacao a CNPD quando aplicavel. Enviaremos nova atualizacao ate {hora}.

Bruno Lopes
LopesTech / RepairDesk
```

### 11.4 Pos-incidente resolvido

```text
Assunto: RepairDesk - incidente resolvido e proximos passos

Ola {nome},

O incidente de {data} ficou resolvido as {hora}. Validamos o sistema e os backups, e a operacao esta novamente disponivel.

Resumo:
- causa: {resumo claro}
- impacto: {downtime/perda/acesso}
- dados afetados: {nenhum/descricao}
- medidas tomadas: {restore, rotacao de credenciais, patch, reforco de alertas}
- medidas futuras: {teste adicional, mudanca de processo}

Obrigado pela paciencia. Vamos guardar o registo do incidente e rever o processo para reduzir a probabilidade de repeticao.

Bruno Lopes
LopesTech / RepairDesk
```

## 12. Compliance RGPD

### 12.1 Artigo 32.o - seguranca

O art. 32.o do RGPD exige medidas tecnicas e organizativas apropriadas ao risco, incluindo, quando adequado:

- pseudonimizacao/encriptacao;
- capacidade de assegurar confidencialidade, integridade, disponibilidade e resiliencia;
- capacidade de restaurar disponibilidade e acesso aos dados pessoais de forma atempada;
- processo regular de teste, apreciacao e avaliacao das medidas.

Para RepairDesk, isto traduz-se em:

- backup automatico;
- encriptacao;
- restore test mensal;
- logs de incidentes;
- MFA;
- controlo de acessos;
- DPA com providers.

### 12.2 Artigo 33.o - notificacao CNPD

Se houver violacao de dados pessoais suscetivel de resultar em risco para direitos/liberdades, a notificacao a CNPD deve ser feita **sem demora injustificada e, quando possivel, ate 72h apos conhecimento**.

Mesmo quando a decisao for nao notificar, documentar:

- o que aconteceu;
- dados envolvidos;
- titulares afetados;
- avaliacao de risco;
- medidas tomadas;
- razao para notificar ou nao notificar.

Se o risco for elevado para os titulares, avaliar comunicacao aos titulares nos termos do art. 34.o.

### 12.3 DPA com providers

Antes de beta real, confirmar:

- hosting tem DPA;
- Cloudflare R2 tem DPA/subprocessor terms;
- Backblaze/Hetzner/iDrive tem DPA e regiao EU;
- email transacional tem DPA;
- todos aparecem na lista de sub-processadores do RepairDesk.

## 13. Plano de implementacao desta semana

Dia 1:

- escolher provider off-site EU;
- criar bucket/repo backup;
- criar credenciais com permissao minima;
- guardar secrets em 1Password/Bitwarden.

Dia 2:

- ativar SQL Server recovery model FULL;
- criar full backup manual;
- correr `RESTORE VERIFYONLY`;
- enviar primeiro backup com restic;
- confirmar que aparece off-site.

Dia 3:

- automatizar full diario;
- automatizar log backup horario;
- configurar alertas de falha.

Dia 4:

- fazer restore para ambiente temporario;
- medir RTO/RPO;
- corrigir falhas.

Dia 5:

- ativar snapshot diario provider;
- preencher contactos;
- criar emergency vault;
- guardar primeiro registo mensal de teste.

## 14. Fontes consultadas

- RGPD, Regulamento (UE) 2016/679, artigo 32.o e 33.o: https://eur-lex.europa.eu/eli/reg/2016/679/oj?locale=PT
- CNPD - Violacao de dados / data breach: https://www.cnpd.pt/organizacoes/obrigacoes/violacao-de-dados-ou-data-breach/
- CNPD - Formulario de notificacao de violacao de dados pessoais: https://www.cnpd.pt/DataBreach/DataBreach_pt.aspx
- Cloudflare R2 pricing: https://developers.cloudflare.com/r2/pricing/
- Cloudflare R2 object lifecycles: https://developers.cloudflare.com/r2/buckets/object-lifecycles/
- Cloudflare R2 bucket locks: https://developers.cloudflare.com/r2/buckets/bucket-locks/
- Backblaze B2 signup/current pricing: https://www.backblaze.com/sign-up/cloud-storage
- Backblaze B2 transaction/pricing docs: https://www.backblaze.com/cloud-storage/transaction-pricing
- Hetzner Storage Box: https://www.hetzner.com/storage/storage-box/
- iDrive e2 pricing: https://www.idrive.com/s3-storage-e2/pricing
- Microsoft SQL Server BACKUP DATABASE docs: https://learn.microsoft.com/sql/t-sql/statements/backup-transact-sql
- Microsoft SQL Server RESTORE VERIFYONLY docs: https://learn.microsoft.com/sql/t-sql/statements/restore-statements-verifyonly-transact-sql
- restic documentation: https://restic.readthedocs.io/
- rclone documentation: https://rclone.org/
