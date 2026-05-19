import { api } from '../api';

export type BackupLocation = 'Local' | 'R2' | 0 | 1;
export type BackupTrigger = 'Scheduled' | 'Manual';
export type BackupHealthStatus = 'Green' | 'Yellow' | 'Red' | 0 | 1 | 2;

export interface BackupSnapshotDto {
  reparacoes: number;
  clientes: number;
  trabalhos: number;
  vendas: number;
  despesas: number;
  capturedAt: string;
}

export interface BackupFileDto {
  id: string;
  fileName: string;
  location: BackupLocation;
  timestamp: string;
  sizeBytes: number;
  status: string;
  ageHours: number;
  snapshot: BackupSnapshotDto | null;
  path: string | null;
  r2Key: string | null;
}

export interface BackupListResult {
  local: BackupFileDto[];
  r2: BackupFileDto[];
  items: BackupFileDto[];
  latestLocalBackupAt: string | null;
  latestBackupAt: string | null;
  latestBackupAgeHours: number | null;
  healthStatus: BackupHealthStatus;
  localBytesUsed: number;
  localRetentionDays: number;
  r2RetentionDays: number;
  status: string;
}

export interface BackupRunResult {
  fileName: string;
  localPath: string;
  r2Key: string | null;
  sizeBytes: number;
  startedAt: string;
  completedAt: string;
  trigger: BackupTrigger;
  uploadedToR2: boolean;
  deletedLocalFiles: number;
  status: string;
}

export interface BackupRestorePreviewDto {
  backup: BackupFileDto;
  currentSnapshot: BackupSnapshotDto;
  backupSnapshot: BackupSnapshotDto | null;
  warning: string;
}

export interface BackupRestoreResult {
  restoredBackup: BackupFileDto;
  safetyBackup: BackupRunResult;
  currentSnapshotBeforeRestore: BackupSnapshotDto;
  backupSnapshot: BackupSnapshotDto | null;
  restoredAt: string;
  status: string;
}

export const backupApi = {
  list() {
    return api.get<BackupListResult>('/admin/backups').then((r) => r.data);
  },
  runNow() {
    return api.post<BackupRunResult>('/admin/backups/now').then((r) => r.data);
  },
  restorePreview(id: string) {
    return api.get<BackupRestorePreviewDto>(`/admin/backups/${encodeURIComponent(id)}/restore-preview`).then((r) => r.data);
  },
  restore(id: string, confirmationText: string) {
    return api.post<BackupRestoreResult>(`/admin/backups/${encodeURIComponent(id)}/restore`, { confirmationText }).then((r) => r.data);
  },
};

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
}
