# Backup Restore Operations

Atualizado: 2026-05-19

## Objetivo

Esta pagina documenta a operacao da nova area **Definicoes > Backups**:

- listar backups locais e Cloudflare R2;
- ver saude/retencao;
- forcar backup manual;
- restaurar um backup com confirmacao forte;
- deixar rasto de auditoria.

O restore substitui a base de dados SQL Server completa. Nao e restore por tenant.

## Permissoes

| Operacao | Permissao |
|---|---|
| Listar backups | role `Admin` |
| Forcar backup agora | role `Admin` |
| Ver preview de restore | role `Admin` + `SuperAdmin` |
| Executar restore | role `Admin` + `SuperAdmin` |

Enquanto o produto estiver em beta, o restore deve ser usado apenas pelo owner SaaS/Bruno ou por alguem com autorizacao operacional explicita.

## Endpoints

| Metodo | Endpoint | Uso |
|---|---|---|
| `GET` | `/api/admin/backups` | lista local + R2, summary e health |
| `POST` | `/api/admin/backups/now` | backup manual imediato |
| `GET` | `/api/admin/backups/{id}/restore-preview` | snapshot atual + metadados do backup |
| `POST` | `/api/admin/backups/{id}/restore` | cria safety backup e restaura |

Aliases antigos mantidos:

- `GET /api/admin/backup/list`
- `POST /api/admin/backup/now`

## Fluxo seguro de restore

1. Operador abre **Definicoes > Backups**.
2. Escolhe backup por timestamp/localizacao.
3. Clica **Restore**.
4. UI chama `restore-preview`.
5. Modal mostra:
   - estado atual: reparacoes, clientes, trabalhos, vendas, despesas;
   - backup escolhido: contagens guardadas em metadados, quando existirem;
   - aviso de substituicao irreversivel.
6. Operador escreve `RESTORE`.
7. Backend cria primeiro um safety backup manual.
8. Backend executa `RESTORE DATABASE`.
9. Backend escreve auditoria com backup restaurado e safety backup.

Backups antigos que nao tenham ficheiro `.json` de metadata continuam restauraveis, mas a UI mostra que nao existem contagens historicas.

## Comando SQL usado

O restore e executado contra `master`:

```sql
ALTER DATABASE [RepairDesk] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [RepairDesk]
FROM DISK = N'/backups/repairdesk-YYYYMMDD-HHMM.bak'
WITH REPLACE, RECOVERY, CHECKSUM;
ALTER DATABASE [RepairDesk] SET MULTI_USER;
```

Para R2, o ficheiro e primeiro descarregado para `/backups/_restore/` dentro do volume partilhado com o container SQL Server.

## Retention dashboard

Valores expostos na UI:

- numero de backups locais;
- numero de backups R2;
- idade do backup mais recente;
- espaco local usado;
- policy: `Backup__RetentionDays` local e `Backup__R2RetentionDays` para R2.

Health:

| Cor | Regra |
|---|---|
| Verde | ultimo backup ha menos de 26h e status OK |
| Amarelo | ultimo backup entre 26h e 48h |
| Vermelho | sem backup, ultimo falhado, ou idade superior a 48h |

## Variaveis de ambiente

```env
Backup__Enabled=true
Backup__CronSchedule=03:00
Backup__RetentionDays=30
Backup__R2RetentionDays=90
Backup__LocalPath=/backups
Backup__DatabaseName=RepairDesk
Backup__R2__Bucket=repairdesk-prod-backups
Backup__R2__Prefix=backups

Storage__R2__AccountId=...
Storage__R2__AccessKey=...
Storage__R2__Secret=...
```

## Operacao manual de emergencia

Listar R2:

```bash
aws --endpoint-url "https://${R2_ACCOUNT_ID}.r2.cloudflarestorage.com" \
  s3 ls "s3://${R2_BUCKET}/backups/"
```

Descarregar:

```bash
aws --endpoint-url "https://${R2_ACCOUNT_ID}.r2.cloudflarestorage.com" \
  s3 cp "s3://${R2_BUCKET}/backups/repairdesk-20260518-0300.bak" \
  "./backups/repairdesk-20260518-0300.bak"
```

Validar depois do restore:

```bash
docker exec repairdesk-db /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$DB_SA_PASSWORD" -No \
  -Q "DBCC CHECKDB([RepairDesk]) WITH NO_INFOMSGS;"
```

## Riscos

- Restore e global da DB: afeta todos os tenants.
- Safety backup criado antes do restore tambem deve ser verificado no dashboard/R2.
- Se o backup restaurado for anterior a migrations recentes, funcionalidades novas podem precisar de migracao novamente.
- Se o backup nao tiver metadata `.json`, a UI nao consegue mostrar contagens historicas.
- O restore deve ser testado num ambiente fresco antes de beta pago.
