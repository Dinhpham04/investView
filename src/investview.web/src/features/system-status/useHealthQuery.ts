import { useQuery } from '@tanstack/react-query';
import { getHealth } from '../../shared/api/healthApi';

export function useHealthQuery() {
  return useQuery({
    queryKey: ['health'],
    queryFn: getHealth,
  });
}
