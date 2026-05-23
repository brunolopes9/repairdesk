import { useQuery } from '@tanstack/react-query';
import { dashboardApi } from './api';

function todayIsoDate() {
  return new Date().toISOString().slice(0, 10);
}

export function useDashboardKpisHoje(dia = todayIsoDate()) {
  return useQuery({
    queryKey: ['dashboard-kpis-hoje', dia],
    queryFn: () => dashboardApi.kpisHoje(dia),
    staleTime: 30_000,
  });
}
