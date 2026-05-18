import { api } from '../api';

export type BackupLocation = 'Local' | 'R2';
export type BackupTrigger = 'Scheduled' | 'Manual';

export interface BackupFileDto {
  fileName: string;
  location: BackupLocation;
  timestamp: string;
  sizeBytes: number;
  status: string;
  path: string | null;
  r2Key: string | null;
}

export interface BackupListResult {
  local: BackupFileDto[];
  r2: BackupFileDto[];
  latestLocalBackupAt: string | null;
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

export const backupApi = {
  list() {
    return api.get<BackupListResult>('/admin/backup/list').then((r) => r.data);
  },
  runNow() {
    return api.post<BackupRunResult>('/admin/backup/now').then((r) => r.data);
  },
};

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
}
